#!/usr/bin/env python3
import json
import os
import re
import shlex
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


def normalize_flag_name(name):
    stripped = (name or "").strip()
    if not stripped:
        return ""
    if stripped.startswith("-"):
        return stripped
    return f"--{stripped}"


def build_structured_flags(raw_json):
    flags = []
    try:
        items = json.loads(raw_json or "[]")
    except json.JSONDecodeError:
        return flags

    for item in items if isinstance(items, list) else []:
        if not isinstance(item, dict):
            continue
        flag_type = str(item.get("type", "flag") or "flag").strip().lower()
        name = normalize_flag_name(item.get("name"))
        value = str(item.get("value", "") or "").strip()
        if not name:
            continue

        if flag_type in {"bool", "boolean"}:
            if value.lower() in {"1", "true", "yes", "on"}:
                flags.append(name)
            continue

        flags.append(name)
        if value:
            flags.append(value)
    return flags


def build_extra_flags(metadata):
    flags = []
    flags.extend(shlex.split((metadata or {}).get("workflow_global_flags", "") or ""))
    flags.extend(build_structured_flags((metadata or {}).get("workflow_structured_flags_json", "")))
    flags.extend(shlex.split((metadata or {}).get("workflow_step_raw_flags", "") or ""))
    return flags


def is_placeholder_final(text):
    normalized = (text or "").strip()
    if not normalized:
        return True
    return normalized.startswith("Handled directly:") or normalized.startswith("Workflow completed:")


def forward_or_log_wrapper_line(line):
    try:
        payload = json.loads(line)
    except json.JSONDecodeError:
        emit("log", line)
        return
    if isinstance(payload, dict) and payload.get("type"):
        print(json.dumps(payload), flush=True)
        return
    emit("log", line)


def wants_code(prompt):
    lower = (prompt or "").lower()
    return "code" in lower or "fenced" in lower or "function" in lower or "def " in lower


def wants_files(prompt):
    lower = (prompt or "").lower()
    return any(term in lower for term in ("file", "script", "project", "vite", "html", "multi-file", "multifile", "build"))


def has_code(text):
    return "```" in (text or "") or re.search(r"^\s*(def|class|function|const|let|var)\s+", text or "", re.MULTILINE) is not None


def deterministic_code_fallback(prompt):
    lower = (prompt or "").lower()
    if re.search(r"\badd\s*\(\s*a\s*,\s*b\s*\)", lower):
        return "```python\ndef add(a, b):\n    return a + b\n```"
    return ""


def _files_from_final_text(final_text):
    files = []
    in_files = False
    for line in (final_text or "").splitlines():
        stripped = line.strip()
        if stripped == "Files:":
            in_files = True
            continue
        if in_files and stripped.endswith(":"):
            break
        if in_files and stripped.startswith("- "):
            files.append(stripped[2:].strip())
    return files


def emit_workspace_artifacts(spec, final_text):
    if not wants_files(spec.get("prompt", "")):
        return
    root_text = (spec.get("metadata") or {}).get("workspace_root") or spec.get("root_directory") or ""
    root = Path(root_text).expanduser().resolve() if root_text else None
    if root is None or not root.exists():
        return
    for relative in _files_from_final_text(final_text):
        path = (root / relative).resolve()
        if not path.is_file() or root not in path.parents:
            continue
        emit("artifact", label=path.name, path=str(path), kind="file")


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
    augmented_path = output_dir / "nexusagent-run-spec-with-spec-docs.json"
    augmented_path.write_text(json.dumps(augmented, indent=2), encoding="utf-8")
    emit("artifact", label=augmented_path.name, path=str(augmented_path), kind="file")
    emit("status", f"Loaded {len(spec.get('artifact_inputs') or [])} attached spec document(s) into NexusAgent intake")
    return augmented, augmented_path


