# Tool Proposal: `evidence_signal_extractor`

- `proposal_id`: `evidence_signal_extractor`
- `recommended_initial_placement`: `tools/experimental_tools/`
- `recommended_maturity`: `experimental`
- `priority`: `high`

## Problem

The current tool layer can collect evidence files, but it does not yet convert mixed artifacts into a normalized signal layer that workflows can summarize quickly. That means release and validation workflows still have to read logs, smoke outputs, manifests, and test files by hand before they can write a matrix or brief.

That gap is visible in:

- `release_evidence_packet_seed`
- `human_view_evidence_brief`
- `validation_infrastructure_readiness_review`

And in sibling project evidence:

- [`../NexusAviationFactory/docs/testing-checklist.md`](<github-workspace>/NexusAviationFactory/docs/testing-checklist.md) defines API, UI, visual validation, and release gating signals that are structured enough to extract.
- [`../AetherVale/AetherVale-tools/README.md`](<github-workspace>/AetherVale/AetherVale-tools/README.md) emphasizes deterministic replay, scene snapshots, and screenshot-oriented QA flows.
- [`../LuminaryInitiatives/docs/executive-summary.md`](<github-workspace>/LuminaryInitiatives/docs/executive-summary.md) keeps `Release Evidence Hub` as the immediate build target and explicitly asks the owner to choose the first evidence source.

## Business Use Case

Prepare a decision-ready release or validation signal layer before a workflow writes the human-facing packet.

Typical examples:

- convert build logs, API smoke outputs, screenshot manifests, and test reports into one readiness matrix
- extract pass, fail, warn, and missing counts from a mixed evidence bundle
- produce a stable summary seed that later workflows can interpret instead of re-parsing raw artifacts

## Why A Tool Is Better Than A Workflow-Only Solution

The heavy work here is structured extraction and normalization:

- parse known report formats such as JSON, XML, CSV, Markdown checklists, and plain-text logs
- classify artifacts by release surface or validation axis
- calculate pass, warn, fail, missing, and stale counts
- preserve source traceability for each extracted signal

A workflow should decide what the signals mean for the business. A CLI should do the repeatable parsing first.

## Suggested Inputs

- evidence bundle root or one or more source directories
- optional `media_evidence_packager` manifest
- parser config covering report formats, required validation axes, and freshness thresholds
- output directory

## Suggested Outputs

- `evidence-signals.json`
- `readiness-matrix.csv`
- `failure-index.json`
- `summary-seed.md`
- optional `stale-evidence.json`

## Core Behaviors

- parse mixed report artifacts into one normalized signal schema
- map extracted signals to release surfaces such as launch, API, visual proof, export proof, and deployment proof
- distinguish present-but-failing evidence from fully missing evidence
- preserve source path, parser type, timestamp, and extracted status for each signal
- emit a deterministic summary seed that stays factual rather than editorial

## Validation Expectations

- every extracted signal points back to a concrete source file
- parser failures are recorded explicitly instead of silently dropping files
- repeated runs over unchanged inputs produce stable signal ids and matrix rows
- the tool stays read-only and never rewrites source evidence

## Workflow Dependencies And Beneficiaries

Use this tool before:

- `release_evidence_packet_seed`
- `human_view_evidence_brief`
- `validation_infrastructure_readiness_review`

It pairs especially well with `media_evidence_packager`, which should gather the files first.

## Non-Goals

- capturing screenshots, logs, or tests
- deciding release readiness by itself
- replacing stakeholder-facing narrative briefs
