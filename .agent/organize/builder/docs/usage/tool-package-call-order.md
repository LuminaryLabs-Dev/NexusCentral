# Tool Package Call Order

Date: `2026-03-26`

## Rule

Run the deterministic CLI tool first when the hard work is extraction, packaging, validation, or normalization. Run the workflow second when the remaining work is interpretation, prioritization, or follow-up design.

## Current Tool-To-Workflow Sequence

### Release Evidence And Human-View Family

1. Seed a repo-specific evidence config from `tools/tool_templates/media_evidence_packager_config_template.yaml`.
2. Run `media_evidence_packager package` to build a stable bundle and manifest from screenshots, logs, tests, notes, and related evidence.
3. Seed a repo-specific contract from `tools/tool_templates/artifact_contract_validator_contract_template.yaml` and run `artifact_contract_validator validate` if the milestone also promises a declared export or demo payload contract.
4. Run `release_evidence_packet_seed`, `human_view_evidence_brief`, or `factory_export_readiness_review` against the tool outputs instead of rediscovering files manually.

Primary handoff files:

- `media_evidence_packager`: `reports/manifest.json`, `reports/validation-summary.json`, `reports/missing-evidence.json`
- `artifact_contract_validator`: `reports/contract-validation.json`, `reports/missing-files.json`, `reports/unexpected-files.json`

### Tool Surface Audit Family

1. Seed a repo-set config from `tools/tool_templates/tool_surface_snapshotter_config_template.yaml`.
2. Run `tool_surface_snapshotter snapshot` across the selected repo roots.
3. Run `tool_surface_snapshotter diff` if a previous snapshot exists or drift review is part of the audit.
4. Run `tool_surface_contract_audit` or `workflow_catalog_refresh` against the emitted catalog, missing-signal report, and drift report.

Primary handoff files:

- `reports/tool-catalog.json`
- `reports/missing-contract-signals.json`
- `reports/drift-report.json`

### Portfolio Signal Family

1. Seed a portfolio-source config from `tools/tool_templates/portfolio_signal_normalizer_config_template.yaml`.
2. Run `portfolio_signal_normalizer` across configured canonical and mirror sources.
3. Run `portfolio_signal_refresh` against the normalized records and drift report.
4. Use `proposal_backlog_triage` only if the normalized portfolio evidence should reshape follow-up ranking.

Primary handoff files:

- `reports/portfolio-signals.json`
- `reports/mirror-drift.json`
- `reports/owner-summary-seed.md`

## Placement Guidance

- Keep `media_evidence_packager`, `artifact_contract_validator`, and `portfolio_signal_normalizer` in `tools/experimental_tools/` until their schemas survive repeated fixture review.
- Keep `tool_surface_snapshotter` in `tools/final_tools/` unless later implementation work proves the command surface is broader than current final-ready scope.
