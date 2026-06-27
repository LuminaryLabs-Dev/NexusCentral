#!/usr/bin/env python3
import json
import os
import subprocess
import sys
from pathlib import Path

from nexuscentral_wrapper_common import (
    call_thinking,
    emit,
    ensure_output_dir,
    handle_failure,
    load_spec,
    read_artifact_texts,
    unwrap_output,
    write_artifact,
)


def parse_env_text(raw_text):
    env = {}
    for line in (raw_text or "").splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#") or "=" not in stripped:
            continue
        key, value = stripped.split("=", 1)
        key = key.strip()
        if key:
            env[key] = value.strip()
    return env


def build_extra_flags(metadata):
    flags = []
    if str((metadata or {}).get("max_iterations", "")).strip():
        flags.extend(["--max-iterations", str(metadata["max_iterations"]).strip()])
    elif str((metadata or {}).get("max_repair_attempts", "")).strip():
        flags.extend(["--max-iterations", str(metadata["max_repair_attempts"]).strip()])

    if str((metadata or {}).get("build", "true")).lower() in {"0", "false", "no", "off"}:
        flags.append("--no-build")
    else:
        flags.append("--build")

    if str((metadata or {}).get("offline", "false")).lower() in {"1", "true", "yes", "on"}:
        flags.append("--offline")
    if str((metadata or {}).get("max_runtime_minutes", "")).strip():
        try:
            flags.extend(["--max-runtime-seconds", str(int(float(str(metadata["max_runtime_minutes"]).strip()) * 60))])
        except ValueError:
            pass
    elif str((metadata or {}).get("max_runtime_seconds", "")).strip():
        flags.extend(["--max-runtime-seconds", str(metadata["max_runtime_seconds"]).strip()])
    if str((metadata or {}).get("max_model_calls", "")).strip():
        flags.extend(["--max-model-calls", str(metadata["max_model_calls"]).strip()])
    if str((metadata or {}).get("context_budget_chars", "")).strip():
        flags.extend(["--context-budget-chars", str(metadata["context_budget_chars"]).strip()])
    if str((metadata or {}).get("require_approval", "true")).lower() in {"0", "false", "no", "off"}:
        flags.append("--no-require-approval")
    return flags


def forward_or_log_wrapper_line(line):
    try:
        payload = json.loads(line)
    except json.JSONDecodeError:
        emit("log", line)
        return None
    if isinstance(payload, dict) and payload.get("type"):
        print(json.dumps(payload), flush=True)
        return payload
    if isinstance(payload, dict) and payload.get("final_status"):
        return payload
    emit("log", line)
    return None


def normalize_outcome(final_status):
    normalized = str(final_status or "").strip().lower()
    if normalized == "valid":
        return "success"
    if normalized in {"invalid", "blocked", "stopped"}:
        return normalized
    return normalized or "unknown"


def build_final_summary(report):
    status = normalize_outcome(report.get("final_status"))
    issues = [str(issue).strip() for issue in report.get("issues") or [] if str(issue).strip()]
    applied_files = [str(path).strip() for path in report.get("applied_files") or [] if str(path).strip()]
    iterations = report.get("iterations") or []
    artifact_paths = []
    ledger = str(report.get("ledger") or "").strip()
    if ledger:
        artifact_paths.extend([
            str(Path(ledger) / "final_report.md"),
            str(Path(ledger) / "report.json"),
            str(Path(ledger) / "validation.md"),
        ])
    final_text = str(report.get("final_text") or "").strip()
    if not final_text:
        final_text = "NexusGLMHarness completed without final text."

    summary = {
        "outcome": status,
        "run_id": report.get("run_id", ""),
        "workspace": report.get("workspace", ""),
        "what_it_did": final_text,
        "files_changed_count": len(applied_files),
        "files_changed_preview": applied_files[:12],
        "validation": "pass" if status == "success" else "needs attention",
        "problems": issues[:12],
        "artifact_paths": artifact_paths,
        "repair_iterations": max(0, len(iterations) - 1),
        "next_action": "Open the generated workspace and inspect the preview." if status == "success" else "Review the listed problems, then rerun repair with the same goal.",
    }
    return summary


def render_final_user_message(summary):
    lines = [
        f"Outcome: {summary['outcome']}",
        "",
        summary["what_it_did"],
        "",
        f"Files changed: {summary['files_changed_count']}",
        f"Validation: {summary['validation']}",
    ]
    if summary["problems"]:
        lines.extend(["", "Problems:"])
        lines.extend(f"- {problem}" for problem in summary["problems"])
    if summary["artifact_paths"]:
        lines.extend(["", "Artifacts:"])
        lines.extend(f"- {path}" for path in summary["artifact_paths"])
    lines.extend(["", f"Next: {summary['next_action']}"])
    return "\n".join(lines).strip()


def build_spec_doc_context(spec):
    return read_artifact_texts(spec.get("artifact_inputs") or [], limit=8, bytes_limit=80000)


