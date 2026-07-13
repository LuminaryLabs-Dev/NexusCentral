# Avalonia V1 Checkpoint

## Status

`NexusCentral-V1` is the current stable reference lane. It proves the workflow-first contract and the service-backed local model, harness, router, workspace, and validation shape.

## Frozen Workflow Contract

```text
Lab -> Setup -> Review -> Settings
```

Do not make large new UI investments in Avalonia unless they protect the checkpoint. New UI experimentation should happen in `NexusCentral-Electron`.

## Cutover Rule

Avalonia can be retired only after Electron proves:

- macOS startup
- Linux package target
- LM Studio probe
- Conversation Harness turn
- workspace file browsing
- validation proof review
- Rust sidecar execution
