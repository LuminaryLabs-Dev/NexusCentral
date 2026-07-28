# Tool Proposal Summary

Date: `2026-03-26`

## Current State

- `tools/experimental_tools/` already covers evidence packaging, artifact contract validation, and portfolio normalization.
- `tools/final_tools/` already covers declared tool-surface cataloging.
- The strongest remaining gaps are downstream of that baseline, not replacements for it.

The library now has a credible first tool layer. The next useful additions should extend it where workflows still do file inspection, signal extraction, or runtime probing manually.

## Priority Order

### 1. `publish_bundle_validator`

Why first:

- the release and demo family still has a public-share safety gap after packaging and contract validation
- `ProjectArrowhead-Demo` already defines an explicit public versus private boundary
- the staged `demo_refresh_sanitization_brief` workflow is blocked on this kind of tooling

Workflows that benefit:

- `release_evidence_packet_seed`
- proposed `demo_refresh_sanitization_brief`

Tool-before-workflow boundary:

- tool proves whether the publishable bundle leaks private artifacts or violates boundary rules
- workflow explains whether the remaining issues block review, release, or external sharing

### 2. `evidence_signal_extractor`

Why second:

- `media_evidence_packager` can gather files, but it does not turn them into a decision-ready signal layer
- release and human-view workflows still spend manual effort reading logs, smoke results, and test artifacts
- this is a direct fit for Release Evidence Hub style structured-input preparation

Workflows that benefit:

- `release_evidence_packet_seed`
- `human_view_evidence_brief`
- `validation_infrastructure_readiness_review`

Tool-before-workflow boundary:

- tool extracts pass, warn, fail, missing, and stale signals from mixed evidence artifacts
- workflow interprets which signals matter most and what follow-up work is justified

### 3. `runtime_health_snapshotter`

Why third:

- `tool_surface_snapshotter` already covers declared contract surfaces, but not live runtime proof
- several repos expose local health, runtime, screenshot, or toolkit endpoints that now require manual checking
- this fills a clear gap between route inventory and readiness interpretation

Workflows that benefit:

- `validation_infrastructure_readiness_review`
- `tool_surface_contract_audit`

Tool-before-workflow boundary:

- tool captures live endpoint responses, sample payloads, and drift evidence
- workflow ranks the runtime risk and decides whether the surface is reliable enough for later handoffs

### 4. `asset_catalog_indexer`

Why fourth:

- factory export review still treats catalog readiness as mostly manual
- Botany and Texture evidence already point to pack manifests, previews, and catalog surfaces as recurring business needs
- this is structured and reusable, but slightly less urgent than public-share validation and release signal extraction

Workflows that benefit:

- `factory_export_readiness_review`
- `release_evidence_packet_seed` for asset-pack releases

Tool-before-workflow boundary:

- tool builds the normalized asset catalog and missing-preview reports
- workflow decides whether the export surface is commercially credible and what productization work remains

## Main Findings

- The current workflow layer is already well-shaped for interpretation and prioritization.
- The current tool layer is still thin around public-share safety, structured evidence extraction, live runtime proof, and asset catalog preparation.
- Those are all repeatable, data-oriented, and business-facing enough that a CLI tool should come before a workflow-only pass.
- Existing tools should still run first where they apply:
  - `media_evidence_packager` before evidence-oriented workflows
  - `artifact_contract_validator` before export or payload contract review
  - `tool_surface_snapshotter` before tool-surface audits
  - `portfolio_signal_normalizer` before portfolio refresh work

## Recommendation

Keep the existing four tools as the base layer, then prioritize the next proposal wave in this order:

1. `publish_bundle_validator`
2. `evidence_signal_extractor`
3. `runtime_health_snapshotter`
4. `asset_catalog_indexer`

The new rule is narrower than yesterday's pass: do not add more broad review workflows where the real missing step is bundle-boundary validation, evidence extraction, runtime probing, or asset catalog normalization.