def build_augmented_spec_for_spec_docs(spec, output_dir):
    attachment_context = build_spec_doc_context(spec)
    if not attachment_context:
        return spec, None

    augmented = dict(spec)
    prompt = spec.get("prompt", "")
    augmented["prompt"] = (
        f"{prompt}\n\n"
        "# Attached Spec Documents\n\n"
        "Treat the following spec document text as authoritative project input. Use it during intake, "
        "breakdown, file planning, file authoring, validation, and final reconciliation. Extract concrete "
        "requirements first, leave unknowns explicitly blank, and build files from the cleaned requirements "
        "instead of summarizing the spec back to the user.\n\n"
        f"{attachment_context}\n"
    ).strip()
    augmented_path = output_dir / "nexusglmharness-run-spec-with-spec-docs.json"
    augmented_path.write_text(json.dumps(augmented, indent=2), encoding="utf-8")
    emit("artifact", label=augmented_path.name, path=str(augmented_path), kind="file")
    emit("status", f"Loaded {len(spec.get('artifact_inputs') or [])} attached spec document(s) into NexusGLMHarness intake")
    return augmented, augmented_path


def find_nexusglm_root(repo_root):
    candidates = []
    if repo_root.exists():
        candidates.extend(
            [
                repo_root / "NexusGLMHarness",
                repo_root.parent / "NexusGLMHarness",
            ]
        )
        for parent in [repo_root, *repo_root.parents]:
            if parent.name == "GitHub":
                candidates.append(parent / "NexusGLMHarness")
                break
    candidates.append(Path.home() / "Documents" / "GitHub" / "NexusGLMHarness")
    for root in candidates:
        if (root / "src" / "nexus_glm_harness" / "cli.py").exists():
            return root.resolve()
    return None


def try_run_real_nexusglm(spec, spec_path, output_dir):
    augmented_spec, augmented_path = build_augmented_spec_for_spec_docs(spec, output_dir)
    if augmented_path is not None:
        spec = augmented_spec
        spec_path = augmented_path

    metadata = spec.get("metadata", {}) or {}
    repo_root = Path(metadata.get("workspace_root", "")).resolve()
    if not repo_root.exists():
        repo_root = Path(spec.get("root_directory") or ".").resolve()

    working_directory = Path(metadata.get("working_directory") or output_dir).resolve()
    if not working_directory.exists():
        working_directory = Path(output_dir).resolve()

    root = find_nexusglm_root(repo_root)
    if root is None:
        return None

    cmd = [
        sys.executable,
        "-m",
        "nexus_glm_harness.cli",
        "nexuscentral-run",
        "--run-spec",
        str(spec_path),
    ]
    cmd.extend(build_extra_flags(metadata))
    emit("status", "Starting NexusGLMHarness adapter")
    env = os.environ.copy()
    env["PYTHONPATH"] = str(root / "src")
    env.update(parse_env_text(metadata.get("workflow_environment_text", "")))
    process = subprocess.Popen(
        cmd,
        cwd=str(working_directory if working_directory.exists() else root),
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        env=env,
    )
    output_lines = []
    final_text = ""
    saw_final_event = False
    report_payload = None
    for line in process.stdout or []:
        clean_line = line.rstrip()
        output_lines.append(clean_line)
        payload = forward_or_log_wrapper_line(clean_line)
        if isinstance(payload, dict) and payload.get("type") == "final":
            final_text = str(payload.get("text") or "").strip()
            saw_final_event = True
        elif isinstance(payload, dict) and payload.get("final_status"):
            report_payload = payload
    process.wait()
    if process.returncode == 0:
        if report_payload:
            summary = build_final_summary(report_payload)
            user_message = render_final_user_message(summary)
            write_artifact(output_dir, "final-summary.json", json.dumps(summary, indent=2, ensure_ascii=True) + "\n")
            write_artifact(output_dir, "final-summary.md", user_message + "\n")
            write_artifact(output_dir, "final-user-message.md", user_message + "\n")
            final_text = user_message
        if not final_text:
            final_text = "\n".join(output_lines).strip()
        write_artifact(output_dir, "nexusglmharness-output.md", final_text or "No output produced.")
        if report_payload or not saw_final_event:
            emit("final", final_text)
    return process.returncode


def main():
    if "--run-spec" not in sys.argv:
        handle_failure("Missing --run-spec")
    spec_path = Path(sys.argv[sys.argv.index("--run-spec") + 1]).expanduser().resolve()
    spec = load_spec(spec_path)
    output_dir = ensure_output_dir(spec)

    return_code = try_run_real_nexusglm(spec, spec_path, output_dir)
    if return_code == 0:
        return
    if return_code is not None:
        handle_failure(f"NexusGLMHarness exited with code {return_code}")

    emit("status", "NexusGLMHarness not installed, using adapter fallback")
    attachment_context = build_spec_doc_context(spec)
    composed_prompt = (
        "Perform this task in a NexusGLMHarness-style bounded run. Provide only the final deliverable.\n\n"
        f"Task:\n{spec.get('prompt', '')}\n\n"
    )
    if attachment_context:
        composed_prompt += f"Artifacts:\n{attachment_context}\n"

    try:
        emit("log", "Calling /nexuscentral/thinking")
        response = unwrap_output(call_thinking(spec, composed_prompt))
        final_text = response.get("output", "").strip()
        write_artifact(output_dir, "nexusglmharness-output.md", final_text or "No output produced.")
        emit("final", final_text)
    except Exception as exc:
        handle_failure(str(exc))


if __name__ == "__main__":
    main()
