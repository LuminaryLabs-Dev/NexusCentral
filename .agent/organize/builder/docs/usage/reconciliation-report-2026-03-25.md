# Workflow And Library Reconciliation Report

Date: `2026-03-25`

## Scope

This pass reconciled the planning layer across workflow specs, goals, templates, lessons, archive history, tool templates and validation notes, staged workflow and tool proposals, and the live workflow catalog. The objective was to leave one coherent library with clear family boundaries instead of a partially updated workflow catalog plus conflicting tool or proposal structure.

## Step 1: Current State Reviewed

Reviewed live workflow inventory:

- active: [`workspace_inventory.yaml`](../../automations/active/workspace_inventory.yaml), [`initiative_plan.yaml`](../../automations/active/initiative_plan.yaml), [`workflow_review_gate.yaml`](../../automations/active/workflow_review_gate.yaml)
- experimental planning and authoring support: [`request_intake_brief.yaml`](../../automations/experimental/request_intake_brief.yaml), [`project_setup_brief.yaml`](../../automations/experimental/project_setup_brief.yaml), [`goal_reconciliation.yaml`](../../automations/experimental/goal_reconciliation.yaml), [`proposal_backlog_triage.yaml`](../../automations/experimental/proposal_backlog_triage.yaml), [`workflow_gap_seeding.yaml`](../../automations/experimental/workflow_gap_seeding.yaml), [`example_usage_seed.yaml`](../../automations/experimental/example_usage_seed.yaml)
- experimental catalog maintenance and consistency review: [`docs_reconciliation.yaml`](../../automations/experimental/docs_reconciliation.yaml), [`docs_cleanup_threshold_seed.yaml`](../../automations/experimental/docs_cleanup_threshold_seed.yaml), [`workspace_delta_scan.yaml`](../../automations/experimental/workspace_delta_scan.yaml), [`workflow_template_gap_review.yaml`](../../automations/experimental/workflow_template_gap_review.yaml), [`workflow_placeholder_audit.yaml`](../../automations/experimental/workflow_placeholder_audit.yaml), [`workflow_catalog_refresh.yaml`](../../automations/experimental/workflow_catalog_refresh.yaml), [`workflow_overlap_reconciliation.yaml`](../../automations/experimental/workflow_overlap_reconciliation.yaml), [`workflow_handoff_reconciliation.yaml`](../../automations/experimental/workflow_handoff_reconciliation.yaml)
- experimental goal-linked domain review: [`factory_export_readiness_review.yaml`](../../automations/experimental/factory_export_readiness_review.yaml), [`human_view_evidence_brief.yaml`](../../automations/experimental/human_view_evidence_brief.yaml), [`portfolio_signal_refresh.yaml`](../../automations/experimental/portfolio_signal_refresh.yaml), [`release_evidence_packet_seed.yaml`](../../automations/experimental/release_evidence_packet_seed.yaml), [`tool_surface_contract_audit.yaml`](../../automations/experimental/tool_surface_contract_audit.yaml), [`workspace_hygiene_triage.yaml`](../../automations/experimental/workspace_hygiene_triage.yaml)
- disabled: [`state_last_run_contract.yaml`](../../automations/disabled/state_last_run_contract.yaml), [`workflow_validation_review.yaml`](../../automations/disabled/workflow_validation_review.yaml), [`workflow_lifecycle_review.yaml`](../../automations/disabled/workflow_lifecycle_review.yaml)

Reviewed support surfaces:

- docs, templates, goals, lessons, improvements, and archive history
- `tools/experimental_tools/`, `tools/final_tools/`, `tools/tool_templates/`, `tools/validation/`, `proposals/workflow_proposals/`, and `proposals/tool_proposals/`

## Step 2: Drift And Overlap Found

Confirmed issues:

- inventory docs still described fourteen experimental workflows even though the workspace contained twenty
- the workspace contract and inventory docs did not cleanly match the generated tool and proposal subtree that already existed
- `improvements/proposals/` was carrying both run-specific follow-up queues and the implied role of a durable proposal library, which blurred proposal placement
- workflow discovery defaults in [`workspace_inventory.yaml`](../../automations/active/workspace_inventory.yaml), [`request_intake_brief.yaml`](../../automations/experimental/request_intake_brief.yaml), [`goal_reconciliation.yaml`](../../automations/experimental/goal_reconciliation.yaml), [`workflow_gap_seeding.yaml`](../../automations/experimental/workflow_gap_seeding.yaml), and [`workspace_delta_scan.yaml`](../../automations/experimental/workspace_delta_scan.yaml) did not inspect the full library the builder now expects
- the workflow catalog was structurally healthy, but the family split was implicit instead of documented
- a duplicate normalization pass briefly introduced alternate `tools/active` and `proposals/workflows` style names that conflicted with the already-generated `tools/final_tools`, `tools/experimental_tools`, `proposals/workflow_proposals`, and `proposals/tool_proposals` structure

Overlap that should stay visible rather than forced into a merge:

- `request_intake_brief` versus `goal_reconciliation` versus `initiative_plan`
- `project_setup_brief` versus `initiative_plan`
- `proposal_backlog_triage` versus `workflow_gap_seeding`
- `docs_reconciliation` versus `docs_cleanup_threshold_seed`
- `workflow_placeholder_audit` versus `workflow_handoff_reconciliation`

## Step 3: Reconciliation Actions Taken

Structural cleanup:

