# Workflow And Tooling Reconciliation Report

Date: `2026-03-26`

## Scope

This pass reviewed the planning and authoring layer across `automations/`, `tools/`, `proposals/`, `templates/`, `docs/`, `goals/`, `lessons/`, `improvements/`, and `archive/`. The goal was to leave one coherent catalog with accurate workflow families, clean proposal boundaries, and tool-linked workflow staging that matches the current builder library instead of yesterday's partial state.

## Step 1: Current State Reviewed

Reviewed workflow inventory:

- active: `workspace_inventory`, `initiative_plan`, `workflow_review_gate`
- experimental planning, intake, and coverage audit: `request_intake_brief`, `project_setup_brief`, `goal_reconciliation`, `proposal_backlog_triage`, `workflow_gap_seeding`, `workflow_goal_coverage_audit`, `example_usage_seed`
- experimental catalog maintenance and consistency review: `docs_reconciliation`, `docs_cleanup_threshold_seed`, `workspace_delta_scan`, `workflow_template_gap_review`, `workflow_placeholder_audit`, `workflow_catalog_refresh`, `workflow_overlap_reconciliation`, `workflow_handoff_reconciliation`
- experimental goal-linked domain review and validation enablement: `factory_export_readiness_review`, `human_view_evidence_brief`, `validation_infrastructure_readiness_review`, `portfolio_signal_refresh`, `release_evidence_packet_seed`, `tool_surface_contract_audit`, `workspace_hygiene_triage`
- disabled: `state_last_run_contract`, `workflow_validation_review`, `workflow_lifecycle_review`

Reviewed tool inventory:

- final tool contracts: `tool_surface_snapshotter`
- experimental tool contracts: `artifact_contract_validator`, `media_evidence_packager`, `portfolio_signal_normalizer`
- proposal provenance under `proposals/tool_proposals/`

Reviewed staged workflow proposals:

- `tool_wrapper_onboarding_brief`
- `demo_refresh_sanitization_brief`

## Step 2: Drift And Overlap Found

Confirmed issues:

- workflow inventory docs still described only twenty experimental workflows even though `automations/experimental/` contains twenty-two
- the two newly seeded workflows, `workflow_goal_coverage_audit` and `validation_infrastructure_readiness_review`, existed in the library but were omitted from the reviewed inventory docs and family summaries
- family naming had drifted: some docs still used older support-family language that no longer reflected the current goal-coverage and validation-enablement lanes
- the staged workflow proposals still depended on placeholder tool names such as `artifact_manifest_diff_cli` and `tool_surface_inventory_cli` instead of the actual tool families now present under `tools/`
- the example guidance still implied the workspace had no real tool inventory, which is no longer true now that tool contracts exist under `tools/final_tools/` and `tools/experimental_tools/`

## Step 3: Reconciliation Actions

Kept the live bucket structure intact and reconciled the authoring layer around it:

- updated [`automations/README.md`](../../automations/README.md) to reflect the actual twenty-two-workflow experimental catalog and its current family structure
- updated [`docs/conventions/automation-lifecycle.md`](../../docs/conventions/automation-lifecycle.md) so the lifecycle contract matches the real inventory
- updated [`docs/conventions/workflow-tool-authoring-pattern-guide.md`](../../docs/conventions/workflow-tool-authoring-pattern-guide.md) so family guidance no longer duplicates or omits newer workflows
- tightened [`proposals/workflow_proposals/tool_wrapper_onboarding_brief.yaml`](../../proposals/workflow_proposals/tool_wrapper_onboarding_brief.yaml) to depend on `tool_surface_snapshotter` instead of vague pre-tool placeholders
- tightened [`proposals/workflow_proposals/demo_refresh_sanitization_brief.yaml`](../../proposals/workflow_proposals/demo_refresh_sanitization_brief.yaml) to depend on `media_evidence_packager` and `artifact_contract_validator` instead of untracked invented tool names
- updated [`docs/examples/goal-coverage-and-tool-aware-loop.md`](../../docs/examples/goal-coverage-and-tool-aware-loop.md) so it reflects the current state: tool contracts exist, but they are not implemented or validated enough to collapse proposal-stage workflow boundaries yet

No workflow files were moved between `active/`, `experimental/`, `disabled/`, or `archive/` in this pass. No tool files were promoted or archived in this pass. Older dated reports remain preserved as historical context.

## Step 4: Clean Library Decisions

Remain active:

- `workspace_inventory`
- `initiative_plan`
- `workflow_review_gate`

Remain experimental:

- planning, intake, and coverage audit: `request_intake_brief`, `project_setup_brief`, `goal_reconciliation`, `proposal_backlog_triage`, `workflow_gap_seeding`, `workflow_goal_coverage_audit`, `example_usage_seed`
- catalog maintenance and consistency review: `docs_reconciliation`, `docs_cleanup_threshold_seed`, `workspace_delta_scan`, `workflow_template_gap_review`, `workflow_placeholder_audit`, `workflow_catalog_refresh`, `workflow_overlap_reconciliation`, `workflow_handoff_reconciliation`
- goal-linked domain review and validation enablement: `factory_export_readiness_review`, `human_view_evidence_brief`, `validation_infrastructure_readiness_review`, `portfolio_signal_refresh`, `release_evidence_packet_seed`, `tool_surface_contract_audit`, `workspace_hygiene_triage`
- experimental tool contracts: `artifact_contract_validator`, `media_evidence_packager`, `portfolio_signal_normalizer`

Remain final:

- `tool_surface_snapshotter`

Remain disabled:

- `state_last_run_contract`
- `workflow_validation_review`
- `workflow_lifecycle_review`

Remain staged proposals:

- `tool_wrapper_onboarding_brief` because wrapper onboarding still depends on validated extractor support from `tool_surface_snapshotter`
- `demo_refresh_sanitization_brief` because demo sanitization still depends on validated evidence packaging and artifact contract tooling rather than only workflow prose

Remain archived:

- current files under `archive/` only; no new archive moves were warranted in this pass

Tool proposals that should advance next:

- no new proposal wave is needed
- the next advancement step is validating or implementing `media_evidence_packager`, `artifact_contract_validator`, and `portfolio_signal_normalizer`
- keep `tool_surface_snapshotter` stable as the final baseline unless real review failures force a contract change

Workflow families now cleanly represented:

- foundation and governance
- planning, intake, and coverage audit
- catalog maintenance and consistency review
- goal-linked domain review and validation enablement

## Step 5: What Still Needs Work

The library is cleaner, but the remaining gaps are proof and validation gaps, not naming or placement gaps:

- validate whether `workflow_goal_coverage_audit` should become a standard precursor to future seeding passes
- prove whether `validation_infrastructure_readiness_review` stays usefully separate from `human_view_evidence_brief` and `release_evidence_packet_seed`
- validate the three experimental tool contracts against realistic fixtures before promoting any workflow proposal that depends on them
- keep staged workflow proposals attached to existing tool families instead of inventing new placeholder tool names
- continue treating older dated usage reports as historical snapshots unless a report is explicitly refreshed by a new dated pass

The cleaned state is now coherent: the workflow inventory docs match the actual catalog, the newer workflows are represented in the right families, staged workflow proposals point at real tool families, and the next extension path is validation of existing contracts rather than another round of overlapping seeds.
