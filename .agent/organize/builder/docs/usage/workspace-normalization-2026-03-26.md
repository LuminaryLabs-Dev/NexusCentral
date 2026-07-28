# Workspace Normalization Record

Date: `2026-03-26`

## Scope

Review the existing Nexus Central Builder workspace, preserve the established library, and normalize only what is still genuinely missing for later planning, workflow-authoring, tool-authoring, and proposal work.

## Found And Preserved

- The required directory contract already exists across `automations/`, `templates/`, `tools/`, `proposals/`, `docs/`, `goals/`, `lessons/`, `improvements/`, `logs/`, `artifacts/`, `outputs/`, `state/`, and `archive/`.
- The required subdirectories also already exist, including `automations/active/`, `automations/experimental/`, `automations/disabled/`, all requested template buckets, the full tool subtree, the proposal subtree, all goal buckets, all lesson buckets, all improvement buckets, and `state/locks/` plus `state/last_runs/`.
- The live workflow library remains intact with `3` active workflow specs, `20` experimental workflow specs, and `3` disabled workflow specs.
- The tool library remains intact with `3` experimental tool contracts, `1` final tool contract, reusable tool templates, and tool validation notes.
- The proposal library remains intact with staged workflow proposals plus tool proposal records preserved as provenance for promoted tool contracts.

## Minimal Normalization Performed

- No folders needed to be created.
- No workflow, tool, template, proposal, goal, lesson, or archive files needed to move.
- Added [`library-extension-precedence.md`](../../docs/conventions/library-extension-precedence.md) so later automations have one concise rule set for extending the existing families instead of duplicating them.
- Added this dated record so the current normalized state is explicit without rewriting the broader workspace contract.

## Established Families To Extend

Workflow families already established:

- foundation and governance: `workspace_inventory`, `initiative_plan`, `workflow_review_gate`
- planning intake and setup: `request_intake_brief`, `project_setup_brief`, `goal_reconciliation`
- proposal and gap shaping: `proposal_backlog_triage`, `workflow_gap_seeding`, `example_usage_seed`
- catalog and documentation maintenance: `docs_reconciliation`, `docs_cleanup_threshold_seed`, `workspace_delta_scan`, `workflow_template_gap_review`, `workflow_placeholder_audit`, `workflow_catalog_refresh`, `workflow_overlap_reconciliation`, `workflow_handoff_reconciliation`, `workspace_hygiene_triage`
- goal-linked domain review: `factory_export_readiness_review`, `human_view_evidence_brief`, `portfolio_signal_refresh`, `release_evidence_packet_seed`, `tool_surface_contract_audit`

Tool families already established:

- tool surface inventory and snapshotting: `tool_surface_snapshotter`
- artifact and evidence contract validation: `artifact_contract_validator`, `media_evidence_packager`
- portfolio and signal normalization: `portfolio_signal_normalizer`

Proposal families already established:

- staged workflow proposals in [`proposals/workflow_proposals/`](../../proposals/workflow_proposals/)
- staged tool proposals and provenance records in [`proposals/tool_proposals/`](../../proposals/tool_proposals/)

## Ready State

- Pattern discovery can start from the existing workflow, tool, proposal, goal, and lesson surfaces without additional scaffolding.
- Gap seeding can continue without creating new roots because proposal staging and improvement backlog placement are already separated.
- Business-goal-driven workflow generation can continue against the existing `goals/` and workflow-family structure.
- Tool proposal work can continue by extending the current tool lanes or staging new ideas under `proposals/tool_proposals/` first.
