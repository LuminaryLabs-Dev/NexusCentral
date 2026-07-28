# Tool Validation Note: `media_evidence_packager`

Date: `2026-03-26`
Status: `prepared for experimental review`

## Placement Decision

- `proposal_source`: `proposals/tool_proposals/media_evidence_packager.md`
- `tool_spec`: `tools/experimental_tools/media_evidence_packager.yaml`
- `template_asset`: `tools/tool_templates/media_evidence_packager_config_template.yaml`
- `example_usage_docs`: `docs/examples/tool-package-cli-examples.md`, `docs/examples/tool-package-template-examples.md`
- `current_decision`: Keep this tool in `experimental_tools/` because the contract is strong, but the config schema and public/private partition behavior still need realistic fixture proof across more than one repo family.

## Contract Checks To Run First

- Confirm the command surface stays limited to `package` and `validate`.
- Confirm `reports/manifest.json`, `reports/validation-summary.json`, and `reports/missing-evidence.json` are sufficient for downstream workflows without extra ad hoc files.
- Confirm every source file is read-only and every staged file lands only under the chosen output directory.
- Confirm sanitization rules can separate public and private evidence without duplicate manifest entries.

## Minimum Fixture Coverage

- A release-evidence fixture with screenshots, logs, tests, and notes that proves mixed-class packaging and manifest completeness.
- A sanitized demo fixture that proves public/private partitioning and missing-evidence reporting under real redaction rules.

## Workflow Handshake To Verify

- Run this tool before `release_evidence_packet_seed`, `human_view_evidence_brief`, and `factory_export_readiness_review`.
- Downstream workflows should read `manifest.json`, `validation-summary.json`, and `missing-evidence.json` instead of rediscovering files themselves.
- The workflows still own readiness interpretation, gap prioritization, and stakeholder framing.

## Promotion Gate

- Promote only after the config schema survives repeated review in both a release packet and a factory evidence scenario.

## Current Risks

- Overlapping classification rules could create duplicate or ambiguous evidence assignments.
- Sanitization requirements may pressure the config schema to expand unless the first implementation stays disciplined.
