# Workflow Authoring Composition Example

Date: 2026-03-25

This example shows how the newer authoring and governance workflows can stay modular while expanding the Nexus Central workflow inventory.

## Example Scenario

Goal: identify missing workflow coverage, seed new mock workflow definitions, review their readiness, and document how they fit together.

## Recommended Flow

1. Run `workspace_inventory` to inventory the current repo surface and capture repeated manual work.
2. Run `proposal_backlog_triage` if improvements, lessons, or usage notes already contain multiple deferred workflow ideas.
3. Run `workflow_gap_seeding` to convert the highest-value gaps into mock YAML workflow specs and a seeding report.
4. Run `workflow_review_gate` to review the new and existing workflows against lifecycle and validation guidance.
5. Run `example_usage_seed` to update scenario-driven examples and clarify the current intended workflow sequence.
6. Re-enable `state_last_run_contract` only when the workflow set is large enough that shared last-run markers need a documented shape.

## Expected Artifacts By Stage

- Exploration stage: workspace inventory, gap report, and candidate workflow notes.
- Triage stage: prioritized micro-automation backlog and a short triage summary.
- Seeding stage: mock workflow YAML files, workflow gap report, and composition updates.
- Review stage: lifecycle recommendations and a promotion or refinement queue.
- Example stage: scenario-driven examples and short audience/boundary notes.
- State-contract stage: a proposed marker contract and a mock example marker after the disabled workflow returns to active review.

## Why This Stays Modular

- Exploration remains observational.
- Triage remains prioritization rather than authoring.
- Seeding remains spec creation rather than runtime implementation.
- Review remains governance rather than mutation.
- Example writing remains explanatory rather than normative.
- State-contract work remains disabled documentation until real writers exist.

Keeping those boundaries visible makes it easier to promote only the workflows that are ready while leaving speculative support work in `automations/experimental/`.
