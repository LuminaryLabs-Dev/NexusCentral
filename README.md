# NexusCentral

NexusCentral is a local control plane. The active stable checkpoint is the Avalonia `NexusCentral-V1` lane. The Electron cutover proof lives in `NexusCentral-Electron`.

## Current Direction

```text
NexusCentral-V1
|-- stable Avalonia checkpoint
|-- workflow contract: Lab / Router / Models / Review / Settings

NexusCentral-Electron
|-- new web-first desktop shell proof
|-- Lab coding loop: Review / Act / Audit
|-- coding harness with workspace-scoped tools
|-- desktop adapters for local model, harness, workspace, proof review, Rust sidecar
```

Do not retire `NexusCentral-V1` until the Electron lane proves local model calls, Conversation Harness execution, workspace browsing, validation proof review, Linux packaging, and Rust sidecar execution.

## Downloads

Public branch downloads are published by GitHub Actions to GitHub Pages:

- Downloads page: https://luminarylabs-dev.github.io/NexusCentral/
- Download index JSON: https://luminarylabs-dev.github.io/NexusCentral/downloads/index.json

Each pushed branch keeps its latest successful build outputs under:

```text
downloads/<branch>/latest/
```

## Build And Release Automation

- Branch build artifacts: `.github/workflows/nexuscentral-pages.yml`
- CI artifacts: `.github/workflows/nexuscentral-build.yml`
- GitHub Release artifacts: `.github/workflows/nexuscentral-release.yml`
- Target matrix: `updates/release-targets.json`

GitHub Pages must be configured to deploy from the `gh-pages` branch at `/`.
