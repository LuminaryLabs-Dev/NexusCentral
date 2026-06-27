# Step 02: Navigation Truth

## One Sentence

Fix shell navigation so Lab is visible, selected, and reachable while dead first-screen controls are hidden.

## State Observes

- The app opens to Lab content.
- The right primary Tabs rail lists Automations, Agents, Router, Models, and Settings.
- Lab is not shown in the right primary Tabs rail while Lab content is active.
- `MainWindow.axaml` binds right-tab selection to `SelectedSurface`, but the tab list excludes Lab.
- State reports call this the main visible regression.
- Disabled Lab controls remain visible: `Clear`, `Save`, `Filter`, `...`, and `Select model`.

## Exact Changes To Plan

- Add Lab to the primary right Tabs rail.
- Ensure `SelectedSurface` can select Lab and render Lab as active.
- Preserve the intended tab order:
  - Lab
  - Automations
  - Agents
  - Router
  - Models
  - Settings
- Hide or move disabled Lab header controls until backed:
  - `Clear`
  - `Save`
  - `Filter`
  - overflow `...`
  - `Select model`
- Keep only backed Lab controls visible:
  - create sandbox
  - open folder
  - refresh tree
  - plus upload
  - drag/drop upload
  - send canned local response if clearly labeled as local placeholder
- Add visual selected state for Lab in the right rail.

## Hero Controls / User-Facing Impact

- Lab should always be reachable from the right rail.
- Current active surface should always have visible selected navigation.
- First screen should show only backed controls.

## Advanced Controls / Deferred

- Do not add Dashboard.
- Do not add Web Lab as a tab.
- Do not expose Integrations/Rpc as a tab.
- Do not add model selection until Models foundation exists.

## Validation

- Build the app.
- Run the app.
- Capture screenshot proof showing Lab selected in the right rail.
- Click each right tab and return to Lab.
- Confirm no dead first-screen controls are visible in Lab.

## Done Criteria

- Lab appears in the primary rail.
- Lab is selected when Lab content is active.
- User can return to Lab after switching tabs.
- Disabled Lab controls no longer imply unsupported behavior.
