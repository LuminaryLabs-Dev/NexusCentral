# Tool Proposal Summary

Date: `2026-03-25`

## Current State

- `tools/experimental_tools/` now contains three promoted tool contracts.
- `tools/final_tools/` now contains one promoted final-ready tool contract.
- `tools/tool_templates/` is seeded.
- `tools/validation/` is seeded.
- `proposals/tool_proposals/` now contains proposal records preserved as provenance for the promoted tool contracts.

The strongest current gap is no longer the absence of a tool layer. The first tool layer now exists, and the main remaining work is validating the promoted contracts and deciding whether any additional staged proposals are needed beyond the existing seeded set.

## Priority Order

### 1. `media_evidence_packager`

This is the strongest immediate opportunity.

Why first:

- directly supports the highest-priority business goal in [`goals/business_goals/release_evidence_and_demo_readiness.md`](../../goals/business_goals/release_evidence_and_demo_readiness.md)
- fits the Release Evidence Hub direction in [`../LuminaryInitiatives/docs/executive-summary.md`](<github-workspace>/LuminaryInitiatives/docs/executive-summary.md)
- turns mixed screenshots, logs, tests, manifests, and notes into a repeatable bundle instead of a bespoke doc exercise

Workflows that benefit:

- `release_evidence_packet_seed`
- `human_view_evidence_brief`
- `factory_export_readiness_review`

Tool-before-workflow boundary:

- tool collects, normalizes, validates, and manifests the evidence set
- workflow decides what evidence matters, what is missing, and what follow-up should be queued

### 2. `artifact_contract_validator`

Why second:

- factory and demo repos already define output contracts, but validation is still manual or repo-local
- it supports export quality, demo safety, and report preparation without inventing new product logic

Workflows that benefit:

- `factory_export_readiness_review`
- `release_evidence_packet_seed`

Tool-before-workflow boundary:

- tool checks actual outputs against required file and manifest rules
- workflow interprets failures in business terms such as readiness, commercialization blockers, or missing proof

### 3. `tool_surface_snapshotter`

Why third:

- the workflow target already exists, but it depends on structured discovery that should not be re-authored in prose every run
- this is the cleanest final-ready candidate because it is read-only, narrow, and catalog-focused

Workflows that benefit:

- `tool_surface_contract_audit`
- future tool-catalog maintenance workflows

Tool-before-workflow boundary:

- tool extracts routes, manifests, schemas, wrappers, and health signals
- workflow explains drift risk, missing docs, and priority of fixes

### 4. `portfolio_signal_normalizer`

Why fourth:

- it is data-oriented and organizationally valuable, but it is one step less urgent than release evidence and tool contract gaps
- it becomes more valuable once portfolio mirror drift starts costing owner trust or repeated refresh time

Workflows that benefit:

- `portfolio_signal_refresh`
- proposal and commercialization backlog shaping

Tool-before-workflow boundary:

- tool normalizes source records and flags drift
- workflow summarizes the posture and recommends action

## Main Findings

- The workflow library is planning-heavy and intentionally non-executing.
- The current business goals repeatedly point to evidence packaging, validation, contract discovery, and normalization.
- Those problems are structured, repeatable, and machine-friendly enough that a CLI tool should precede prose-heavy workflows.
- The strongest business-facing opportunities are concentrated in release proof, demo packaging, export validation, and tool-surface cataloging.

## Recommendation

Keep the current promotion order:

1. `media_evidence_packager` in `tools/experimental_tools/`
2. `artifact_contract_validator` in `tools/experimental_tools/`
3. `tool_surface_snapshotter` in `tools/final_tools/`
4. `portfolio_signal_normalizer` in `tools/experimental_tools/`

The next step is not another proposal wave. It is validation: prove the experimental tool contracts against realistic fixtures and keep `tool_surface_snapshotter` stable as the final-ready baseline.
