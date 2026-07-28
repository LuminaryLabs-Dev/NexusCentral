# Tool Package CLI Examples

Date: `2026-03-26`

These are contract examples for the seeded tool files. They are not proof of implementation.

## Example 1: `media_evidence_packager`

Template seed:

- Start from `tools/tool_templates/media_evidence_packager_config_template.yaml` and save a repo-specific copy under `configs/`.

```bash
ncb-media-evidence-packager package \
  --config configs/project-arrowhead-demo-evidence.yaml \
  --output-dir outputs/project-arrowhead-demo-evidence
```

Expected handoff:

- `outputs/project-arrowhead-demo-evidence/reports/manifest.json`
- `outputs/project-arrowhead-demo-evidence/reports/validation-summary.json`
- `outputs/project-arrowhead-demo-evidence/reports/missing-evidence.json`

Workflow follow-on:

- Feed the reports into `release_evidence_packet_seed` or `human_view_evidence_brief`.

## Example 2: `artifact_contract_validator`

Template seed:

- Start from `tools/tool_templates/artifact_contract_validator_contract_template.yaml` and save a repo-specific contract under `contracts/`.

```bash
ncb-artifact-contract-validator validate \
  --contract contracts/unity-tree-pack.yaml \
  --artifact-dir ../NexusBotanyFactory/exports/tree-pack-v1 \
  --output-dir outputs/tree-pack-contract-review
```

Expected handoff:

- `outputs/tree-pack-contract-review/reports/contract-validation.json`
- `outputs/tree-pack-contract-review/reports/missing-files.json`
- `outputs/tree-pack-contract-review/reports/unexpected-files.json`

Workflow follow-on:

- Feed the reports into `factory_export_readiness_review`.

## Example 3: `tool_surface_snapshotter`

Template seed:

- Start from `tools/tool_templates/tool_surface_snapshotter_config_template.yaml` and save a repo-set config under `configs/`.

```bash
ncb-tool-surface-snapshotter snapshot \
  --config configs/nexus-tool-surface-roots.yaml \
  --output-dir outputs/tool-surface-snapshot
```

Optional drift pass:

```bash
ncb-tool-surface-snapshotter diff \
  --current outputs/tool-surface-snapshot/reports/tool-catalog.json \
  --previous state/last-tool-catalog.json \
  --output-dir outputs/tool-surface-drift
```

Workflow follow-on:

- Feed `tool-catalog.json` and `drift-report.json` into `tool_surface_contract_audit`.

## Example 4: `portfolio_signal_normalizer`

Template seed:

- Start from `tools/tool_templates/portfolio_signal_normalizer_config_template.yaml` and save a portfolio-specific copy under `configs/`.

```bash
ncb-portfolio-signal-normalizer normalize \
  --config configs/luminary-portfolio-sources.yaml \
  --output-dir outputs/portfolio-signals
```

Expected handoff:

- `outputs/portfolio-signals/reports/portfolio-signals.json`
- `outputs/portfolio-signals/reports/mirror-drift.json`
- `outputs/portfolio-signals/reports/owner-summary-seed.md`

Workflow follow-on:

- Feed the outputs into `portfolio_signal_refresh`.
