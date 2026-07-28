# Tool Lifecycle

## Purpose

This document defines how tool-family inventory, tool-surface planning, and tool proposal material should be classified inside `tools/` so the library stays readable and does not collapse back into workflow-only structure.

## Bucket Definitions

- `tools/experimental_tools/`: draft tool contracts, wrappers, or helper surfaces that still need review
- `tools/final_tools/`: reviewed tool assets that should be extended instead of re-created elsewhere
- `tools/tool_templates/`: reusable templates for tool contracts, wrappers, or interface definitions
- `tools/validation/`: tool-specific validation notes, checklists, or review artifacts
- `archive/`: retired, superseded, or historical tool material that should no longer shape the live library

## Movement Rules

- Seed a candidate in `proposals/tool_proposals/` before treating it as a live tool asset.
- Move a tool note into `tools/experimental_tools/` only when the tool family, contract surface, and review purpose are clear.
- Move a tool note into `tools/final_tools/` only after the asset survives repeated review without needing ongoing contract churn.
- Move tool material into `archive/` when it has been replaced, rejected, or preserved only for historical traceability.

## Proposal Boundary

- Use `tools/` for tool-family inventory and tool-surface planning that has already earned a place in the live library.
- Use `proposals/tool_proposals/` for staged tool proposals that are still pre-library.
- Use `improvements/proposals/` for run-specific follow-up queues discovered during reviews, not as the canonical tool proposal library.

## Current Reviewed State

As of `2026-03-25`, the tool surface is lightly seeded but live:

- `tools/experimental_tools/`: `artifact_contract_validator`, `media_evidence_packager`, `portfolio_signal_normalizer`
- `tools/final_tools/`: `tool_surface_snapshotter`
- `tools/tool_templates/`: seeded
- `tools/validation/`: seeded
- `proposals/tool_proposals/`: preserved as proposal provenance for the seeded tool contracts

This state is intentional. The live tool layer is still smaller and less proven than the workflow layer, but it is now real enough to use `tool_contract` as the house shape for new tool files. Add another tool only when it clearly extends one of the existing goal-linked tool families or a reviewed workflow beneficiary.
