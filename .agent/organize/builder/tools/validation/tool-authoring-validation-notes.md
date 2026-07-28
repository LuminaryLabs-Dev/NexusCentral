# Tool Authoring Validation Notes

Date: 2026-03-28
Status: current guidance grounded in the seeded tool and workflow corpus

## Current State

The live tool layer is still small, but it is real enough that new tool work should follow the existing contract exactly:

- experimental: `media_evidence_packager`
- experimental: `artifact_contract_validator`
- experimental: `portfolio_signal_normalizer`
- final: `tool_surface_snapshotter`

The main validation question remains:

- is the candidate extending an already-seeded deterministic family that workflows need, or is it inventing a broader runtime contract than the repo actually proves?

## Current House Tool Shape

Before validating a new tool, confirm it follows the current `tool_contract` order:

- `version`
- `kind`
- `metadata`
- `summary`
- `goal_links`
- `scope`
- `workflow_relationships`
- `package_assets`
- `commands`
- `global_output_contract`
- `build_plan`
- `promotion`

Observed metadata expectations:

- `id` matches the filename
- `status` matches bucket placement
- `maturity` explains readiness more precisely than status alone
- `owner` stays `nexus-central`
- `implementation_status` is currently `specification_only`
- `proposal_source` points to a real proposal note under `proposals/tool_proposals/`

Observed live-tool norms:

- every current tool has a matching markdown proposal
- every current tool has a matching validation note under `tools/validation/`
- every current tool names at least one beneficiary workflow target
- every current tool ships with a matching template or contract seed under `tools/tool_templates/`
- three current tools expose two commands and one exposes one command
- all current tools are read-only against source inputs
- all current tools write under the selected output directory only
- current tools are strongest when they stay contract-first and review-friendly

## Validate New Tool Assets Against These Rules

- Proposal-first before spec-first: preserve the idea in `proposals/tool_proposals/` before adding live YAML.
- Family-first before novelty-first: extend an existing tool lane when possible.
- Beneficiary-first before executor-first: name the workflows that will consume the outputs.
- Package-assets-first before implementation detail: provide a template file, validation note path, and usage docs path.
- Output-contract-first before algorithm detail: workflows should be able to rely on filenames and output layout without guessing.
- Command-minimal before command-sprawl: keep the callable surface narrow unless a concrete beneficiary requires expansion.

## Current Tool Families

- `media_evidence_packager` and `artifact_contract_validator` for evidence packaging, artifact inspection, contract validation, and partitioned report emission
- `tool_surface_snapshotter` for declared tool-surface inventory and drift
- `portfolio_signal_normalizer` for deterministic portfolio extraction and mirror comparison

What is still only a gap signal:

- `runtime_health_snapshotter` is a proposal-backed hole in the observability lane, not a live `tool_contract`

If a candidate does not fit one of the live lanes cleanly, keep it as a proposal until the family boundary is clearer.

## Promotion And Staging Rules

Use this progression:

1. markdown proposal under `proposals/tool_proposals/`
2. live contract under `tools/experimental_tools/` when the callable surface is clear
3. final placement under `tools/final_tools/` only after repeated review proves the contract is stable

Observed related workflow rule:

- tool-linked workflows can move from `proposals/workflow_proposals/` into `automations/experimental/` only after the supporting tool contract and validation note exist
- current repo evidence for that pattern includes `demo_refresh_sanitization_brief` and `tool_wrapper_onboarding_brief`
- keep tool-input-shaping workflows staged when they are still upstream of stable config or contract schemas, as with `artifact_contract_profile_seed`

## Minimal Review Gate For A New Tool

Before adding a file under `tools/experimental_tools/` or `tools/final_tools/`, confirm:

- a proposal exists and is worth preserving
- the repeated manual work is deterministic enough to justify a tool
- the nearest workflow family or workflow targets are named explicitly
- a matching validation note can exist under `tools/validation/`
- a matching template or contract seed can exist under `tools/tool_templates/`
- the tool does not force a new top-level schema beyond `tool_contract`
- the proposed outputs are stable enough for workflows to consume later

## Promotion Gate For Final Placement

Before moving a tool from `tools/experimental_tools/` to `tools/final_tools/`, confirm:

- output filenames and layout survived repeated fixture review
- the command surface stopped expanding
- downstream workflow beneficiaries stayed stable
- the validation note names the remaining risks explicitly
- the tool still fits the narrow, contract-first style already proven by `tool_surface_snapshotter`

## What Tool-Linked Workflows Should Validate

- which workflow family or goal family the tool supports
- which tool contract and validation note define the baseline
- which emitted files the workflow expects to interpret
- whether the workflow is interpreting stable tool output or still shaping future tool inputs
- what follow-up work remains after the tool-backed evidence is reviewed

## What Tool-Linked Workflows Should Not Validate Yet

- live execution guarantees
- scheduling semantics
- endpoint mutation or remediation behavior
- machine-state contracts the repo does not already prove
- broad release-management logic that belongs in workflows, not tools

## Current Uncertainties To Keep Explicit

- only four live tool contracts exist, so house style is real but still narrow
- `project_goals` are not yet established in live tool contracts
- runtime probing is still proposal-backed rather than live
- final placement is proven only for the read-only cataloging lane today

When those uncertainties matter, say so directly in the proposal, workflow notes, or validation note instead of smoothing them over.
