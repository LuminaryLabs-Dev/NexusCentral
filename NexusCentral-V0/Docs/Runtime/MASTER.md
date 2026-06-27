# Runtime Module - MASTER

## Final Target State

- BitNet-first local inference runtime for supported 1-bit / ternary GGUF models.
- Stable NexusCentral JSON contract for chat, wrappers, workflows, CLI, and LAN routing.
- Explicit memory, thread, context, and loaded-instance admission before runtime launch.
- Clear rejection for unsupported GGUF and over-budget models.

## Key Modules

- `Runtime/bitnet_runtime.py`: command adapter around Microsoft `bitnet.cpp`.
- `Runtime/NexusCentral-BitNet-Runtime`: platform launcher copied into app bundles.
- `Desktop/Services/ModelCatalogService.cs`: compatibility and memory admission.
- `Desktop/Services/RuntimeService.cs`: BitNet process invocation and result normalization.

## Integration Contracts

- Runtime command shape:
  - `--model <path>`
  - `--n-predict <tokens>`
  - `--ctx-size <tokens>`
  - `--threads <count>`
  - `--max-memory-mb <budget>`
  - `--mode thinking|instruct`
  - `run-once --prompt <text> --json|--stream-json`
- Output remains the existing `RuntimeInvocationResult` JSON shape.

## Success Criteria

- Desktop builds without Cargo.
- BitNet-compatible models can be cataloged and admitted under budget.
- Over-budget models return `blocked_by_memory_budget`.
- Unsupported GGUF models remain visible but cannot be activated.
