# Provider Router And Models Split

Status: active

## Purpose

Keep provider routing, local model lifecycle, and coding harness execution as separate service domains in NexusCentral Electron.

## Criteria

- Router profiles support hosted providers and OpenAI-compatible local endpoints.
- Provider credentials are encrypted outside the provider registry.
- Models owns Hugging Face search and LM Studio lifecycle calls.
- Publishing a local model updates Router without transferring model lifecycle ownership.
- Validation remains deferred to `docs/provider-router-models-validation.md`.