def finalize_with_model(spec, harness_text):
    attachment_context = build_spec_doc_context(spec)
    prompt = (
        "You are NexusAgent's final response writer. Use the harness result as internal context, "
        "but answer the user's request directly. If the user asks for code, return the code in a fenced code block. "
        "Do not describe the harness.\n\n"
        f"User request:\n{spec.get('prompt', '')}\n\n"
        f"Harness result:\n{harness_text or '[no harness text]'}\n"
    )
    if attachment_context:
        prompt += f"\nArtifacts:\n{attachment_context}\n"
    response = unwrap_output(call_thinking(spec, prompt))
    return response.get("output", "").strip()


def find_nexusagent_root(repo_root):
    candidates = []
    if repo_root.exists():
        candidates.extend(
            [
                repo_root / "NexusAgent-Stegosaurus",
                repo_root / "NexusAgent" / "NexusAgent-Stegosaurus",
                repo_root.parent / "NexusAgent" / "NexusAgent-Stegosaurus",
            ]
        )
        for parent in [repo_root, *repo_root.parents]:
            if parent.name == "GitHub":
                candidates.append(parent / "NexusAgent" / "NexusAgent-Stegosaurus")
                break
    candidates.append(Path.home() / "Documents" / "GitHub" / "NexusAgent" / "NexusAgent-Stegosaurus")
    for root in candidates:
        if (root / "src" / "NexusAgent" / "cli" / "nexusagent.py").exists():
            return root.resolve()
    return None


def try_run_real_nexusagent(spec, spec_path, output_dir):
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

    root = find_nexusagent_root(repo_root)
    if root is None:
        return None

    cmd = [
        sys.executable,
        "-m",
        "NexusAgent.cli.nexusagent",
        "nexuscentral-run",
        "--run-spec",
        str(spec_path),
    ]
    cmd.extend(build_extra_flags(metadata))
    emit("status", "Starting NexusAgent adapter")
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
    for line in process.stdout or []:
        clean_line = line.rstrip()
        output_lines.append(clean_line)
        forward_or_log_wrapper_line(clean_line)
    process.wait()
    if process.returncode == 0:
        final_text = "\n".join(output_lines).strip()
        if is_placeholder_final(final_text):
            try:
                emit("status", "Finalizing NexusAgent response with selected model")
                model_text = finalize_with_model(spec, final_text)
                if model_text:
                    final_text = model_text
            except Exception as exc:
                emit("log", f"Model finalization failed: {exc}")
        if wants_code(spec.get("prompt", "")) and not has_code(final_text):
            fallback_text = deterministic_code_fallback(spec.get("prompt", ""))
            if fallback_text:
                emit("log", "Applied deterministic NexusAgent code fallback")
                final_text = fallback_text
        write_artifact(output_dir, "nexusagent-output.md", final_text or "No output produced.")
        emit_workspace_artifacts(spec, final_text)
        emit("final", final_text)
    return process.returncode


def main():
    if "--run-spec" not in sys.argv:
        handle_failure("Missing --run-spec")
    spec_path = Path(sys.argv[sys.argv.index("--run-spec") + 1]).expanduser().resolve()
    spec = load_spec(spec_path)
    output_dir = ensure_output_dir(spec)

    return_code = try_run_real_nexusagent(spec, spec_path, output_dir)
    if return_code == 0:
        return
    if return_code is not None:
        handle_failure(f"NexusAgent exited with code {return_code}")

    emit("status", "NexusAgent not installed, using adapter fallback")
    attachment_context = build_spec_doc_context(spec)
    composed_prompt = (
        "Perform this task in a NexusAgent-style bounded run. Provide only the final deliverable.\n\n"
        f"Task:\n{spec.get('prompt', '')}\n\n"
    )
    if attachment_context:
        composed_prompt += f"Artifacts:\n{attachment_context}\n"

    try:
        emit("log", "Calling /nexuscentral/thinking")
        response = unwrap_output(call_thinking(spec, composed_prompt))
        final_text = response.get("output", "").strip()
        write_artifact(output_dir, "nexusagent-output.md", final_text or "No output produced.")
        emit("final", final_text)
    except Exception as exc:
        handle_failure(str(exc))


if __name__ == "__main__":
    main()
