# Step 03: Lab Workspace Completion

## One Sentence

Finish Lab + Workspaces with workspace-owned uploads, expandable tree browsing, and file preview.

## State Observes

- Lab + Workspaces are the only service-backed slice.
- Workspace registry, sandbox creation, open/select, generated-directory filtering, path safety, and local JSON persistence are backed.
- Chat uploads are owned by the chat UI boundary.
- Uploaded files are currently recorded as external source paths.
- Visible workspace tree is flattened text.
- `WorkspaceSession.ExpandedPaths` exists but has no visible expand/collapse interaction.
- There is no real selected file preview/read path in the Lab UI.

## Exact Changes To Plan

- Add workspace-owned upload import:
  - plus upload and drag/drop copy files into a workspace-owned upload/import folder.
  - attachment records reference workspace-relative paths.
  - external source path can be retained only as metadata if safe.
- Add import conflict behavior:
  - preserve original filename if free.
  - suffix duplicate names.
  - reject unsafe names.
- Add expandable/collapsible tree:
  - folders can expand/collapse.
  - expanded state persists through `WorkspaceSession.ExpandedPaths`.
  - generated folders stay filtered.
- Add file selection:
  - selected file path updates status/footer.
  - selected file can be previewed read-only.
  - binary or oversized files show a safe placeholder.
- Keep file additions out of the tree:
  - no row-level add/attach/write/delete controls.
  - upload remains chat-owned.

## Hero Controls / User-Facing Impact

- Create sandbox.
- Open folder.
- Refresh tree.
- Expand/collapse folders.
- Select file to preview.
- Upload via plus or drag/drop into chat.

## Advanced Controls / Deferred

- Rename/delete/write file actions should remain deferred unless explicitly added as advanced workspace actions.
- No model execution yet.
- No workflow execution yet.

## Validation

- Test upload import stays inside workspace.
- Test duplicate upload names.
- Test path traversal rejection.
- Test symlink/reparse escape rejection still passes.
- Manual screenshot shows expandable tree and file preview.
- Build and tests pass.

## Done Criteria

- Uploaded files become workspace-owned.
- Tree interaction matches persisted expansion state.
- File preview reads only bounded workspace files.
- Lab remains compact and chat-dominant.
