# Tool Proposal: `runtime_health_snapshotter`

- `proposal_id`: `runtime_health_snapshotter`
- `recommended_initial_placement`: `tools/experimental_tools/`
- `recommended_maturity`: `experimental`
- `priority`: `high`

## Problem

`tool_surface_snapshotter` inventories declared routes, wrappers, and schemas, but it intentionally does not call live endpoints. That leaves a real gap for workflows that need evidence about whether local runtime and validation surfaces are actually answering as expected.

That gap is visible in:

- `validation_infrastructure_readiness_review`
- `tool_surface_contract_audit`

And in sibling project evidence:

- [`../NexusAviationFactory/README.md`](<github-workspace>/NexusAviationFactory/README.md) declares `GET /health` plus screenshot routes that matter for validation readiness.
- [`../NexusAgentChat/NexusAgentChat-Stegosaurus-0.0.1/README.md`](<github-workspace>/NexusAgentChat/NexusAgentChat-Stegosaurus-0.0.1/README.md) exposes `GET /api/health` and `GET /api/runtime` for external validation.
- [`../human-view-python/README.md`](<github-workspace>/human-view-python/README.md) centers screenshot capture and a local companion API.
- [`../NexusTextureFactory/NexusTextureFactory-Spinosaurus/docs/toolkit-audit.md`](<github-workspace>/NexusTextureFactory/NexusTextureFactory-Spinosaurus/docs/toolkit-audit.md) shows a categorized local toolkit surface where health and catalog endpoints are operational evidence, not just documentation.

## Business Use Case

Capture live runtime and validation-surface evidence before a workflow writes a readiness or drift report.

Typical examples:

- verify that expected health, runtime, screenshot, and catalog endpoints respond with the correct status and content type
- snapshot sample JSON or SVG payloads from validation helpers for later audit review
- compare the current runtime surface against a previous snapshot to detect operational drift

## Why A Tool Is Better Than A Workflow-Only Solution

The repeated work is probe execution and normalization:

- call known local endpoints using declared methods and sample payloads
- capture status, latency, content type, body hash, and selected response fields
- save sample payloads for later review
- compare current responses against expected cues or a prior snapshot

A workflow should rank the operational risk. The tool should produce the repeatable runtime evidence first.

## Suggested Inputs

- YAML or JSON probe config listing base URLs, routes, methods, expected content types, optional sample payloads, and timeouts
- optional previous snapshot
- output directory

## Suggested Outputs

- `health-snapshot.json`
- `probe-results.json`
- `runtime-drift.json`
- `probe-summary.md`
- `endpoint-samples/`

## Core Behaviors

- execute read-only HTTP probes against configured local or controlled runtime surfaces
- record normalized response metadata including status, latency, content type, and response hash
- persist bounded sample payloads such as JSON, SVG, or text for review
- compare results against declared expectations and optional prior snapshots
- separate transport failures, contract mismatches, and content drift into distinct finding types

## Validation Expectations

- only read-safe probes are allowed by default
- every failed probe cites the exact endpoint, method, and mismatch reason
- sample capture stays size-bounded and predictable
- repeated runs against unchanged services produce stable normalized outputs

## Workflow Dependencies And Beneficiaries

Use this tool before:

- `validation_infrastructure_readiness_review`
- `tool_surface_contract_audit`

It can also strengthen `release_evidence_packet_seed` when release proof depends on live health or screenshot routes.

## Non-Goals

- load testing or performance benchmarking
- mutating runtime state or calling destructive routes
- replacing wrapper or route catalog extraction
