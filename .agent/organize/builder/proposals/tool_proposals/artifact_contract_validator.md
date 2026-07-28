# Tool Proposal: `artifact_contract_validator`

- `proposal_id`: `artifact_contract_validator`
- `recommended_initial_placement`: `tools/experimental_tools/`
- `recommended_maturity`: `experimental`
- `priority`: `high`

## Problem

Several workflows describe export-readiness, packaging, manifest checks, and demo-output review, but the workspace has no reusable CLI that validates whether an output directory actually satisfies a declared artifact contract.

That gap is visible in:

- `factory_export_readiness_review`
- `release_evidence_packet_seed`

And in sibling project evidence:

- [`../NexusBotanyFactory/docs/product/LOD_AND_EXPORT_SPEC.md`](<github-workspace>/NexusBotanyFactory/docs/product/LOD_AND_EXPORT_SPEC.md) defines exact LOD and pack output expectations.
- [`../NexusBotanyFactory/docs/product/MASTER_PLAN.md`](<github-workspace>/NexusBotanyFactory/docs/product/MASTER_PLAN.md) requires manifest validation before export milestones are accepted.
- [`../ProjectArrowhead-Demo/README.md`](<github-workspace>/ProjectArrowhead-Demo/README.md) and [`../ProjectArrowhead-Demo/outputs-to-externalize/README.md`](<github-workspace>/ProjectArrowhead-Demo/outputs-to-externalize/README.md) define a strict split between public demo outputs and private generated artifacts.
- [`../NexusAviationFactory/docs/testing-checklist.md`](<github-workspace>/NexusAviationFactory/docs/testing-checklist.md) requires screenshots and API smoke outputs as release gates.

## Business Use Case

Validate that generated asset bundles, demo payloads, or packaged outputs match their promised contract before a workflow writes a readiness brief.

Typical examples:

- verify a 10-tree Unity pack contains all required `LOD0` through `LOD3` files plus manifests and preview imagery
- verify a demo payload includes public outputs while private generated files remain externalized
- verify screenshot loops or export runs produced the expected number of files and naming patterns

## Why A Tool Is Better Than A Workflow-Only Solution

This is a schema and file-system validation problem, not just a planning problem. A tool can:

- enforce counts, naming, file presence, and directory shape consistently
- compare an output folder against a declared contract file
- emit precise machine-readable failures
- reduce false confidence from prose-based review notes

A workflow should interpret the result, not manually perform the inspection every time.

## Suggested Inputs

- target artifact directory
- contract file in JSON or YAML
- optional manifest file to cross-check
- optional preview-asset rules
- output directory for reports

## Suggested Outputs

- `contract-validation.json`
- `contract-validation.md`
- `missing-files.json`
- `unexpected-files.json`
- optional normalized `inventory.json`

## Core Behaviors

- validate required files, counts, patterns, and folder layout
- validate manifest-to-filesystem parity
- support public/private partition rules
- support per-asset metadata checks such as label, type, or profile
- return machine-readable severity levels so workflows can gate on errors versus warnings

## Validation Expectations

- all contract checks are rule-driven rather than hard-coded to one repo
- every failure cites the exact missing or mismatched path
- the tool remains read-only against the validated output directory

## Workflow Dependencies And Beneficiaries

Use this tool before:

- `factory_export_readiness_review`
- `release_evidence_packet_seed`

It is especially relevant for Botany, Aviation, Texture, Arrowhead demo, and future Release Evidence Hub pipelines.

## Non-Goals

- building the artifact bundle
- fixing the outputs automatically
- replacing product-specific export code

