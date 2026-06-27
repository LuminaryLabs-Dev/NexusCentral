#!/usr/bin/env python3
import sys

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


def main():
    if "--run-spec" not in sys.argv:
        handle_failure("Missing --run-spec")
    spec = load_spec(sys.argv[sys.argv.index("--run-spec") + 1])
    output_dir = ensure_output_dir(spec)
    prompt = spec.get("prompt", "").strip()
    artifact_inputs = spec.get("artifact_inputs") or []
    if not prompt:
        handle_failure("Prompt is required")

    emit("status", "Preparing artifact builder context")
    attachment_context = read_artifact_texts(artifact_inputs)
    composed_prompt = (
        "Produce a concise implementation artifact response. "
        "Return the main result as plain text suitable for saving to markdown.\n\n"
        f"Request:\n{prompt}\n\n"
    )
    if attachment_context:
        composed_prompt += f"Reference materials:\n{attachment_context}\n\n"

    try:
        emit("log", "Calling /nexuscentral/thinking")
        response = unwrap_output(call_thinking(spec, composed_prompt))
        final_text = response.get("output", "").strip()
        write_artifact(output_dir, "artifact-output.md", final_text or "No content produced.")
        write_artifact(output_dir, "artifact-output.json", '{"status":"ok"}\n')
        emit("final", final_text)
    except Exception as exc:
        handle_failure(str(exc))


if __name__ == "__main__":
    main()
