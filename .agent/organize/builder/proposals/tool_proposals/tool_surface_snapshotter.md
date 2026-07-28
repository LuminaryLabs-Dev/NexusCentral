# Tool Proposal: `tool_surface_snapshotter`

- `proposal_id`: `tool_surface_snapshotter`
- `recommended_initial_placement`: `tools/final_tools/`
- `recommended_maturity`: `final-ready`
- `priority`: `high`

## Problem

The workspace already has a `tool_surface_contract_audit` workflow target, but the actual work it depends on is deterministic surface discovery:

- routes
- manifests
- schemas
- health endpoints
- wrapper outputs
- CLI entrypoints

Right now that discovery exists only as reading and comparison labor.

The business need is visible in:

- [`goals/business_goals/tool_surface_contract_and_runtime_observability.md`](../../goals/business_goals/tool_surface_contract_and_runtime_observability.md)
- [`automations/experimental/tool_surface_contract_audit.yaml`](../../automations/experimental/tool_surface_contract_audit.yaml)

And in sibling repos:

- [`../NexusCentral/docs/integration_model.md`](<github-workspace>/NexusCentral/docs/integration_model.md) defines integration and tool schemas plus route conventions.
- [`../NexusIndexAgent-Stegosaurus/README.md`](<github-workspace>/NexusIndexAgent-Stegosaurus/README.md) describes a wrapper artifact and schema-driven output.
- [`../NexusUniverse/NexusUniverse-Stegosaurus/README.md`](<github-workspace>/NexusUniverse/NexusUniverse-Stegosaurus/README.md) defines separate UI and read-only tools ports.
- [`../NexusTextureFactory/NexusTextureFactory-Spinosaurus/docs/toolkit-audit.md`](<github-workspace>/NexusTextureFactory/NexusTextureFactory-Spinosaurus/docs/toolkit-audit.md) lists categorized toolkit endpoints.

## Business Use Case

Produce a reliable, normalized catalog of tool surfaces before audits, wrapper onboarding, runtime support handoffs, or contract-drift reviews.

Typical examples:

- snapshot all tool manifests and routes for a release note or integration review
- compare wrapper outputs against the declared NexusCentral contract
- inventory local health and discovery surfaces across a tool-heavy repo set

## Why A Tool Is Better Than A Workflow-Only Solution

This is structured extraction and normalization work. A CLI tool can:

- parse manifests and schema files consistently
- discover route and health patterns across repos
- emit one normalized catalog format
- support repeatable diffs between snapshots

That makes the later workflow smaller and better grounded: the workflow explains risk and priority, while the tool provides the evidence inventory.

## Suggested Inputs

- repo roots
- file patterns for manifests, schemas, route definitions, and README hints
- optional extraction config describing known route prefixes or wrapper file names
- optional previous snapshot for diff mode

## Suggested Outputs

- `tool-catalog.json`
- `tool-catalog.md`
- `drift-report.json`
- `missing-contract-signals.json`

## Core Behaviors

- discover integration manifests, wrapper descriptors, schema files, route declarations, and health endpoints
- normalize them into a consistent catalog shape
- flag missing descriptions, schemas, health cues, or discovery docs
- compare current and prior snapshots to isolate contract drift
- stay read-only and usable across multiple repo conventions

## Why This Can Start Final-Ready

The scope is narrow, non-destructive, and catalog-oriented. It does not mutate runtime state or promise universal semantic understanding. It only extracts and normalizes declared surface evidence, which is already a stable recurring need.

## Workflow Dependencies And Beneficiaries

Use this tool before:

- `tool_surface_contract_audit`
- `workflow_catalog_refresh` when tool-facing workflows expand

## Non-Goals

- calling live runtime endpoints
- enforcing auth or remediation
- replacing a human contract review

