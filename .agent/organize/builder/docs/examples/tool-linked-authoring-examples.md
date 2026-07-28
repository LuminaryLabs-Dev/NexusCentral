# Tool-Linked Authoring Examples

Date: 2026-03-28
Status: example-only reference aligned to the current seeded corpus

Use these compact examples as style references, not as live specs.

## Example 1: Workflow Referencing An Existing Tool

Suggested family extension:

- tool-aware interpretation

Suggested placement:

- `automations/experimental/`

```yaml
version: 1
kind: workflow_spec
metadata:
  id: tool_wrapper_onboarding_brief
  name: Tool Wrapper Onboarding Brief
  status: experimental
  owner: nexus-central
  implementation_status: specification_only
  tags:
    - tooling
    - onboarding
    - contracts
summary:
  purpose: Turn tool-surface snapshots into a wrapper and operator onboarding brief without manually recompiling the same contract packet.
  offloads:
    - repeated wrapper-onboarding note drafting across tool-centric repos
    - repeated manual compilation of routes, wrappers, and discovery cues into one handoff packet
goal_links:
  project_goals:
    - nexus_central_enforce_tool_namespace_contracts
  business_goals:
    - tool_surface_contract_and_runtime_observability
inputs:
  required:
    - name: workspace_root
      type: path
      description: Root of the Nexus Central Builder workspace.
    - name: target_project
      type: string
      description: Tool-centric project that needs an onboarding packet.
  optional:
    - name: supporting_tools
      type: list[string]
      default:
        - tool_surface_snapshotter
      description: Existing tool contracts that should back the onboarding brief.
    - name: tool_report_paths
      type: list[path]
      default:
        - reports/tool-catalog.json
        - reports/drift-report.json
        - reports/missing-contract-signals.json
      description: Snapshot outputs that should be interpreted before drafting the onboarding packet.
reads:
  required:
    - tools/final_tools/tool_surface_snapshotter.yaml
    - tools/validation/tool_surface_snapshotter-validation.md
    - docs/usage/tool-package-call-order.md
writes:
  primary:
    - path: docs/usage/{project_slug}-tool-wrapper-onboarding-brief.md
      description: Main onboarding packet explaining routes, wrappers, health cues, and open contract gaps.
  secondary:
    - path: artifacts/{project_slug}-tool-onboarding-catalog.md
      description: Supporting catalog of routes, schemas, examples, and missing discovery cues.
    - path: improvements/proposals/{project_slug}-tool-wrapper-followups.md
      description: Narrow follow-up queue for missing docs, snapshot drift, or onboarding blockers.
steps:
  - id: load-tool-backed-contract-evidence
    objective: Reuse current snapshot outputs instead of recompiling tool surfaces by hand.
    actions:
      - Read the tool catalog, drift report, and missing-contract signals if they exist.
      - Separate tool-backed facts from remaining manual interpretation.
validation:
  checks:
    - The workflow cites the live tool contract and validation note.
    - The workflow interprets snapshot output instead of re-stating extractor logic.
handoff:
  upstream:
    - tool_surface_contract_audit
  downstream:
    - workflow_review_gate
notes:
  - The tool extracts the surface; the workflow explains how to onboard against it.
```

## Example 2: Tool Proposal Note

Suggested family extension:

- proposal-backed runtime-observability gap, not yet a live tool family

Suggested placement:

- `proposals/tool_proposals/runtime_health_snapshotter.md`

