# Automation Lifecycle

## Purpose

This document defines how workflow material should be classified inside `automations/` so the inventory stays intentional, readable, and aligned with project goals and workflow families.

## Bucket Definitions

- `automations/active/`: strong workflow candidates that are understandable, goal-aligned, and ready for routine use or final review
- `automations/experimental/`: incomplete, speculative, or unproven workflow candidates that still need refinement
- `automations/disabled/`: intentionally inactive items kept for reference because they may return or still inform current work
- `archive/`: retired, superseded, or historical material that should no longer be considered part of the live workflow set

## Movement Rules

- Move a workflow into `active/` only when its purpose, inputs, outputs, and usage are clear enough that another reviewer can understand why it exists.
- Keep a workflow in `experimental/` if its scope is still changing, it duplicates another candidate, or its value is not yet proven.
- Move a workflow into `disabled/` when it is intentionally paused or deferred but still relevant enough to retain near the live inventory.
- Move a workflow into `archive/` when it has been replaced, rejected, or preserved only for historical context.

## Minimum Readiness For Active

A workflow candidate should not enter `active/` unless it has:

- a clear goal statement tied to the project
- an understandable lifecycle status
- enough documentation to distinguish it from nearby candidates
- no unresolved ambiguity about whether it belongs in `active/` versus `experimental/`

`active/` does not require runtime implementation. A specification-only workflow can be active when it is the strongest reviewed planning-layer option and its remaining gaps are documented.

## Naming And Documentation Expectations

- Prefer descriptive names that explain the workflow outcome, not just the mechanism.
- Keep historical notes and review summaries in `docs/usage/` rather than mixing them into workflow definitions.
- Record recurring guidance in `docs/conventions/`, not in individual workflow folders.
- Capture unresolved gaps in `improvements/proposals/` so missing support work is visible.

## Current Reviewed State

As of `2026-03-26`, the workspace contains:

- active: `workspace_inventory`, `initiative_plan`, `workflow_review_gate`
- experimental planning, intake, and coverage audit: `request_intake_brief`, `project_setup_brief`, `goal_reconciliation`, `proposal_backlog_triage`, `workflow_gap_seeding`, `workflow_goal_coverage_audit`, `example_usage_seed`
- experimental catalog maintenance and consistency review: `docs_reconciliation`, `docs_cleanup_threshold_seed`, `workspace_delta_scan`, `workflow_template_gap_review`, `workflow_placeholder_audit`, `workflow_catalog_refresh`, `workflow_overlap_reconciliation`, `workflow_handoff_reconciliation`
- experimental goal-linked domain review and validation enablement: `factory_export_readiness_review`, `human_view_evidence_brief`, `validation_infrastructure_readiness_review`, `portfolio_signal_refresh`, `release_evidence_packet_seed`, `tool_surface_contract_audit`, `workspace_hygiene_triage`
- disabled: `state_last_run_contract`, `workflow_validation_review`, `workflow_lifecycle_review`
- archived workflow-support material: `workspace-normalization-2026-03-25.md`, `workspace-normalization-followup-2026-03-25.md`, `workspace-bootstrap-verification-2026-03-25.md`, `state_last_run_seed.yaml`, `workflow-gap-seeding-followup-2026-03-25.md`

The active set is specification-only but review-ready. The experimental set still needs stronger evidence about scope boundaries, overlap, family placement, and promotion criteria. The disabled workflows are deferred by timing or kept as superseded-but-informative references rather than rejected on quality.

## Final Output Gate

Polished final outputs should be generated only after:

- the relevant workflow inventory has been reviewed
- the candidate is placed in the correct lifecycle bucket
- the needed support templates and documentation exist or the missing gaps are explicitly recorded
