# Tool Package Template Examples

Date: `2026-03-26`

These examples map each selected tool contract to the template asset that should be copied first, the first command to run, and the workflow that should consume the output next.

## Example 1: `media_evidence_packager`

- Template asset: `tools/tool_templates/media_evidence_packager_config_template.yaml`
- First saved copy: `configs/project-arrowhead-demo-evidence.yaml`
- First command: `ncb-media-evidence-packager package --config configs/project-arrowhead-demo-evidence.yaml --output-dir outputs/project-arrowhead-demo-evidence`
- Primary handoff: `reports/manifest.json`, `reports/validation-summary.json`, `reports/missing-evidence.json`
- Workflow follow-on: `release_evidence_packet_seed`, `human_view_evidence_brief`, or `factory_export_readiness_review`

## Example 2: `artifact_contract_validator`

- Template asset: `tools/tool_templates/artifact_contract_validator_contract_template.yaml`
- First saved copy: `contracts/unity-tree-pack.yaml`
- First command: `ncb-artifact-contract-validator validate --contract contracts/unity-tree-pack.yaml --artifact-dir ../NexusBotanyFactory/exports/tree-pack-v1 --output-dir outputs/tree-pack-contract-review`
- Primary handoff: `reports/contract-validation.json`, `reports/contract-validation.md`, `reports/missing-files.json`, `reports/unexpected-files.json`
- Workflow follow-on: `factory_export_readiness_review` or `release_evidence_packet_seed`

## Example 3: `tool_surface_snapshotter`

- Template asset: `tools/tool_templates/tool_surface_snapshotter_config_template.yaml`
- First saved copy: `configs/nexus-tool-surface-roots.yaml`
- First command: `ncb-tool-surface-snapshotter snapshot --config configs/nexus-tool-surface-roots.yaml --output-dir outputs/tool-surface-snapshot`
- Primary handoff: `reports/tool-catalog.json`, `reports/missing-contract-signals.json`, `reports/drift-report.json`
- Workflow follow-on: `tool_surface_contract_audit` or `workflow_catalog_refresh`

## Example 4: `portfolio_signal_normalizer`

- Template asset: `tools/tool_templates/portfolio_signal_normalizer_config_template.yaml`
- First saved copy: `configs/luminary-portfolio-sources.yaml`
- First command: `ncb-portfolio-signal-normalizer normalize --config configs/luminary-portfolio-sources.yaml --output-dir outputs/portfolio-signals`
- Primary handoff: `reports/portfolio-signals.json`, `reports/mirror-drift.json`, `reports/owner-summary-seed.md`
- Workflow follow-on: `portfolio_signal_refresh` and optionally `proposal_backlog_triage`
