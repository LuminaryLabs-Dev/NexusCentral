# Design DNA

## Purpose

NexusCentral should feel like a dense desktop control plane, not a marketing app. The first screen should expose only the controls needed for the current workflow.

## Hero Controls

- Lab: create sandbox, open folder, refresh tree, upload, send.
- Workspaces: select workspace, browse tree, preview file.
- Router: add/select profile, view real inactive/available state.
- Models: add endpoint, probe health, select active healthy endpoint.
- Agents: view harnesses, inspect permissions, validate manifest.
- Automations: create draft, edit ladder, validate draft.
- Settings: edit paths, theme/density, auth profile references, routing defaults.

## Advanced Controls

- Runtime execution controls.
- Remote peer routing.
- Provider secrets and vault setup.
- Workflow run history.
- Export/import settings.
- Diagnostics and source scans.

## Interaction Rules

- First-screen controls must be backed by real behavior.
- Planned controls belong in explicit inactive state or advanced groups.
- Status labels must come from service state or say inactive.
- Do not show ready, available, capacity, queue, or fallback claims without a state owner.
