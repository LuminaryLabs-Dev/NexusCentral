# NexusCentral

NexusCentral is a local desktop control plane built from the active `NexusCentral-V1` app lane.

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
