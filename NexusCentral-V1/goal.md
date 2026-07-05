# Goal

Make `NexusCentral-V1` the active, truthful, release-ready NexusCentral app lane.

## Criteria

- Resolve the active app by files, not by folder name: `ARCHITECTURE.md` plus `Desktop/*.csproj` plus `Tests/*.csproj`.
- Keep the product architecture V1-native: `Desktop -> Core -> Features -> Integrations`.
- Keep `Lab + Workspaces` as the backed baseline slice.
- Remove or downgrade fake readiness before adding broader capability.
- Add a manifest-based app and package update shape before public release work.
- Keep public release versions in GitHub tags/releases and update manifests, not in `NexusCentral-V0/V1` folder names.
- Build every buildable version lane through GitHub Actions when app, package, update, or workflow files change.
- Deploy release artifacts to GitHub Releases from semver tags or manual release workflow runs.
- Validate source changes with build and test; validate UI changes with human-view proof where possible.

## Current Next Target

Tighten self-hosted orchestration proof: verify the Router selects the hosted harness route when model and harness state are ready, then keep improving human-view validation around setup, route smoke, and 100+ scenario sandbox evidence.