```md
# Tool Proposal: `runtime_health_snapshotter`

- `proposal_id`: `runtime_health_snapshotter`
- `recommended_initial_placement`: `tools/experimental_tools/`
- `recommended_maturity`: `experimental`
- `priority`: `high`

## Problem

`tool_surface_snapshotter` catalogs declared routes and wrappers, but it does not prove whether local health or validation endpoints actually answer.

That gap is visible in:

- `validation_infrastructure_readiness_review`
- `tool_surface_contract_audit`

## Business Use Case

Capture live runtime evidence before a workflow writes a readiness or drift brief.

## Why A Tool Is Better Than A Workflow-Only Solution

The repeated work is probe execution and normalization:

- call read-safe endpoints
- record status, latency, content type, and response metadata
- emit stable runtime evidence for later review

## Suggested Inputs

- probe config in YAML or JSON
- optional previous snapshot
- output directory

## Suggested Outputs

- `health-snapshot.json`
- `probe-results.json`
- `runtime-drift.json`
- `probe-summary.md`

## Core Behaviors

- execute read-only probes
- save normalized runtime results
- separate transport failures from contract mismatches

## Validation Expectations

- every failed probe cites the exact endpoint and mismatch reason
- repeated runs over unchanged services stay stable enough for review

## Workflow Dependencies And Beneficiaries

Use this tool before:

- `validation_infrastructure_readiness_review`
- `tool_surface_contract_audit`

## Non-Goals

- mutating runtime state
- load testing
- replacing readiness interpretation
```

## Example 3: New Tool-Linked Workflow Shape

Suggested family extension:

- tool-input staging

Suggested placement:

- `proposals/workflow_proposals/`

```yaml
version: 1
kind: workflow_spec
metadata:
  id: artifact_contract_profile_seed
  name: Artifact Contract Profile Seed
  status: experimental
  owner: nexus-central
  implementation_status: specification_only
  tags:
    - contracts
    - tooling
    - seeding
summary:
  purpose: Turn repo-specific bundle promises into a reviewable artifact-contract and evidence-profile brief before validator and packager configs are treated as stable reusable assets.
  offloads:
    - repeated manual drafting of artifact contract rules before output validation can happen
    - repeated translation of bundle promises into provisional tool-input profiles
goal_links:
  project_goals:
    - project_arrowhead_demo_build_safe_public_demo_payloads
  business_goals:
    - factory_productization_and_export_quality
    - release_evidence_and_demo_readiness
inputs:
  required:
    - name: workspace_root
      type: path
      description: Root of the Nexus Central Builder workspace.
    - name: target_project
      type: string
      description: Project whose bundle or evidence set needs a contract-profile seed.
  optional:
    - name: supporting_tools
      type: list[string]
      default:
        - artifact_contract_validator
        - media_evidence_packager
      description: Tool contracts whose future config inputs this workflow would help define.
reads:
  required:
    - tools/experimental_tools/artifact_contract_validator.yaml
    - tools/experimental_tools/media_evidence_packager.yaml
    - tools/validation/artifact_contract_validator-validation.md
    - tools/validation/media_evidence_packager-validation.md
writes:
  primary:
    - path: docs/usage/{project_slug}-artifact-contract-profile-seed.md
      description: Main brief describing draft contract rules, evidence classes, and unresolved schema questions.
  secondary:
    - path: artifacts/{project_slug}-artifact-contract-profile-matrix.md
      description: Supporting matrix mapping repo promises to validator rules and packager classes.
    - path: improvements/proposals/{project_slug}-artifact-contract-profile-followups.md
      description: Narrow follow-up queue for schema ambiguity or missing source evidence.
steps:
  - id: inspect-tool-input-needs
    objective: Confirm which bundle rules belong to validation and which belong to evidence packaging.
    actions:
      - Read the current tool contracts before assuming one profile can cover both tools cleanly.
      - Keep provisional schema assumptions visible.
validation:
  checks:
    - The brief distinguishes validator-facing rules from packager-facing profile sections.
    - The workflow stays seeding-focused and does not pretend to replace tool execution.
handoff:
  upstream:
    - release_evidence_packet_seed
  downstream:
    - workflow_review_gate
notes:
  - Keep this shape in proposal space until the tool-input schemas are stable enough to support a live library workflow.
```

## Quick Rule

- If the tool contract and validation note already exist, a tool-linked workflow can live in `automations/experimental/`.
- If the workflow is still shaping tool inputs or depends on a not-yet-live tool, keep it in `proposals/workflow_proposals/`.
