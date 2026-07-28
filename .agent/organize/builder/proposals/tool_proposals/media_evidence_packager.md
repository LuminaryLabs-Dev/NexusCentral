# Tool Proposal: `media_evidence_packager`

- `proposal_id`: `media_evidence_packager`
- `recommended_initial_placement`: `tools/experimental_tools/`
- `recommended_maturity`: `experimental`
- `priority`: `highest`

## Problem

Current workflows can describe what release or review evidence should exist, but they do not give the workspace a deterministic way to collect mixed media and structured artifacts into one reusable bundle.

That gap shows up across:

- `release_evidence_packet_seed`
- `human_view_evidence_brief`
- `factory_export_readiness_review`

It is also visible in sibling project evidence:

- [`../ProjectArrowhead-Demo/README.md`](<github-workspace>/ProjectArrowhead-Demo/README.md) describes a sanitized `build/` plus private generated artifacts.
- [`../NexusBotanyFactory/docs/product/HUMAN_VIEW_VALIDATION.md`](<github-workspace>/NexusBotanyFactory/docs/product/HUMAN_VIEW_VALIDATION.md) requires screenshots, folder proof, and Unity import evidence.
- [`../AetherVale/AetherVale-tools/README.md`](<github-workspace>/AetherVale/AetherVale-tools/README.md) centers replay, validation, and screenshot-oriented QA.

## Business Use Case

Support release evidence, demo readiness, stakeholder review packets, and milestone proof across web, game, factory, and device projects.

Typical examples:

- assemble screenshots, test outputs, logs, manifests, and build notes for a release candidate
- prepare a sanitized demo evidence bundle for external review
- gather human-view validation proof for UI-heavy milestones

## Why A Tool Is Better Than A Workflow-Only Solution

A workflow can explain what to gather. A CLI tool can actually perform the repetitive, deterministic part:

- collect files by rule
- normalize naming
- compute hashes and timestamps
- validate required evidence classes
- generate a machine-readable manifest
- copy or stage the bundle in a stable layout

Without that tool layer, every workflow run redoes the same file discovery and packaging logic in prose.

## Suggested Inputs

- one or more source directories
- include and exclude glob patterns
- evidence class map such as `screenshots`, `videos`, `logs`, `tests`, `manifests`, `notes`
- optional metadata file describing milestone, repo, reviewer, and release id
- optional sanitization or redaction rule file
- output directory

## Suggested Outputs

- packaged evidence bundle directory
- `manifest.json`
- `manifest.md`
- `validation-summary.json`
- `missing-evidence.json`

## Core Behaviors

- scan mixed media and structured data from multiple roots
- normalize bundle layout by evidence class
- capture size, hash, mtime, source path, and logical label for each asset
- fail or warn when required evidence classes are absent
- optionally copy private-only artifacts into a separate non-public partition
- emit bundle-ready metadata that later workflows can summarize instead of rediscovering

## Validation Expectations

- every bundled asset is listed exactly once in `manifest.json`
- required evidence classes are either satisfied or explicitly reported missing
- public and private outputs are kept separate when sanitization rules are present
- the tool is non-destructive to source files

## Workflow Dependencies And Beneficiaries

Use this tool before:

- `release_evidence_packet_seed`
- `human_view_evidence_brief`
- `factory_export_readiness_review`

Those workflows should consume the manifest and validation summary instead of inventing bundle structure manually.

## Non-Goals

- generating screenshots or running tests
- editing source artifacts
- deciding business readiness on its own

