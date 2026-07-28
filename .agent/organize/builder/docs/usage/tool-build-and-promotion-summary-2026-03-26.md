# Tool Build And Promotion Summary

Date: `2026-03-26`

## Highest-Value Selected Candidates

The selected candidates remain the strongest accepted proposals already preserved under `proposals/tool_proposals/`:

1. `media_evidence_packager`
2. `artifact_contract_validator`
3. `tool_surface_snapshotter`
4. `portfolio_signal_normalizer`

The ranking still holds because these four proposals best combine business usefulness, workflow support value, repeatability, and implementation clarity without expanding into generic tooling.

## What This Pass Prepared

This pass made the tool layer more buildable without widening the proposal set:

- Updated `tools/tool_templates/cli_tool_contract_template.yaml` so it matches the real seeded `tool_contract` shape, including command contracts, workflow handoffs, package assets, build plan slices, and promotion gates.
- Updated `tools/tool_templates/tool_validation_note_template.md` so future validation notes follow the same placement, fixture, workflow-handshake, and promotion structure already used by the reviewed tools.
- Added per-tool input templates:
  - `tools/tool_templates/media_evidence_packager_config_template.yaml`
  - `tools/tool_templates/artifact_contract_validator_contract_template.yaml`
  - `tools/tool_templates/tool_surface_snapshotter_config_template.yaml`
  - `tools/tool_templates/portfolio_signal_normalizer_config_template.yaml`
- Refreshed all current tool contracts so each one now names its template asset, validation note, and stable usage docs directly inside the package definition.
- Aligned workflow handoff artifacts with the validation notes, including the missing `missing-contract-signals.json` handoff for `tool_surface_snapshotter`.
- Added example-driven docs that show which template to copy first and which workflow should run after each tool call.

## Workflow Call Order To Preserve

- Run `media_evidence_packager` before `release_evidence_packet_seed`, `human_view_evidence_brief`, and `factory_export_readiness_review`.
- Run `artifact_contract_validator` before `factory_export_readiness_review` and before `release_evidence_packet_seed` whenever a release or demo payload has an explicit file contract.
- Run `tool_surface_snapshotter` before `tool_surface_contract_audit` and `workflow_catalog_refresh`.
- Run `portfolio_signal_normalizer` before `portfolio_signal_refresh` and optionally before `proposal_backlog_triage`.

The tool-first rule is unchanged: tools should handle extraction, packaging, validation, normalization, and cataloging. Workflows should handle interpretation, prioritization, and follow-up planning.

## Promotion Status

- Keep `media_evidence_packager` in `tools/experimental_tools/`.
- Keep `artifact_contract_validator` in `tools/experimental_tools/`.
- Keep `portfolio_signal_normalizer` in `tools/experimental_tools/`.
- Keep `tool_surface_snapshotter` in `tools/final_tools/`.

No additional promotion is justified today. `tool_surface_snapshotter` remains the only package ready for final placement because it is read-only, narrow, and already anchored to a stable audit workflow. The other three still need fixture-backed proof that their schemas stay stable across more than one repo family.

## Validation Still Required

- `media_evidence_packager`: prove class-rule stability, duplicate handling, and public/private partitioning across both release-evidence and factory-evidence fixtures.
- `artifact_contract_validator`: prove the contract schema against at least one factory export bundle and one public demo payload.
- `portfolio_signal_normalizer`: prove extraction stability across at least two portfolio roots and keep `owner-summary-seed.md` deterministic.
- `tool_surface_snapshotter`: prove extractor coverage and drift stability, but not placement; the package is already final-ready unless implementation pressure broadens the command surface.

## Recommended Next Build Order

1. Implement `media_evidence_packager` from its config template and validation note.
2. Implement `artifact_contract_validator` from its contract template and validation note.
3. Implement `tool_surface_snapshotter` using the final contract and config template as the baseline.
4. Implement `portfolio_signal_normalizer` after the evidence and contract tools, because it is valuable but less implementation-clear today.
