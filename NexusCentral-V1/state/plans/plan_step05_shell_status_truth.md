# Step 05: Shell Status Truth

## One Sentence

Bind shell status, console, queue, route, model, and session panels to real state or explicit inactive state.

## State Observes

- Console message text is partly live.
- Console timestamp, level, and source cells are fixed/static.
- Pause and Clear controls are visible but disabled/static.
- Right status rail contains Workspace, Queue, Route, Model, and Session sections.
- Workspace status can be backed from WorkspaceService.
- Queue, Route, Model, and Session statuses are mostly static/inactive strings.

## Exact Changes To Plan

- Add a shell status adapter/view model.
- Bind Workspace status to active workspace state:
  - workspace name
  - path
  - file count
  - selected path
- Bind Console rows to `IAppLogService`:
  - timestamp
  - level
  - source
  - message
- Make Clear active only if logs can be cleared.
- Make Pause active only if log streaming can actually pause.
- Bind Queue to real execution queue when available; otherwise show explicit inactive.
- Bind Route to Router service when Step 04 is complete.
- Bind Model to Models service when Step 06 is complete.
- Bind Session to Lab conversation state:
  - active session id
  - message count
  - duration if tracked

## Hero Controls / User-Facing Impact

- Console should show real recent events.
- Status rail should be trusted at a glance.
- Unavailable systems should say inactive, not ready.

## Advanced Controls / Deferred

- Log filtering can wait.
- Export logs can wait.
- Deep diagnostics can live under future overflow controls.

## Validation

- Trigger workspace selection and see status update.
- Send a Lab message and see console/session status update.
- Confirm queue/route/model remain inactive until backed.
- Build and tests pass.

## Done Criteria

- No static fake status values remain in the shell.
- Status rail reflects real service state or explicit inactive state.
- Console rows are backed by actual logs.
