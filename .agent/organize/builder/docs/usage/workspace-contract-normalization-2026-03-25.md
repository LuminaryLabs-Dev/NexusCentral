# Workspace Contract Normalization Record

Date: `2026-03-25`

## Scope

Normalize the minimum remaining workspace contract for Nexus Central Builder without recreating the existing workflow library or broadening the repo into implementation work.

## Found And Preserved

- The live workflow library already exists under `automations/` with `3` active specs, `20` experimental specs, and `3` disabled specs.
- The template surface already exists under `templates/workflow_templates/`, `templates/project_setup_templates/`, `templates/doc_templates/`, and `templates/validation/`.
- The documentation surface already exists under `docs/conventions/`, `docs/examples/`, `docs/project_breakdowns/`, and `docs/usage/`.
- The goal surface already exists under `goals/business_goals/`, `goals/project_goals/`, and `goals/workflow_targets/`.
- Lessons, improvements, logs, artifacts, outputs, state, and archive buckets already exist and remain usable as-is.
- No existing workflow files, template files, goal files, or archive history needed to move.

## Normalized

- Created the missing `tools/` subtree with `experimental_tools/`, `final_tools/`, `tool_templates/`, and `validation/`.
- Created the missing `proposals/` subtree with `workflow_proposals/` and `tool_proposals/`.
- Refreshed [`workspace-contract.md`](../../docs/conventions/workspace-contract.md) so `tools/`, `proposals/`, and `goals/` are first-class parts of the contract.
- Refreshed [`workflow-authoring-contract.md`](../../docs/conventions/workflow-authoring-contract.md) so staged workflow proposals have a defined pre-spec home.
- Refreshed [`automation-lifecycle.md`](../../docs/conventions/automation-lifecycle.md) so the reviewed lifecycle snapshot matches the actual experimental inventory now present.
- Added [`workflow-tool-proposal-boundaries.md`](../../docs/conventions/workflow-tool-proposal-boundaries.md) to explain the boundary between workflows, tools, staged proposals, and improvement queues.

## Established Families To Extend

Workflow families already established:

- Inventory and governance: `workspace_inventory`, `workflow_review_gate`, `workspace_delta_scan`, `workflow_catalog_refresh`
- Planning and intake: `initiative_plan`, `request_intake_brief`, `goal_reconciliation`, `project_setup_brief`
- Documentation and reconciliation: `docs_reconciliation`, `docs_cleanup_threshold_seed`, `workflow_template_gap_review`, `workflow_placeholder_audit`, `workflow_overlap_reconciliation`, `workflow_handoff_reconciliation`, `workspace_hygiene_triage`
- Seeding and backlog shaping: `proposal_backlog_triage`, `workflow_gap_seeding`, `example_usage_seed`
- Cross-project evidence and release review: `factory_export_readiness_review`, `human_view_evidence_brief`, `portfolio_signal_refresh`, `release_evidence_packet_seed`
- Tool-surface and contract review: `tool_surface_contract_audit`

Tool families already established:

- No live tool implementations or tool contracts exist under `tools/` yet.
- The current tool-oriented family is documentation-first: tool surface contracts, wrapper alignment, runtime observability, and audit-driven follow-ups anchored by `tool_surface_contract_audit`, project-goal references, and project breakdown notes.
- Future tool work should extend that family inside `tools/experimental_tools/`, `tools/tool_templates/`, `tools/validation/`, and `proposals/tool_proposals/` rather than creating alternate top-level buckets.

## Ready State

- The workspace is ready for pattern discovery because the live workflow library, template surface, and contract docs are now aligned with the real directory model.
- The workspace is ready for gap seeding because staged workflow proposals now have a distinct home separate from improvement backlogs.
- The workspace is ready for business-goal-driven workflow generation because `goals/` is now explicitly part of the contract rather than implicit repo context.
- The workspace is ready for tool proposal work because the `tools/` and `proposals/tool_proposals/` buckets now exist without forcing premature implementation.
