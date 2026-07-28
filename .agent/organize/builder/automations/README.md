# Automation Inventory

## Current State

As of `2026-03-26`, the workspace contains a reviewed planning-layer workflow set with three active governance and planning specs, twenty-two experimental follow-on specs, and three intentionally disabled deferred specs.

Current inventory:

- `active/`: `workspace_inventory`, `initiative_plan`, and `workflow_review_gate`
- `experimental/`: twenty-two workflows grouped into three families
- planning, intake, and coverage audit: `request_intake_brief`, `project_setup_brief`, `goal_reconciliation`, `proposal_backlog_triage`, `workflow_gap_seeding`, `workflow_goal_coverage_audit`, `example_usage_seed`
- catalog maintenance and consistency review: `docs_reconciliation`, `docs_cleanup_threshold_seed`, `workspace_delta_scan`, `workflow_template_gap_review`, `workflow_placeholder_audit`, `workflow_catalog_refresh`, `workflow_overlap_reconciliation`, `workflow_handoff_reconciliation`
- goal-linked domain review and validation enablement: `factory_export_readiness_review`, `human_view_evidence_brief`, `validation_infrastructure_readiness_review`, `portfolio_signal_refresh`, `release_evidence_packet_seed`, `tool_surface_contract_audit`, `workspace_hygiene_triage`
- `disabled/`: `state_last_run_contract`, `workflow_validation_review`, and `workflow_lifecycle_review`

All workflow specs remain `specification_only`, but the active group is now clear enough to serve as the default reviewed entry points for later authoring and reconciliation work.

## How To Use This Folder

- Put only strong, review-ready workflow candidates in `active/`.
- Put incomplete, uncertain, or trial candidates in `experimental/`.
- Put intentionally inactive but still relevant items in `disabled/`.
- Move fully retired or superseded items to [`archive/`](../archive/), not `disabled/`.

## Before Adding Or Promoting Workflows

- Read [`docs/conventions/automation-lifecycle.md`](../docs/conventions/automation-lifecycle.md).
- Read [`docs/conventions/tool-lifecycle.md`](../docs/conventions/tool-lifecycle.md) if the workflow touches tool-family planning.
- Read [`docs/usage/reconciliation-report-2026-03-26.md`](../docs/usage/reconciliation-report-2026-03-26.md).
- Check [`improvements/proposals/initial-workflow-gap-backlog.md`](../improvements/proposals/initial-workflow-gap-backlog.md) for still-missing support material.
- Check [`goals/workflow_targets/`](../goals/workflow_targets/) and [`proposals/README.md`](../proposals/README.md) before seeding a new family.

## Review Notes

- `workspace_inventory` is active because it is the clearest non-destructive entry point for understanding the repo before any later workflow runs.
- `initiative_plan` is active because it turns broad goals into reviewable phases without depending on project-specific conventions.
- `workflow_review_gate` is active because lifecycle review is now a stable recurring need with clear inputs, outputs, and boundaries.
- `project_setup_brief` remains experimental because the boundary between setup-specific briefs and normal initiative planning is still situational.
- `factory_export_readiness_review`, `human_view_evidence_brief`, `portfolio_signal_refresh`, `release_evidence_packet_seed`, `tool_surface_contract_audit`, and `workspace_hygiene_triage` remain experimental because they clearly support current goals but still need proof that their domain-specific review lanes deserve promotion over remaining as goal-linked proposal-stage workflows.
- `docs_reconciliation` remains experimental because the repo still needs stronger examples for merge, archive, and retirement thresholds.
- `docs_cleanup_threshold_seed` remains experimental because it addresses a real blocker in documentation cleanup but still needs repeated review use before it becomes baseline guidance.
- `proposal_backlog_triage` remains experimental because its backlog-shaping role is useful but still adjacent to seeding and planning.
- `workflow_gap_seeding` remains experimental because it is broader than the other support specs and still needs tighter evidence around its authoring boundaries.
- `workflow_goal_coverage_audit` remains experimental because it is useful as a whole-library anchor, but it still needs repeated use before it becomes a default precursor to every seeding pass.
- `example_usage_seed` remains experimental because it supports the library but should follow inventory changes rather than drive them.
- `request_intake_brief` remains experimental because its compression role is useful for large asks, but its boundary against `goal_reconciliation` and `initiative_plan` still needs proof.
- `workspace_delta_scan`, `goal_reconciliation`, and `workflow_catalog_refresh` remain experimental because they are credible maintenance workflows but still need more repeated use before promotion.
- `workflow_template_gap_review` remains experimental because the template-gap problem is credible, but promotion should depend on repeated template-reuse decisions rather than one seeding pass.
- `workflow_placeholder_audit` remains experimental because its consistency-review role is useful but still adjacent to `workflow_handoff_reconciliation` and `workflow_review_gate`.
- `workflow_overlap_reconciliation` and `workflow_handoff_reconciliation` remain experimental because they are useful maintenance passes but were seeded from follow-up gaps rather than repeated validated runs.
- `validation_infrastructure_readiness_review` remains experimental because it cleanly extends the human-view and release-evidence family, but it still needs evidence that shared validation plumbing should stay separate from per-project evidence briefs.
- `state_last_run_contract` is disabled because the repo does not yet have enough real shared-state writers to justify a live contract workflow.
- `workflow_validation_review` and `workflow_lifecycle_review` are disabled because the active `workflow_review_gate` currently covers the clearer merged review path.
