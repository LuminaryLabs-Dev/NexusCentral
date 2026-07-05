# Goal

Make `NexusCentral-V1` the active, truthful, release-ready NexusCentral app lane, with public app versions handled by GitHub releases/manifests instead of repo folder names.

## Folder

`/Users/crimsonwheeler/Documents/GitHub/NexusCentral`

## Canonical Build Root

`/Users/crimsonwheeler/Documents/GitHub/NexusCentral/NexusCentral-V1`

## Detected Stack

- Avalonia desktop app on .NET.
- xUnit test project.
- File-backed local state through JSON services.

## Current State

- Build and tests are green in the latest evidence.
- `Lab + Workspaces` is the backed baseline slice.
- Router is the highest fake-readiness gap.
- Models, Agents, Automations, Settings, provider adapters, and RPC are mostly static or placeholder-backed.
- Active lane control docs were missing before this pass.

## Stack Plan

1. Normalize active lane docs and version wording.
2. Fix navigation truth so Lab is visible and selected.
3. Finish Lab workspace ownership, tree interaction, and file preview.
4. Make Router state truthful before adding routing power.
5. Bind shell/status state to real services or explicit inactive states.
6. Add Models endpoint registry and health checks.
7. Add Agents manifest and permission foundations.
8. Add Automations draft and validation foundations.
9. Add Settings persistence.
10. Add release hardening, GitHub Actions build/release workflows, branch downloads through GitHub Pages, and manifest-based update checks.

## Next Sequential Step

Run Step 01: state and repo normalization.

Exact target:

- Keep `NexusCentral-V1` as the active lane.
- Replace stale hard-coded `NexusCentral-V1/state` guidance with latest-active-version wording.
- Keep generated outputs ignored.
- Treat `NexusCentral-V0/V1` as repo lanes, not public product versions.
- Keep public versions in GitHub releases, semver tags, and update manifests.
- Keep release target changes centralized in `updates/release-targets.json`.

## Execution Handoff

Use `do-it` only after the doc normalization pass is accepted or explicitly requested.

## Validation Handoff

- `dotnet build /Users/crimsonwheeler/Documents/GitHub/NexusCentral/NexusCentral-V1/Desktop/NexusCentral-V1.csproj`
- `dotnet test /Users/crimsonwheeler/Documents/GitHub/NexusCentral/NexusCentral-V1/Tests/NexusCentral-V1.Tests.csproj`
- GitHub Actions build matrix from `.github/workflows/nexuscentral-build.yml`
- GitHub Release artifact deployment from `.github/workflows/nexuscentral-release.yml`
- Branch download page deployment from `.github/workflows/nexuscentral-pages.yml`
- Human-view proof for UI changes.

## Blockers

- Decide whether to rename `Desktop/NexusCentral-V1.csproj` before release packaging.
- Decide first public release version and update channel names.
