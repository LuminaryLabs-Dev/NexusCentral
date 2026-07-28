# Tool Proposal: `asset_catalog_indexer`

- `proposal_id`: `asset_catalog_indexer`
- `recommended_initial_placement`: `tools/experimental_tools/`
- `recommended_maturity`: `experimental`
- `priority`: `high`

## Problem

Factory and asset-oriented workflows repeatedly discuss packaging, preview imagery, manifest quality, and catalog readiness, but the workspace has no reusable CLI that turns those outputs into a normalized product catalog layer.

That gap is visible in:

- `factory_export_readiness_review`

And in sibling project evidence:

- [`../NexusBotanyFactory/docs/product/LOD_AND_EXPORT_SPEC.md`](<github-workspace>/NexusBotanyFactory/docs/product/LOD_AND_EXPORT_SPEC.md) requires tree folders, atlas textures, manifest entries, preview imagery, and Unity import notes for each pack.
- [`../NexusBotanyFactory/docs/product/MASTER_PLAN.md`](<github-workspace>/NexusBotanyFactory/docs/product/MASTER_PLAN.md) calls for a companion packager, Unity-oriented folder layout, metadata, and QA gates before release.
- [`../NexusTextureFactory/NexusTextureFactory-Spinosaurus/docs/toolkit-audit.md`](<github-workspace>/NexusTextureFactory/NexusTextureFactory-Spinosaurus/docs/toolkit-audit.md) already treats catalog and preview surfaces as first-class review targets.

## Business Use Case

Produce a structured catalog of exported assets before commercialization, review, or packaging workflows write their recommendations.

Typical examples:

- build a pack-level index of trees, LOD files, preview images, atlas outputs, and manifest metadata
- generate a CSV or JSON catalog of reviewable assets for a productization pass
- flag missing thumbnails, missing notes, or incomplete metadata before a commercialization brief is written

## Why A Tool Is Better Than A Workflow-Only Solution

This is asset discovery and metadata normalization work:

- walk pack folders and preview directories
- read manifests and companion metadata
- derive consistent catalog rows for each pack or asset
- report missing previews, notes, and metadata fields

A workflow should decide whether the catalog quality is commercially acceptable. The tool should create the catalog first.

## Suggested Inputs

- one or more asset or export roots
- optional manifest and metadata file patterns
- preview and thumbnail file patterns
- output directory

## Suggested Outputs

- `asset-catalog.json`
- `asset-catalog.csv`
- `catalog-summary.md`
- `missing-preview-assets.json`
- optional `metadata-gaps.json`

## Core Behaviors

- inventory packs, folders, preview media, and declared manifests
- normalize asset rows with identifiers, export profile, preview presence, file counts, and source traceability
- roll up pack-level and asset-level completeness summaries
- emit explicit gap reports for missing preview imagery, missing notes, or incomplete metadata
- stay read-only and support multiple export conventions through config

## Validation Expectations

- every catalog row cites at least one source path and one owning pack or grouping
- preview gaps and metadata gaps remain separate finding classes
- CSV and JSON outputs stay aligned on row counts and identifiers
- the source asset directories are never mutated

## Workflow Dependencies And Beneficiaries

Use this tool before:

- `factory_export_readiness_review`

It can also support `release_evidence_packet_seed` when the reviewed payload is an asset pack rather than a web app or service build.

## Non-Goals

- generating previews or thumbnails
- converting formats such as GLB to FBX
- deciding pricing, commercialization rank, or release approval
