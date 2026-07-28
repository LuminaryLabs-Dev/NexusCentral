# Tool-First Release And Catalog Pipelines

## 1. Public Demo Refresh

Use this sequence when the core risk is whether a shareable build is actually safe to publish:

1. `publish_bundle_validator`
2. `media_evidence_packager`
3. `release_evidence_packet_seed`
4. proposed `demo_refresh_sanitization_brief`

Why this order:

- `publish_bundle_validator` proves the public payload boundary first
- `media_evidence_packager` gathers the supporting proof
- the workflows explain business impact and next actions instead of rediscovering files

## 2. Mixed Evidence To Release Brief

Use this sequence when screenshots, logs, tests, and API checks already exist but the release packet is still manual:

1. `media_evidence_packager`
2. `evidence_signal_extractor`
3. `human_view_evidence_brief`
4. `release_evidence_packet_seed`

Why this order:

- the packager gathers the raw proof
- the extractor turns it into structured release signals
- the workflows write the human-facing brief and follow-up queue

## 3. Live Runtime Review

Use this sequence when declared routes exist but operational confidence is still weak:

1. `tool_surface_snapshotter snapshot`
2. `runtime_health_snapshotter`
3. `tool_surface_contract_audit`
4. `validation_infrastructure_readiness_review`

Why this order:

- the snapshotter catalogs what the repo says exists
- the runtime snapshotter proves what actually answers now
- the workflows explain drift, readiness, and repair priority

## 4. Factory Pack Catalog Review

Use this sequence when export folders, previews, and manifests exist but catalog readiness is still hand-reviewed:

1. `artifact_contract_validator`
2. `asset_catalog_indexer`
3. `factory_export_readiness_review`

Why this order:

- the validator checks declared export completeness
- the catalog indexer converts the outputs into a reviewable commercial surface
- the workflow decides whether the pack is credible enough for the next productization step
