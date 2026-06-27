#!/usr/bin/env python3
import sys
from pathlib import Path

from nexuscentral_wrapper_common import call_thinking, emit, ensure_output_dir, handle_failure, load_spec, unwrap_output, write_artifact


def main():
    if "--run-spec" not in sys.argv:
        handle_failure("Missing --run-spec")
    spec = load_spec(sys.argv[sys.argv.index("--run-spec") + 1])
    output_dir = ensure_output_dir(spec)
    prompt = spec.get("prompt", "").strip()
    memory_context = spec.get("memory_context", "").strip()
    if not prompt:
        handle_failure("Prompt is required")

    emit("status", "Preparing memory-augmented prompt")
    composed_prompt = prompt
    if memory_context:
        composed_prompt = (
            "Use the following memory context when it is relevant.\n\n"
            f"{memory_context}\n\n"
            "User request:\n"
            f"{prompt}"
        )

    try:
        emit("log", "Calling /nexuscentral/thinking")
        response = unwrap_output(call_thinking(spec, composed_prompt))
        final_text = response.get("output", "").strip()
        if memory_context:
            write_artifact(output_dir, "memory-notes.md", memory_context)
        emit("final", final_text)
    except Exception as exc:
        handle_failure(str(exc))


if __name__ == "__main__":
    main()
