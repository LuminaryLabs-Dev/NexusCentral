# Workflow Gap Seeding Report

Date: `2026-03-26`
Status: `current seeding pass`
Workspace Root: `NexusCentral-Builder`
Author: `Codex`

## Step 1: Existing Coverage Reviewed First

Reviewed before seeding:

- `automations/active/`
- `automations/experimental/`
- `automations/disabled/`
- `tools/experimental_tools/`
- `tools/final_tools/`
- `proposals/workflow_proposals/`
- `proposals/tool_proposals/`
- `templates/workflow_templates/`
- `docs/conventions/`
- `docs/examples/`
- `docs/usage/`
- `goals/business_goals/`
- `goals/project_goals/`
- `goals/workflow_targets/`
- `lessons/`
- `improvements/`

## Step 2: Gap Handling Decision Before New Workflow Seeding

The current library already covers the governance and catalog-maintenance family well enough. No new governance workflow was seeded for goal cross-links, overlap, handoffs, or lifecycle review because those concerns are already handled by:

- `workflow_goal_coverage_audit`
- `workflow_handoff_reconciliation`
- `workflow_overlap_reconciliation`
- `workflow_review_gate`

The current tool layer also already handles the deterministic parts that should not be re-authored as prose:

- `tool_surface_snapshotter` handles route, manifest, schema, and discovery extraction.
- `media_evidence_packager` handles evidence bundling, manifest generation, and missing-evidence reporting.
- `artifact_contract_validator` handles artifact-contract validation, manifest parity, and public/private partition checks.

That left three real gaps:

1. The tool-surface family had audit coverage, but still no live onboarding workflow that consumes snapshot outputs.
2. The release and human-view family had evidence packet planning, but still no live demo-sanitization workflow that consumes package and validation outputs.
3. The new tools assume contract and profile inputs, but the library still lacks a clear upstream workflow for seeding those inputs without pretending the config schemas are final.

## Step 3: Existing Families Extended

Promoted into `automations/experimental/`:

- `tool_wrapper_onboarding_brief`
  - Extends: `tool_surface_contract_audit`
  - Why now: the earlier proposal blocker is gone because `tool_surface_snapshotter` exists in `tools/final_tools/`.
  - Why not a duplicate: it produces operator and wrapper onboarding guidance, not a drift audit.

- `demo_refresh_sanitization_brief`
  - Extends: `release_evidence_packet_seed` and `human_view_evidence_brief`
  - Why now: the earlier proposal blocker is gone because `media_evidence_packager` and `artifact_contract_validator` now exist in `tools/experimental_tools/`.
  - Why not a duplicate: it focuses on public-shareability, sanitization boundaries, and publish handoff rather than generic release-evidence planning.

## Step 4: Gap Kept As A Workflow Proposal

Created in `proposals/workflow_proposals/`:

- `artifact_contract_profile_seed`
  - Gap handled: upstream contract-profile authoring for validator and packager inputs.
  - Why it stays proposed: both supporting tools are still experimental, and their config schemas are not yet stable enough to anchor a reusable live-library authoring workflow.
  - Why not a duplicate: downstream review workflows interpret tool outputs, but none of them help define the artifact rules, evidence classes, and sanitization profiles those tools need first.

## Step 5: Support Files Added

Created template support:

- `templates/workflow_templates/tool_input_seed_template.yaml`
  - Reason: the existing template set covered review and audit shapes, but not workflows that seed draft tool-input profiles or contract sections.

Created example support:

- `docs/examples/tool-aware-promotion-and-contract-seeding-loop.md`
  - Reason: the current examples explained why these workflows stayed proposed on `2026-03-25`; this new example shows the next valid layering now that the tool inventory exists.

## File Set Created

- `automations/experimental/tool_wrapper_onboarding_brief.yaml`
- `automations/experimental/demo_refresh_sanitization_brief.yaml`
- `proposals/workflow_proposals/artifact_contract_profile_seed.yaml`
- `templates/workflow_templates/tool_input_seed_template.yaml`
- `docs/examples/tool-aware-promotion-and-contract-seeding-loop.md`
- `docs/usage/workflow-gap-seeding-report-2026-03-26-tool-aware-promotions.md`

## Remaining Thin Spots Not Seeded As New Workflows

- Missing explicit `goal_links.workflow_targets` inside the current workflow YAML inventory is a real cross-link gap, but it belongs to existing coverage-review and catalog-maintenance workflows rather than a new governance family.
- Experimental tool config schema proof is still missing for `media_evidence_packager` and `artifact_contract_validator`, so no live workflow was seeded for executable config authoring.
- Factory-specific version-readiness or routed-set review work is still thinner than ideal, but the current evidence pointed to contract-profile seeding as the higher-leverage gap first.
