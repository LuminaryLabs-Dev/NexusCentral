# Runtime Module - BREAKDOWN

## Current State

- `Runtime/bitnet_runtime.py` adapts Microsoft `bitnet.cpp` to the NexusCentral JSON runtime contract.
- `Runtime/NexusCentral-BitNet-Runtime` and `.cmd` are platform launchers.
- Desktop copies adapter files during build/publish instead of compiling Rust.
- BitNet source is external via `NEXUSCENTRAL_BITNET_ROOT` or `Runtime/BitNet`.

## Bug / Interaction Audit

- Runtime cannot execute until Microsoft BitNet is installed or bundled.
- Stream mode currently emits a final normalized chunk rather than true token-by-token BitNet streaming.
- Compatibility detection is filename/path/repo based and should later be hardened with GGUF metadata parsing.

## Cross-module Interaction Matrix

- Runtime -> Desktop: process stdout parsing and status updates via `RuntimeService`.
- Runtime -> Launcher: adapter files copied into `runtime/`.
- Runtime -> Models: admission metadata comes from `ModelCatalogService`.

---
