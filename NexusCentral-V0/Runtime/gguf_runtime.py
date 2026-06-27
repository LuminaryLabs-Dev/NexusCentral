#!/usr/bin/env python3
"""NexusCentral JSON adapter for regular GGUF models through llama-cpp-python."""

from __future__ import annotations

import argparse
import json
import math
import os
import sys
import time
from pathlib import Path


BACKEND = "llama.cpp"


def main() -> int:
    parser = argparse.ArgumentParser(prog="NexusCentral-GGUF-Runtime")
    parser.add_argument("--model", required=True)
    parser.add_argument("--n-predict", "--n_predict", dest="n_predict", type=int, default=120)
    parser.add_argument("--n-ctx", "--ctx-size", dest="ctx_size", type=int, default=2048)
    parser.add_argument("--threads", type=int, default=0)
    parser.add_argument("--mode", choices=("thinking", "instruct"), default="thinking")
    parser.add_argument("--max-memory-mb", type=int, default=2048)

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

    try:
        from llama_cpp import Llama
    except Exception as exc:
        return emit_result(
            False,
            args,
            "",
            "",
            "LLAMA_CPP_PYTHON_MISSING",
            f"Python package llama-cpp-python is not installed or could not load: {exc}",
            started,
        )

    settings = derive_settings(args.max_memory_mb, args.ctx_size)
    threads = args.threads if args.threads > 0 else max(1, min(os.cpu_count() or 1, 4))
    gpu_layers = int(os.environ.get("NEXUSCENTRAL_GGUF_GPU_LAYERS", "0") or "0")

    try:
        llm = Llama(
            model_path=str(model_path),
            n_ctx=settings["n_ctx"],
            n_batch=settings["n_batch"],
            n_ubatch=settings["n_ubatch"],
            n_threads=threads,
            n_gpu_layers=gpu_layers,
            use_mmap=True,
            use_mlock=False,
            verbose=False,
        )
    except Exception as exc:
        return emit_result(False, args, "", "", "GGUF_MODEL_LOAD_FAILED", str(exc), started)

    max_tokens = max(1, min(args.n_predict, 5000))
    try:
        output = run_chat_or_completion(llm, args.prompt, max_tokens)
    except Exception as exc:
        return emit_result(False, args, "", "", "GGUF_INFERENCE_FAILED", str(exc), started)

    return emit_result(True, args, normalize_output(output), output, None, None, started)


def derive_settings(memory_mb: int, requested_ctx: int) -> dict[str, int]:
    cap = max(50, min(memory_mb if memory_mb > 0 else 2048, 2048))
    if cap <= 64:
        ctx, batch, ubatch = 256, 16, 8
    elif cap <= 128:
        ctx, batch, ubatch = 384, 24, 12
    elif cap <= 256:
        ctx, batch, ubatch = 512, 32, 16
    elif cap <= 512:
        ctx, batch, ubatch = 768, 64, 32
    elif cap <= 1024:
        ctx, batch, ubatch = 1024, 96, 48
    else:
        ctx, batch, ubatch = 1536, 128, 64

    ctx = max(128, min(max(1, requested_ctx), ctx))
    return {
        "n_ctx": ctx,
        "n_batch": max(1, min(batch, ctx)),
        "n_ubatch": max(1, min(ubatch, ctx)),
    }


def run_chat_or_completion(llm, prompt: str, max_tokens: int) -> str:
    try:
        response = llm.create_chat_completion(
            messages=[{"role": "user", "content": prompt}],
            max_tokens=max_tokens,
            temperature=0.2,
            top_p=0.95,
        )
        choice = response.get("choices", [{}])[0]
        message = choice.get("message", {})
        content = message.get("content", "")
        if content:
            return str(content)
    except Exception:
        pass

    completion = llm(
        f"User: {prompt.strip()}\nAssistant:",
        max_tokens=max_tokens,
        temperature=0.2,
        top_p=0.95,
        stop=["</s>", "<|im_end|>", "User:"],
    )
    return str(completion.get("choices", [{}])[0].get("text", ""))


def normalize_output(raw_output: str) -> str:
    return raw_output.strip()


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
        "backend": BACKEND,
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
    return 0 if not text else max(1, math.ceil(len(text.split()) * 1.25))


if __name__ == "__main__":
    raise SystemExit(main())
