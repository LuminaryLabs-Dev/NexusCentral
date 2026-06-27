#!/usr/bin/env python3
"""NexusCentral JSON adapter for Microsoft bitnet.cpp."""

from __future__ import annotations

import argparse
import json
import math
import os
import subprocess
import sys
import time
from pathlib import Path


BITNET_BACKEND = "bitnet.cpp"


def main() -> int:
    parser = argparse.ArgumentParser(prog="NexusCentral-BitNet-Runtime")
    parser.add_argument("--model", required=True)
    parser.add_argument("--n-predict", "--n_predict", dest="n_predict", type=int, default=120)
    parser.add_argument("--n-ctx", "--ctx-size", dest="ctx_size", type=int, default=2048)
    parser.add_argument("--threads", type=int, default=0)
    parser.add_argument("--mode", choices=("thinking", "instruct"), default="thinking")
    parser.add_argument("--max-memory-mb", type=int, default=0)

    subparsers = parser.add_subparsers(dest="command")
    run_once = subparsers.add_parser("run-once")
    run_once.add_argument("--prompt", required=True)
    run_once.add_argument("--json", action="store_true")
    run_once.add_argument("--stream-json", action="store_true")

    args = parser.parse_args()
    if args.command != "run-once":
        return emit_result(False, args, "", "", "UNSUPPORTED_COMMAND", "Only run-once is supported.")

    started = time.time()
    model_path = Path(args.model).expanduser().resolve()
    if not model_path.exists():
        return emit_result(False, args, "", "", "MODEL_FILE_MISSING", f"Model file was not found at {model_path}.", started)

    estimated_memory = estimate_memory_mb(model_path)
    if args.max_memory_mb > 0 and estimated_memory > args.max_memory_mb:
        return emit_result(
            False,
            args,
            "",
            "",
            "BLOCKED_BY_MEMORY_BUDGET",
            f"Estimated BitNet memory {estimated_memory} MB exceeds configured budget {args.max_memory_mb} MB.",
            started,
        )

    bitnet_root = resolve_bitnet_root()
    if bitnet_root is None:
        return emit_result(
            False,
            args,
            "",
            "",
            "BITNET_RUNTIME_NOT_CONFIGURED",
            "Set NEXUSCENTRAL_BITNET_ROOT or place Microsoft BitNet at Runtime/BitNet.",
            started,
        )

    inference_script = bitnet_root / "run_inference.py"
    if not inference_script.exists():
        return emit_result(
            False,
            args,
            "",
            "",
            "BITNET_RUNTIME_NOT_FOUND",
            f"run_inference.py was not found under {bitnet_root}.",
            started,
        )

    command = [
        sys.executable,
        str(inference_script),
        "-m",
        str(model_path),
        "-n",
        str(max(1, min(args.n_predict, 5000))),
        "-p",
        args.prompt,
        "-c",
        str(max(1, args.ctx_size)),
    ]
    if args.threads > 0:
        command.extend(["-t", str(args.threads)])
    if args.mode == "instruct":
        command.append("-cnv")

    try:
        completed = subprocess.run(
            command,
            cwd=str(bitnet_root),
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
    except Exception as exc:
        return emit_result(False, args, "", "", "BITNET_RUNTIME_START_FAILED", str(exc), started)

    raw_output = completed.stdout.strip()
    output = normalize_output(raw_output)
    if completed.returncode != 0:
        message = completed.stderr.strip() or raw_output or f"BitNet exited with code {completed.returncode}."
        return emit_result(False, args, output, raw_output, "BITNET_RUNTIME_ERROR", message, started)

    return emit_result(True, args, output, raw_output, None, None, started)


def resolve_bitnet_root() -> Path | None:
    configured = os.environ.get("NEXUSCENTRAL_BITNET_ROOT", "").strip()
    candidates = []
    if configured:
        candidates.append(Path(configured).expanduser())
    candidates.append(Path(__file__).resolve().parent / "BitNet")
    candidates.append(Path.cwd() / "Runtime" / "BitNet")
    candidates.append(Path.cwd() / "BitNet")

    for candidate in candidates:
        if (candidate / "run_inference.py").exists():
            return candidate.resolve()
    return None


def estimate_memory_mb(model_path: Path) -> int:
    file_mb = model_path.stat().st_size / (1024 * 1024)
    return max(512, math.ceil(file_mb * 1.25 + 512))


def normalize_output(raw_output: str) -> str:
    if not raw_output:
        return ""

    lines = [line.rstrip() for line in raw_output.splitlines()]
    dropped_prefixes = (
        "usage:",
        "warning:",
        "model:",
        "prompt:",
        "main:",
        "llama_",
    )
    useful = [
        line for line in lines
        if line.strip() and not line.strip().lower().startswith(dropped_prefixes)
    ]
    return "\n".join(useful).strip() or raw_output.strip()


def emit_result(
    ok: bool,
    args: argparse.Namespace,
    output: str,
    raw_output: str,
    error_code: str | None,
    error: str | None,
    started: float | None = None,
) -> int:
    elapsed_ms = int((time.time() - started) * 1000) if started else 0
    data = {
        "ok": ok,
        "model_path": str(Path(args.model).expanduser()),
        "prompt": getattr(args, "prompt", ""),
        "output": output,
        "raw_output": raw_output or output,
        "finish_reason": "stop" if ok else "error",
        "tokens_generated": estimate_tokens(output),
        "backend": BITNET_BACKEND,
        "elapsed_ms": elapsed_ms,
        "error_code": error_code,
        "error": error,
    }

    if getattr(args, "stream_json", False):
        if output:
            print(json.dumps({"type": "chunk", "phase": "answer", "text": output}), flush=True)
        print(json.dumps({"type": "final", "data": data}), flush=True)
    else:
        print(json.dumps(data), flush=True)

    return 0 if ok else 1


def estimate_tokens(text: str) -> int:
    return 0 if not text else max(1, len(text.split()))


if __name__ == "__main__":
    raise SystemExit(main())
