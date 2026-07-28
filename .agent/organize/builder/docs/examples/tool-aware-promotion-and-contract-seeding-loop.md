# Tool-Aware Promotion And Contract Seeding Loop

Date: `2026-03-26`

## Purpose

This example shows the next extension of the current workflow library now that the first real tool contracts exist. The rule stays the same: let tools handle deterministic extraction, packaging, validation, and cataloging, and let workflows interpret those outputs or seed the missing tool inputs.

## Example Sequence

1. Start with `workflow_goal_coverage_audit` or `workspace_delta_scan` when you need to confirm whether the current gap is still real.
2. If the missing work is wrapper or operator handoff, run `tool_surface_snapshotter` first and then `tool_wrapper_onboarding_brief`.
3. If the missing work is demo publish safety, run `media_evidence_packager` and `artifact_contract_validator` first and then `demo_refresh_sanitization_brief`.
4. If the hard part is not interpretation but defining what the validator or packager should inspect, stage `artifact_contract_profile_seed` under `proposals/workflow_proposals/`.
5. Feed the resulting outputs downstream into `tool_surface_contract_audit`, `release_evidence_packet_seed`, or `factory_export_readiness_review` instead of recomputing the same evidence manually.

## Why These Promotions Are Legitimate

- `tool_wrapper_onboarding_brief` no longer has to wait on an abstract tool inventory because `tool_surface_snapshotter` already exists as a real contract in `tools/final_tools/`.
- `demo_refresh_sanitization_brief` no longer has to stay proposal-only because `media_evidence_packager` and `artifact_contract_validator` now define the deterministic parts of the job.
- `artifact_contract_profile_seed` should still stay proposed because the config and contract schemas for those tools are not yet stable enough to claim a reusable live-library authoring workflow.

## Review Heuristic

- Promote a tool-aware workflow into `automations/experimental/` when the supporting tool contract already exists and the workflow can honestly stay interpretive.
- Keep a tool-aware workflow in `proposals/workflow_proposals/` when the repeated work is still upstream schema shaping for a tool that remains experimental or lightly proven.
