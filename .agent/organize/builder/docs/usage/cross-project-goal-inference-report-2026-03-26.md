# Cross-Project Goal Inference Report

Date: `2026-03-26`

## What Changed In This Pass

- Added missing project coverage for `MuseumMultiverse` and `NexusHardwareFactory`.
- Expanded the factory productization goal and factory export workflow to include `NexusHardwareFactory`.
- Added a new cross-project business goal for structured content parity and traceability based on the new `MuseumMultiverse` scene-parity evidence and the continuing docs-mirror needs in `LuminaryInitiatives` and `LuminaryWiki`.
- Seeded one new live mock workflow and one staged workflow proposal for turning parity evidence into reviewable and sprintable outputs.

## Highest-Value Business Priorities Now

1. `release_evidence_and_demo_readiness`
   - Still the strongest business-facing priority because `LuminaryInitiatives` continues to rank release-evidence work highest and multiple product repos still need stakeholder-safe proof packets.
2. `structured_content_parity_and_traceability`
   - Newly worth elevating because `MuseumMultiverse` now has a large, explicit content-parity review surface with normalized scene groups, issue markers, and missing counterpart evidence.
3. `factory_productization_and_export_quality`
   - Still strong, now broader because `NexusHardwareFactory` exposes exporter-oriented UI without a documented contract, which is the same commercialization gap already visible in the other factory repos.

## Tool-Aware Workflow Guidance

- Use `media_evidence_packager` before `content_parity_traceability_review` and `scene_gap_rebuild_packet` when parity evidence is spread across `Docs/SceneParity`, `Docs/Issues`, screenshots, and external notes.
- Use `artifact_contract_validator` before `factory_export_readiness_review` whenever a factory repo already has an export folder, manifest, or naming contract that can be checked deterministically.
- Keep `portfolio_signal_normalizer` in the loop for portfolio-only mirror drift, but do not force it onto scene-parity work where the current evidence is already organized as manual review docs rather than portfolio records.

## New Workflow Artifacts

- Live mock workflow: `automations/experimental/content_parity_traceability_review.yaml`
- Staged proposal: `proposals/workflow_proposals/scene_gap_rebuild_packet.yaml`

## Current Constraint

- There is still no dedicated scene-inventory or scene-parity CLI in the seeded tool library, so the new content workflows should consume existing parity docs and evidence bundles rather than pretending they can deterministically rediscover every scene relationship from source projects alone.