- kept the generated `tools/final_tools`, `tools/experimental_tools`, `tools/tool_templates`, and `tools/validation` split as the canonical tool-family structure
- kept the generated `proposals/workflow_proposals` and `proposals/tool_proposals` split as the canonical staged-proposal structure
- added [`docs/conventions/tool-lifecycle.md`](../../docs/conventions/tool-lifecycle.md) and updated [`docs/conventions/workspace-contract.md`](../../docs/conventions/workspace-contract.md) so those existing surfaces are first-class and non-duplicative
- added missing tool templates promised by the validation notes: [`cli_tool_contract_template.yaml`](../../tools/tool_templates/cli_tool_contract_template.yaml) and [`tool_validation_note_template.md`](../../tools/tool_templates/tool_validation_note_template.md)

Workflow and documentation cleanup:

- updated [`README.md`](../../README.md), [`automations/README.md`](../../automations/README.md), [`docs/conventions/automation-lifecycle.md`](../../docs/conventions/automation-lifecycle.md), [`docs/conventions/workflow-authoring-contract.md`](../../docs/conventions/workflow-authoring-contract.md), [`tools/README.md`](../../tools/README.md), and [`proposals/README.md`](../../proposals/README.md) so the written contract now matches the real library
- updated discovery defaults in the workflow inventory, intake, reuse, seeding, and delta-scan specs so future review passes actually read `goals/`, `tools/`, and `proposals/`
- updated tool authoring guidance and checklists so they no longer claim the tool-support surfaces are empty
- kept the active workflow trio unchanged because they remain the clearest default entry points
- kept the disabled trio unchanged because the merged review gate still supersedes them and no new shared-state pressure exists

## Step 4: Clean Library Decisions

Remain active:

- [`automations/active/workspace_inventory.yaml`](../../automations/active/workspace_inventory.yaml)
- [`automations/active/initiative_plan.yaml`](../../automations/active/initiative_plan.yaml)
- [`automations/active/workflow_review_gate.yaml`](../../automations/active/workflow_review_gate.yaml)

Remain experimental:

- planning and authoring support: `request_intake_brief`, `project_setup_brief`, `goal_reconciliation`, `proposal_backlog_triage`, `workflow_gap_seeding`, `example_usage_seed`
- catalog maintenance and consistency review: `docs_reconciliation`, `docs_cleanup_threshold_seed`, `workspace_delta_scan`, `workflow_template_gap_review`, `workflow_placeholder_audit`, `workflow_catalog_refresh`, `workflow_overlap_reconciliation`, `workflow_handoff_reconciliation`
- goal-linked domain review: `factory_export_readiness_review`, `human_view_evidence_brief`, `portfolio_signal_refresh`, `release_evidence_packet_seed`, `tool_surface_contract_audit`, `workspace_hygiene_triage`
- experimental tool contracts: `artifact_contract_validator`, `media_evidence_packager`, `portfolio_signal_normalizer`
- final-ready tool contracts: `tool_surface_snapshotter`
- staged workflow proposals: `demo_refresh_sanitization_brief` and `tool_wrapper_onboarding_brief` remain in `proposals/workflow_proposals/`
- tool proposal records remain in `proposals/tool_proposals/` as provenance for the promoted tool contracts

Remain disabled:

- [`automations/disabled/state_last_run_contract.yaml`](../../automations/disabled/state_last_run_contract.yaml)
- [`automations/disabled/workflow_validation_review.yaml`](../../automations/disabled/workflow_validation_review.yaml)
- [`automations/disabled/workflow_lifecycle_review.yaml`](../../automations/disabled/workflow_lifecycle_review.yaml)
- no separate disabled tool bucket is needed yet

Remain archived:

- all current files under [`archive/`](../../archive/) stay archived because they are historical normalization or superseded seeding context rather than live library material

Tool promotions and next advancement:

- `media_evidence_packager`, `artifact_contract_validator`, and `portfolio_signal_normalizer` have already advanced into `tools/experimental_tools/`
- `tool_surface_snapshotter` has already advanced into `tools/final_tools/`
- no additional staged tool proposal needs promotion right now; the next tool work is validating the promoted contracts
- keep the staged workflow proposals under `proposals/workflow_proposals/` until their dependent tool layer exists or the need disappears
- leave the other domain workflows as goal-linked experimental workflows until repeated use proves they deserve standalone tool or proposal records

Workflow families now cleanly represented:

- foundation and governance
- planning intake and setup
- reuse and workflow authoring
- catalog maintenance and consistency review
- goal-linked domain review

## Step 5: What Still Needs Work

The library is cleaner, but a few boundaries still need proof rather than more restructuring:

- produce example outputs for `project_setup_brief` and `docs_reconciliation` before promoting either one
- decide whether `request_intake_brief` stays separate from `goal_reconciliation` after more than one real reuse pass
- decide whether `proposal_backlog_triage` and `workflow_gap_seeding` stay separate once `proposals/` starts carrying durable records
- validate the experimental tool contracts against realistic fixtures before promoting any of them further
- keep `tool_surface_snapshotter` stable as the final-ready baseline unless concrete review failures prove the contract insufficient
- keep new staged workflow ideas in `proposals/workflow_proposals/` rather than mixing them into `improvements/proposals/`

The resulting workspace is now coherent: workflow buckets are accurate, tool and proposal surfaces exist without duplicating run-generated follow-up queues, and the next extension points are family-based instead of ad hoc.
