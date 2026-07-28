# Tool Gap Priority Guidance

Date: `2026-03-26`

## What Changed

The first tool layer is no longer missing. The current question is where workflows are still doing tool-shaped work after the baseline tools finish.

Current baseline tools:

- `media_evidence_packager`
- `artifact_contract_validator`
- `tool_surface_snapshotter`
- `portfolio_signal_normalizer`

## Next Tool-Shaped Gaps

Add another tool before a new workflow when the repeated job is:

- publish-boundary validation for a public demo or stakeholder bundle
- extraction of pass, fail, warn, missing, or stale signals from mixed evidence artifacts
- live probing of health, runtime, screenshot, or toolkit endpoints
- indexing exported assets, previews, and manifests into a reviewable catalog

Keep the work as a workflow when the repeated job is:

- deciding whether a leak or runtime mismatch is business-critical
- ranking follow-ups across repos
- writing the owner-facing narrative
- choosing commercialization priorities

## Recommended New Tool Order

1. `publish_bundle_validator`
2. `evidence_signal_extractor`
3. `runtime_health_snapshotter`
4. `asset_catalog_indexer`

## Where The Tool Layer Should Come First

### Public Demo And Release Safety

1. Run `publish_bundle_validator`.
2. Run `media_evidence_packager` if a shareable evidence bundle is also needed.
3. Run `release_evidence_packet_seed` or proposed `demo_refresh_sanitization_brief`.

### Release Signal Preparation

1. Run `media_evidence_packager`.
2. Run `evidence_signal_extractor`.
3. Run `human_view_evidence_brief` or `release_evidence_packet_seed`.

### Runtime And Validation Infrastructure Review

1. Run `tool_surface_snapshotter` for declared surfaces.
2. Run `runtime_health_snapshotter` for live proof.
3. Run `validation_infrastructure_readiness_review` or `tool_surface_contract_audit`.

### Factory Export And Catalog Readiness

1. Run `artifact_contract_validator` when an explicit export contract exists.
2. Run `asset_catalog_indexer`.
3. Run `factory_export_readiness_review`.

## Anti-Pattern To Avoid

Do not add more workflow-only notes that:

- manually inspect public versus private bundle boundaries
- manually read test and smoke artifacts into a matrix
- manually probe local health or screenshot endpoints
- manually compile export packs into a catalog table

Those are now the clearest remaining CLI-shaped gaps.
