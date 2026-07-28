# Workflow Gap Seeding Follow-Up

Archived: `2026-03-25`
Reason: superseded by the consolidated reconciliation report after the workflow inventory was cleaned and re-bucketed.

Date: 2026-03-25
Workspace Root: NexusCentral-Builder
Author: Codex

Note: the workflow inventory shifted during this run. The reviewed file list below reflects the YAMLs and support files observed during inspection, including a few items that were later moved between lifecycle buckets or removed before close.

## Existing YAML And Support Material Reviewed

Workflow YAML files reviewed:

- `automations/active/workspace_inventory.yaml`
- `automations/active/initiative_plan.yaml`
- `automations/active/workflow_review_gate.yaml`
- `automations/experimental/docs_reconciliation.yaml`
- `automations/experimental/example_usage_seed.yaml`
- `automations/experimental/goal_reconciliation.yaml`
- `automations/experimental/project_setup_brief.yaml`
- `automations/experimental/proposal_backlog_triage.yaml`
- `automations/experimental/state_last_run_seed.yaml`
- `automations/experimental/workflow_catalog_refresh.yaml`
- `automations/experimental/workflow_gap_seeding.yaml`
- `automations/experimental/workflow_lifecycle_review.yaml`
- `automations/experimental/workflow_validation_review.yaml`
- `automations/experimental/workspace_delta_scan.yaml`
- `automations/disabled/state_last_run_contract.yaml`
- `templates/workflow_templates/micro_automation_template.yaml`
- `templates/workflow_templates/review_reconciliation_template.yaml`

Workflow docs and examples reviewed:

- `automations/README.md`
- `docs/conventions/workspace-contract.md`
- `docs/conventions/automation-lifecycle.md`
- `docs/conventions/workflow-authoring-contract.md`
- `docs/conventions/mock-workflow-yaml-pattern-guide.md`
- `docs/usage/workflow-seeding-overview.md`
- `docs/usage/workflow-seeding-report-2026-03-25.md`
- `docs/usage/workflow-gap-seeding-report-2026-03-25.md`
- `docs/usage/workspace-normalization-followup-2026-03-25.md`
- `docs/examples/experimental-workflow-composition.md`
- `docs/examples/mock-workflow-yaml-patterns.md`
- `docs/examples/workflow-authoring-composition.md`

## Patterns Confirmed

- The current repo uses a stable `workflow_spec` YAML shape with explicit `summary.offloads`, `inputs`, `reads`, `writes`, `steps`, `validation`, `handoff`, and `notes`.
- The workflow surface is now split across `active`, `experimental`, and `disabled`, and the current style supports specification-only workflows in any lifecycle bucket when the placement rationale is documented.
- Review-oriented workflows now form their own cluster. The repo already contains validation, lifecycle, gate, and catalog-refresh passes, plus a reusable review template.
- The main risk is no longer missing basic workflow categories. The newer risk is catalog sprawl: adjacent workflow scopes and stale references can drift faster than the docs are refreshed.

## Follow-Up Gaps Found

- Gap: overlap between nearby workflows is not yet reconciled.
  Repeated work: manually comparing adjacent workflows such as `workflow_gap_seeding`, `proposal_backlog_triage`, `workflow_validation_review`, `workflow_review_gate`, `workflow_lifecycle_review`, and `workflow_catalog_refresh` to decide whether they should stay separate.
  Why it matters: without an overlap pass, the experimental catalog can grow faster than reviewers can understand why each workflow exists.

- Gap: workflow handoff references can drift after renames, promotions, and newly seeded workflow batches.
  Repeated work: searching for stale ids in `handoff` blocks, examples, and usage docs.
  Why it matters: broken or outdated references make the workflow catalog harder to navigate and reduce confidence in the documentation layer.

## New Mock Workflow Files Created

- [`workflow_overlap_reconciliation.yaml`](../automations/experimental/workflow_overlap_reconciliation.yaml)
  Purpose: compare nearby workflow specs and recommend keep, merge-review, rename, or narrow decisions.
  Why this unit is separate: overlap analysis is a distinct review problem that current validation and lifecycle workflows do not directly solve.

- [`workflow_handoff_reconciliation.yaml`](../automations/experimental/workflow_handoff_reconciliation.yaml)
  Purpose: scan workflow ids referenced across YAML handoffs, examples, and usage notes, then queue targeted consistency fixes.
  Why this unit is separate: reference consistency is a narrower maintenance task than full catalog refresh or docs reconciliation.

## Supporting Documentation Added

- [`workflow-maintenance-loop.md`](../docs/examples/workflow-maintenance-loop.md)
  Why it was added: the existing examples focused on initial authoring and setup; this adds a maintenance-oriented composition path for overlap and reference cleanup.

- [`workflow-gap-seeding-followup-2026-03-25.md`](./workflow-gap-seeding-followup-2026-03-25.md)
  Why it was added: the first seeding report already covered the larger expansion wave, so this follow-up report isolates the narrower remaining gaps discovered after the workflow inventory grew.

## Review Next

- Review whether `workflow_overlap_reconciliation` should run before any more review-oriented workflows are added to `automations/experimental/`.
- Review whether `workflow_handoff_reconciliation` should refresh only current guidance or also flag historical docs that intentionally preserve older workflow names.
- Review stale references to `workspace_explore`, `planning_scaffold`, `project_setup`, and `docs_cleanup` before they spread into more examples or reports.
