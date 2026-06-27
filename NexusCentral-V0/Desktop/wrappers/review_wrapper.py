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
    prompt = spec.get("prompt", "").strip() or "Review the supplied artifacts."
    artifact_inputs = spec.get("artifact_inputs") or []
    if not artifact_inputs:
        handle_failure("review_wrapper requires at least one artifact input")

    emit("status", "Preparing review context")
    attachment_context = read_artifact_texts(artifact_inputs, limit=5)
    composed_prompt = (
        "Review the following materials and provide a concise report with findings and recommendations.\n\n"
        f"Request:\n{prompt}\n\n"
        f"Materials:\n{attachment_context}\n"
    )

    try:
        emit("log", "Calling /nexuscentral/thinking")
        response = unwrap_output(call_thinking(spec, composed_prompt))
        final_text = response.get("output", "").strip()
        write_artifact(output_dir, "review-report.md", final_text or "No review output produced.")
        emit("final", final_text)
    except Exception as exc:
        handle_failure(str(exc))


if __name__ == "__main__":
    main()
