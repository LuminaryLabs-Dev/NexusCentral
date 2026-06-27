# Step 08: Automations Foundation

## One Sentence

Add the Automations foundation for draft workflow editing and validation without fake execution.

## State Observes

- Automations UI is placeholder workflow list, ladder steps, run status, and output strip.
- No automation service exists under `Features/Automations`.
- Architecture excludes the old standalone Workflow page.
- Desired direction is workflow capability through Automations, not a restored Workflow tab.

## Exact Changes To Plan

- Add workflow draft model:
  - workflow id
  - name
  - steps
  - input prompt
  - output target
  - required harness/model capabilities
  - validation state
- Add workflow draft store.
- Add workflow validation service.
- Bind Automations UI to draft workflow state.
- Add ladder editor interactions:
  - add step
  - reorder step
  - duplicate step
  - delete step
  - edit prompt/input/output
- Keep Run disabled until execution queue and harness/model routing exist.

## Hero Controls / User-Facing Impact

- New automation draft.
- Edit ladder.
- Validate draft.

## Advanced Controls / Deferred

- Run selected.
- Stop run.
- Artifact output.
- YAML export.
- Run history.

## Validation

- Create draft workflow.
- Add/edit/reorder steps.
- Validation reports missing model/harness support clearly.
- Run remains disabled until backed.
- Build and tests pass.

## Done Criteria

- Automations is a real draft builder.
- It does not fake execution.
- Old standalone Workflow page remains excluded.
