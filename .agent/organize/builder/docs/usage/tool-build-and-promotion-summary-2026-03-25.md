# Tool Build And Promotion Summary

Date: `2026-03-25`

## Highest-Value Selected Candidates

The strongest accepted proposals remain the same four candidates already ranked in `proposals/tool_proposals/tool_proposal_summary_2026-03-25.md`:

1. `media_evidence_packager`
2. `artifact_contract_validator`
3. `tool_surface_snapshotter`
4. `portfolio_signal_normalizer`

They were selected because they best combine business usefulness, workflow support value, repeatability, and implementation clarity without drifting into generic tooling.

## What This Pass Prepared

### Reusable Templates

- Added `tools/tool_templates/cli_tool_contract_template.yaml` for future tool specs.
- Added `tools/tool_templates/tool_validation_note_template.md` for future review notes.
- Updated `tools/validation/tool-authoring-validation-notes.md` so it reflects the now-seeded tool layer.

### Tool Contracts

- Experimental: `tools/experimental_tools/media_evidence_packager.yaml`
- Experimental: `tools/experimental_tools/artifact_contract_validator.yaml`
- Experimental: `tools/experimental_tools/portfolio_signal_normalizer.yaml`
- Final-ready: `tools/final_tools/tool_surface_snapshotter.yaml`

### Validation Notes

- `tools/validation/media_evidence_packager-validation.md`
- `tools/validation/artifact_contract_validator-validation.md`
- `tools/validation/portfolio_signal_normalizer-validation.md`
- `tools/validation/tool_surface_snapshotter-validation.md`

## Workflow Relationships To Preserve

- Run `media_evidence_packager` before `release_evidence_packet_seed`, `human_view_evidence_brief`, and `factory_export_readiness_review`.
- Run `artifact_contract_validator` before `factory_export_readiness_review` and before `release_evidence_packet_seed` when a release or demo payload has an explicit output contract.
- Run `tool_surface_snapshotter` before `tool_surface_contract_audit` and `workflow_catalog_refresh`.
- Run `portfolio_signal_normalizer` before `portfolio_signal_refresh` and optionally before `proposal_backlog_triage`.

The rule is explicit: workflows should interpret these outputs, not recreate the packaging, validation, cataloging, or normalization logic in prose.

## What Still Needs Validation

- `media_evidence_packager`: prove config stability, duplicate-class handling, and public/private partitioning with realistic demo and factory fixtures.
- `artifact_contract_validator`: prove the contract schema against both factory exports and public demo payloads.
- `portfolio_signal_normalizer`: prove extraction rules across more than one portfolio root and keep the markdown summary seed deterministic.
- `tool_surface_snapshotter`: prove extractor coverage and stable diff output, but not placement; the contract is already narrow enough for final storage.

## Promotion Decision

- Ready for `tools/final_tools/` now: `tool_surface_snapshotter`
- Keep experimental for now: `media_evidence_packager`, `artifact_contract_validator`, `portfolio_signal_normalizer`

## Recommended Implementation Order

1. `media_evidence_packager`
2. `artifact_contract_validator`
3. `tool_surface_snapshotter`
4. `portfolio_signal_normalizer`

This order preserves the strongest business leverage first while still treating `tool_surface_snapshotter` as the first stable final contract in the library.
