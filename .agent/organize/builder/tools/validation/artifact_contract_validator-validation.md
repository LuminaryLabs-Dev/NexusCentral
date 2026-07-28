# Tool Validation Note: `artifact_contract_validator`

Date: `2026-03-26`
Status: `prepared for experimental review`

## Placement Decision

- `proposal_source`: `proposals/tool_proposals/artifact_contract_validator.md`
- `tool_spec`: `tools/experimental_tools/artifact_contract_validator.yaml`
- `template_asset`: `tools/tool_templates/artifact_contract_validator_contract_template.yaml`
- `example_usage_docs`: `docs/examples/tool-package-cli-examples.md`, `docs/examples/tool-package-template-examples.md`
- `current_decision`: Keep this tool in `experimental_tools/` because the contract is credible, but the rule schema still needs proof against both factory export bundles and public demo payloads.

## Contract Checks To Run First

- Confirm `validate` and `inventory` are enough and no broader contract-authoring mode is introduced.
- Confirm failed rules cite exact missing or mismatched paths rather than generic summary text.
- Confirm manifest-parity results remain distinct from raw filesystem-rule results.
- Confirm the target artifact directory stays strictly read-only.

## Minimum Fixture Coverage

- A factory export fixture with LOD-style counts, naming patterns, and manifest parity rules.
- A demo payload fixture with public/private partition rules and explicit unexpected-file handling.

## Workflow Handshake To Verify

- Run this tool before `factory_export_readiness_review` and before `release_evidence_packet_seed` when a generated payload has a declared contract.
- Downstream workflows should read `contract-validation.json`, `contract-validation.md`, `missing-files.json`, and `unexpected-files.json`.
- Workflow logic should decide readiness and commercialization impact; the tool should only validate the declared contract.

## Promotion Gate

- Promote only after one factory-style contract and one demo-style contract pass repeated review without schema churn.

## Current Risks

- Contract authors may try to encode business policy instead of concrete file rules.
- Severity handling could become inconsistent if early fixtures mix warnings and errors without a stable threshold policy.
