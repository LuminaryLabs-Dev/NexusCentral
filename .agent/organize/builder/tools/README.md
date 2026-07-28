# Tool Library

## Purpose

This directory is the canonical home for tool-family inventory, tool-surface planning notes, templates, validation notes, and later tool contract records.

## Current State

As of `2026-03-25`, the first live tool-contract layer has been seeded:

- `tools/experimental_tools/`: `media_evidence_packager`, `artifact_contract_validator`, and `portfolio_signal_normalizer`
- `tools/final_tools/`: `tool_surface_snapshotter`

The supporting authoring layer is also present:

- `tools/tool_templates/`: reusable tool proposal, contract, and validation scaffolds
- `tools/validation/`: generic guidance plus per-tool review notes

Tool planning still enters through staged tool proposals, goals, and goal-linked workflows, but the selected proposals now have concrete tool contracts ready for implementation planning and promotion review.

Current upstream planning signals:

- `tool_surface_contract_audit`: primary consumer of `tool_surface_snapshotter`
- `human_view_evidence_brief` plus `release_evidence_packet_seed`: primary consumers of `media_evidence_packager`
- `factory_export_readiness_review`: primary consumer of `artifact_contract_validator` and secondary consumer of `media_evidence_packager`
- `portfolio_signal_refresh`: primary consumer of `portfolio_signal_normalizer`

## Bucket Use

- `experimental_tools/`: draft tool contracts, wrappers, or helper surfaces that still need review
- `final_tools/`: reviewed tool assets that should be extended instead of re-created elsewhere
- `tool_templates/`: reusable tool scaffolds
- `validation/`: tool-specific validation notes and review aids

Read [`docs/conventions/tool-lifecycle.md`](../docs/conventions/tool-lifecycle.md) before adding anything here.
