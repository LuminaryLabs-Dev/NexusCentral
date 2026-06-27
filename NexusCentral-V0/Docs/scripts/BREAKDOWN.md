# scripts Module - BREAKDOWN

## Current State

- `verify-publish-clean.sh` checks and asserts that the publish output does not contain:
  - writable `models` directory
  - writable `logs` directory
  - state artifacts (`*.gguf`, `*.safetensors`, `catalog.json`, `library-state.json`)

## Bug / Interaction Audit

- Script may fail on paths with spaces if not robust (shell quoting risk).
- No test harness for the script itself.
- Requires `binary` file artifacts being present if in a different CWD.

## Cross-module Interaction Matrix

- scripts -> Launcher: verify packaging hygiene.
- scripts -> Docs: validation that docs reflect publish contract.
- scripts -> Tests: can be run as precheck before dotnet test and release.

---
