# Step 09: Settings Foundation

## One Sentence

Add Settings persistence for paths, auth profiles, routing defaults, and theme/density.

## State Observes

- Settings UI is static.
- `Core/Config/AppPathSettings.cs` exists.
- No Settings view model or config editor is wired.
- Architecture needs config, path service, theme tokens, auth-profile references, and routing defaults.
- Provider secrets must not be stored directly in route targets.

## Exact Changes To Plan

- Add Settings models:
  - paths
  - auth profiles
  - routing defaults
  - theme/density
  - workspace defaults
- Add Settings service using local JSON persistence.
- Bind Settings UI groups to real config.
- Add auth profile reference model:
  - id
  - provider kind
  - display name
  - secret storage reference
- Never store raw provider keys in model endpoint or route target objects.
- Add validation for paths and auth profile references.

## Hero Controls / User-Facing Impact

- Edit app paths.
- Select theme/density.
- Manage auth profile references.
- Set routing defaults.

## Advanced Controls / Deferred

- Secret vault integration.
- Export/import settings.
- Reset all settings.
- Low-level diagnostics.

## Validation

- Change setting, restart app, confirm persistence.
- Invalid path shows validation.
- Endpoint/auth models use auth profile references, not raw secrets.
- Build and tests pass.

## Done Criteria

- Settings tab edits real persisted config.
- Provider and router systems can safely reference auth profiles.
- UI no longer shows disabled static settings blocks.
