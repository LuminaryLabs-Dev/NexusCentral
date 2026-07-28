# Tool And Workflow Composition Examples

## Example 1: Release Evidence Packet

1. Run `media_evidence_packager` against screenshots, test reports, logs, and build notes for a milestone.
2. Feed `manifest.json` and `validation-summary.json` into `release_evidence_packet_seed`.
3. Write the human-facing packet and missing-evidence follow-up note.

Why this split works:

- the tool handles deterministic collection and packaging
- the workflow handles readiness interpretation

## Example 2: Factory Export Readiness

1. Run `artifact_contract_validator` against a generated export folder and its contract file.
2. Feed the validation outputs into `factory_export_readiness_review`.
3. Write the commercialization and readiness queue.

Why this split works:

- the tool handles filesystem and manifest parity
- the workflow handles productization reasoning

## Example 3: Tool Surface Contract Audit

1. Run `tool_surface_snapshotter` across NexusCentral-adjacent repos.
2. Feed the normalized tool catalog and drift report into `tool_surface_contract_audit`.
3. Write the contract-risk summary and narrow follow-up list.

Why this split works:

- the tool handles catalog extraction
- the workflow handles cross-repo drift interpretation

## Example 4: Portfolio Signal Refresh

1. Run `portfolio_signal_normalizer` across LuminaryInitiatives and related mirrors.
2. Feed normalized records and mirror drift output into `portfolio_signal_refresh`.
3. Write the owner-facing refresh note and minimal fix queue.

Why this split works:

- the tool handles source normalization
- the workflow handles business prioritization

