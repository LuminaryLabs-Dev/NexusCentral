# NexusCentral Triceratops Documentation

This `Docs` folder is a top-level reference hub for the NexusCentral product components. Each sibling folder corresponds to one of the top-level modules in the repository.

## Contents

- `Desktop/`: Avalonia desktop application (main UI and orchestration)
- `Interface/`: Python command-line interface (nci) domains and automation surface
- `Launcher/`: .NET bundle/publish orchestrator that builds Desktop + Runtime
- `Runtime/`: BitNet runtime adapter (1-bit / ternary model inference + prompt execution)
- `Web/`: web host in backend + frontend layers
- `Tests/`: automated tests and test runners
- `scripts/`: repo-level scripts (publish/clean validation)

## Architecture overview

1. Desktop app is the user-facing host shell. It starts as Avalonia, resolves services, loads viewmodels and controls UI flow.
2. Runtime engine is a BitNet adapter launched by Desktop/Launcher. It handles memory-admitted 1-bit model generation and normalizes output into the existing runtime response contract.
3. Interface module provides CLI commands for developers and automation, with domains like `models`, `ui`, `workflows`, and `nexuscentral`.
4. Launcher project creates self-contained bundles by publishing Desktop and building Runtime, copying runtime binaries, and applying packaging rules.
5. Web host provides a separate web UI surface; Desktop exposes an internal local IntegrationServer API (port 3005) used by WebLab integration and external orchestrators.
6. Tests cover service-level behavior (AppPathsService etc.) and should run from the `Tests` project.
7. Scripts are utility checks for release quality.

## Integration flow

- `Desktop` starts `IntegrationServer` on port 3005 and optionally `NexusWebLabService` (web-hosted frontend + backend).
- `Desktop` uses `RuntimeService` to invoke `runtime/NexusCentral-BitNet-Runtime`.
- `Desktop` and `Interface` share behavior through the same underlying `agents`, `workflows`, and `model` schemas in `Desktop/Models` and `config/` directories.
- `Launcher` ensures the packed product composition includes correct `Runtime` artifact and asset whitelisting.

## How to use this docs set

1. Inspect a module-specific README first.
2. Check cross-links in each module for integration details.
3. Keep this root README updated when adding new top-level modules.
