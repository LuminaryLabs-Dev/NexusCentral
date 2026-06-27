# Runtime Module

## Purpose

`Runtime` is now a NexusCentral JSON adapter for Microsoft `bitnet.cpp`.
The goal is local 1-bit / ternary LLM inference with explicit memory admission instead of a Rust `llama-cpp-2` sidecar.

## Project structure

- `Runtime/NexusCentral-BitNet-Runtime`: macOS/Linux launcher.
- `Runtime/NexusCentral-BitNet-Runtime.cmd`: Windows launcher.
- `Runtime/bitnet_runtime.py`: stable NexusCentral JSON contract adapter.
- `Runtime/README.md`: local BitNet setup notes.

## Runtime policy

- Supported model family: BitNet b1.58 / 1.58-bit GGUF-compatible models.
- Unsupported general GGUF models are rejected before activation.
- Each model records `estimated_memory_mb`, `max_memory_mb`, `max_threads`, `context_size`, `quant_type`, and `admission_status`.
- Oversized models remain catalogable but are blocked with `blocked_by_memory_budget`.

## Workflow

1. `RuntimeService` launches `runtime/NexusCentral-BitNet-Runtime`.
2. The adapter validates model path and memory budget.
3. The adapter invokes `run_inference.py` from `NEXUSCENTRAL_BITNET_ROOT` or `Runtime/BitNet`.
4. The adapter normalizes BitNet output into the existing NexusCentral JSON result.

## Integration

- Desktop, CLI, wrappers, workflows, and LAN routing continue using the existing NexusCentral runtime endpoint contract.
- Desktop build/publish copies the runtime adapter files instead of running Cargo.
- Model admission now lives in `ModelCatalogService` and readiness still flows through `ModelLibraryService`.
