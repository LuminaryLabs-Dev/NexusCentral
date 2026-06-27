# Launcher Module - BREAKDOWN

## Current State

- `Launcher/NexusCentral-Launcher.csproj` includes bundle target `BundleProduct`.
- Supports conditions:
  - `osx-arm64`, `osx-x64`, `win-x64`.
  - copies the BitNet runtime adapter files.
- Performs publish + runtime adapter copy.
- Verifies no models/logs folder and no state artifacts in publish output.

## Bug / Interaction Audit

- No explicit Linux build target; missing `linux-x64` / `linux-aarch64`.
- No support for auto version injection from git or semver.
- Error check for published artifacts may falsely fail if output includes valid non-state files named similarly.
- BitNet itself is external; bundle readiness depends on `NEXUSCENTRAL_BITNET_ROOT` or a bundled `Runtime/BitNet`.

## Cross-module Interaction Matrix

- Launcher -> Desktop: published app path and binary references.
- Launcher -> Runtime: runtime adapter file copy.
- Launcher -> Docs: shipping requires docs updates for bundle policies.

---
