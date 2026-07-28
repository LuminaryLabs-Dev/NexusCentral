# Library Extension Precedence

## Purpose

Use this note before adding new workflow, tool, or proposal material. The builder already contains real library families. Extend those families first instead of creating parallel categories, alternate roots, or duplicate naming schemes.

## First Placement Decision

- Put repeated planning, review, reconciliation, seeding, or reporting specs in `automations/`.
- Put callable surface contracts, wrappers, adapters, validators, or tool validation notes in `tools/`.
- Put staged pre-library candidates in `proposals/`.
- Put run-generated follow-up queues in `improvements/proposals/`, not in `proposals/`.

Read [`workspace-contract.md`](../../docs/conventions/workspace-contract.md) and [`workflow-tool-proposal-boundaries.md`](../../docs/conventions/workflow-tool-proposal-boundaries.md) before introducing new material.

## Established Workflow Families

Extend one of these families before inventing a new workflow lane:

- foundation and governance: `workspace_inventory`, `initiative_plan`, `workflow_review_gate`
- planning intake and setup: `request_intake_brief`, `project_setup_brief`, `goal_reconciliation`
- proposal and gap shaping: `proposal_backlog_triage`, `workflow_gap_seeding`, `example_usage_seed`
- catalog and documentation maintenance: `docs_reconciliation`, `docs_cleanup_threshold_seed`, `workspace_delta_scan`, `workflow_template_gap_review`, `workflow_placeholder_audit`, `workflow_catalog_refresh`, `workflow_overlap_reconciliation`, `workflow_handoff_reconciliation`, `workspace_hygiene_triage`
- goal-linked domain review: `factory_export_readiness_review`, `human_view_evidence_brief`, `portfolio_signal_refresh`, `release_evidence_packet_seed`, `tool_surface_contract_audit`

## Established Tool Families

Extend one of these tool lanes before creating a new top-level tool theme:

- tool surface inventory and snapshotting: `tool_surface_snapshotter`
- artifact and evidence contract validation: `artifact_contract_validator`, `media_evidence_packager`
- portfolio and signal normalization: `portfolio_signal_normalizer`

If a new tool does not fit one of these lanes, preserve the same directory contract by staging the idea in [`proposals/tool_proposals/`](../../proposals/tool_proposals/) first.

## Proposal Rules

- Keep workflow ideas that are not ready for the live library in [`proposals/workflow_proposals/`](../../proposals/workflow_proposals/).
- Keep tool ideas that are not ready for the live library in [`proposals/tool_proposals/`](../../proposals/tool_proposals/).
- Promote only when scope, outputs, and placement are clear enough to join `automations/` or `tools/`.

## Normalization Rule

- Prefer documenting a new sub-family inside the existing contract over creating a new top-level bucket.
- Preserve the current `active`, `experimental`, `disabled`, `experimental_tools`, and `final_tools` splits.
- Treat this workspace as a planning and authoring library, not an implementation runtime.
