# NexusCentral V1 Memory

## Purpose

`NexusCentral-V1` is the active app lane for the V3-native NexusCentral rebuild. The goal is a truthful local desktop control plane for Lab, Workspaces, Router, Models, Agents, Automations, and Settings.

## Architecture Shape

- `Desktop/` is the Avalonia host.
- `Core/` owns shared configuration, logging, state, and execution contracts.
- `Features/` owns user-facing product modules.
- `Integrations/Rpc/` is infrastructure and must not become primary navigation.
- `Integrations/Providers/` is the future provider adapter boundary.

## Major Conventions

- Resolve the active lane by required files, not by folder number.
- Treat folder names like `NexusCentral-V1` as repo lanes, not public product versions.
- Do not copy V2 source or view-model shapes into this lane.
- Use service-backed state or explicit inactive state; do not show static ready/available claims.
- Keep hero UI controls limited to backed primary workflow actions.
- Keep advanced or future actions hidden, disabled with clear inactive state, or moved out of first-screen flow.

## Update Pipeline Direction

- Use GitHub releases and semver tags for public app versions.
- Use a small update index such as `updates/stable.json` and `updates/beta.json`.
- Use package manifests for internal modules with `id`, `version`, `compatibility`, `source`, `sha256`, and `capabilities`.
- Use a local installed lockfile so the app can tell what versions are installed.
- Verify hash/signature before applying updates and keep rollback possible.

## Build And Deployment Pipeline

- `updates/release-targets.json` owns the target matrix.
- `.github/workflows/nexuscentral-build.yml` builds every discovered `NexusCentral-V*` lane that contains `ARCHITECTURE.md`, `Desktop/*.csproj`, and `Tests/*.csproj`.
- `.github/workflows/nexuscentral-release.yml` publishes the same target matrix to GitHub Releases on `v*` tags or manual release runs.
- `.github/workflows/nexuscentral-pages.yml` publishes the latest successful build outputs for each pushed branch to the `gh-pages` branch.
- The public downloads page is expected at `https://luminarylabs-dev.github.io/NexusCentral/` after GitHub Pages is configured to deploy from `gh-pages` `/`.
- Generated local publish output belongs under `artifacts/` and must stay out of git.
- Current measured self-contained zipped build sizes for `NexusCentral-V1` are about:
  - `osx-arm64`: 45 MB zipped, 113 MB expanded.
  - `osx-x64`: 50 MB zipped, 111 MB expanded.
  - `linux-x64`: 49 MB zipped, 108 MB expanded.
  - `win-x64`: 76 MB zipped, 211 MB expanded.
