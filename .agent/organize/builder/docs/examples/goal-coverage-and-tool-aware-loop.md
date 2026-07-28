# Goal Coverage And Tool-Aware Loop

Date: `2026-03-25`

## Purpose

This example shows how to extend the workflow library without inventing duplicate governance layers or overstating the still specification-only tool layer.

## Example Sequence

1. Start with `workspace_inventory` or `workspace_delta_scan` to confirm what changed in the workspace and which reports are still current.
2. Run `workflow_goal_coverage_audit` to map business goals, workflow targets, and project-goal signals onto the current workflow library.
3. If the uncovered gap is still planning or review shaped, seed or run a library extension such as `validation_infrastructure_readiness_review`.
4. If the uncovered gap clearly wants extraction or inspection tooling first, keep it under `proposals/workflow_proposals/` until the supporting tool contracts are implemented and validated enough to carry the evidence load.
5. Use `workflow_review_gate` only after the new workflow or proposal has a clear reason to exist and a non-duplicate scope boundary.
6. Refresh overview and example docs later with `workflow_catalog_refresh` instead of mixing catalog maintenance into the seeding pass itself.

## Why The Split Matters

- `workflow_goal_coverage_audit` is library-wide and goal-grounded; it is not a replacement for `goal_reconciliation`, which only maps one incoming request.
- `validation_infrastructure_readiness_review` handles shared validation plumbing; it is not a replacement for `human_view_evidence_brief`, which plans proof for a specific feature, repo, or milestone.
- `tool_wrapper_onboarding_brief` stays proposed because `tool_surface_snapshotter` now exists as a contract, but wrapper onboarding still depends on implemented and trustworthy extraction support.
- `demo_refresh_sanitization_brief` stays proposed because `media_evidence_packager` and `artifact_contract_validator` exist only as tool contracts today, and public-shareability review still depends on proving those inspection surfaces in practice.

## Review Heuristic

- Promote workflow-shaped gaps into `automations/experimental/` when reports, matrices, and follow-up queues are enough to express the work honestly.
- Keep tool-shaped gaps in `proposals/workflow_proposals/` when missing tooling would otherwise force the workflow to over-promise what it can validate.
