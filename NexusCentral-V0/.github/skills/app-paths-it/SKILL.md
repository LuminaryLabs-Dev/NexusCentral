---
name: app-paths-it
description: "Use when resolving app paths, migrating state locations, or managing bundle and user data directories."
---

# App Paths It

## When to Use
- Resolve filesystem locations.
- Migrate state from legacy directories.
- Keep bundle and writable data separate.

## Procedure
1. Use `AppPathsService` for all path discovery.
2. Store mutable state under the state root, not the bundle root.
3. Preserve migration metadata when moving legacy files.

## References
- [AppPathsService.cs](../../../Desktop/Services/AppPathsService.cs)
- [WorkspaceService.cs](../../../Desktop/Services/WorkspaceService.cs)
- [AppStateService.cs](../../../Desktop/Services/AppStateService.cs)
- [WorkspaceModels.cs](../../../Desktop/Models/WorkspaceModels.cs)
