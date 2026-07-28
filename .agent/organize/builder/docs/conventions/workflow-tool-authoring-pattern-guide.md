# Workflow And Tool Authoring Pattern Guide

Date: 2026-03-28
Status: inferred from the current seeded builder corpus

## Purpose

Use this guide before adding mock workflows, tool proposals, or tool-linked workflows. The builder already has a real authoring style. Extend that style instead of inventing a second model.

## Source Basis

This pass reviewed:

- `automations/active/`
- `automations/experimental/`
- `automations/disabled/`
- `templates/workflow_templates/`
- `templates/validation/`
- `tools/experimental_tools/`
- `tools/final_tools/`
- `tools/tool_templates/`
- `tools/validation/`
- `proposals/tool_proposals/`
- `proposals/workflow_proposals/`
- `docs/conventions/`
- `docs/examples/`
- `docs/usage/`
- all current `.yaml` and `.yml` files under the workspace

Observed corpus size that matters for authoring today:

- 31 live workflow specs under `automations/`
- 4 staged workflow YAML files under `proposals/workflow_proposals/`
- 6 workflow templates under `templates/workflow_templates/`
- 4 live `tool_contract` files
- 5 live tool YAML templates
- 5 tool validation notes

## Stable House Conventions

### Workflow YAML

Across the 31 live workflow specs, these patterns are stable enough to treat as house style:

- every file uses `version: 1`
- every file uses `kind: workflow_spec`
- every file uses `metadata.owner: nexus-central`
- every file uses `metadata.implementation_status: specification_only`
- every file includes both `handoff` and `notes`
- `metadata.status` matches folder placement for `active`, `experimental`, and `disabled`
- filenames match `metadata.id` and stay lowercase snake_case
- `metadata.name` is short and outcome-focused
- tags are lowercase and category-first
- step ids are lowercase and hyphenated
- placeholders belong in output paths, not in ids
- `workspace_root` is the first required input in 31 of 31 live workflow specs
- `reads` appears in 28 of 31 live workflow specs, so it is common but still optional
- `goal_links` appears in 11 of 31 live workflow specs, mostly in cross-repo and business-facing review families
- multiple primary outputs appear in only 3 of 31 live workflow specs, so paired primaries are allowed but not the default

Observed workflow section order:

```yaml
version
kind
metadata
summary
goal_links   # optional
inputs
reads        # optional
writes
steps
validation
handoff
notes
```

Observed write destinations:

- `docs/usage/` for the main brief, review note, report, or contract note
- `artifacts/` for matrices, inventories, manifests, and supporting evidence
- `improvements/proposals/` for follow-up queues
- `docs/examples/` for example-only output
- `automations/experimental/` only when the workflow is seeding another mock workflow

Observed input and output style:

- required inputs describe the review subject, not runtime internals
- optional inputs usually shape scope, comparison axes, scan roots, report modes, or supporting tool context
- `writes.primary` holds the main reviewed artifact
- `writes.secondary` holds matrices, examples, and deferred follow-ups
- workflows stay report-first and non-destructive

### Status And Placement Handling

Placement is path-driven first.

- live reviewable workflow specs belong in `automations/`
- staged workflow ideas belong in `proposals/workflow_proposals/`
- tool ideas belong in markdown under `proposals/tool_proposals/`

Important current nuance:

- staged workflow YAML files under `proposals/workflow_proposals/` still use `kind: workflow_spec`
- those staged files still say `metadata.status: experimental`
- proposal-stage versus live-stage is determined by path, not by inventing a second status vocabulary

### Goal-Linked Workflow Additions

`goal_links` is real, but it is not universal.

Use it when the workflow exists to support cross-project goals, business review lanes, or a named workflow target rather than only local library maintenance.

Observed shape:

```yaml
goal_links:
  project_goals:
    - project_goal_id
  business_goals:
    - business_goal_id
```

Observed companion patterns:

- `goal_links` sits between `summary` and `inputs`
- `reads.required` usually includes `goals/project_goals/`
- `reads.required` usually includes one or more `goals/business_goals/*.md` files
- `reads.required` often includes a matching `goals/workflow_targets/*.md` file when one exists

### Tool-Linked Workflow Conventions

Tool linkage is currently expressed through ordinary workflow fields, not a special top-level tool block.

Observed patterns in live tool-linked workflows:

- optional inputs such as `supporting_tools`, `tool_report_paths`, `supporting_tool_candidates`, or repo roots
- `reads.required` includes the live tool contract YAML and its matching validation note
- `reads.required` often includes `docs/usage/tool-package-call-order.md`
- steps explicitly separate tool-backed evidence from remaining manual interpretation
- notes usually say some version of "the tool extracts or validates; the workflow interprets"

Do not invent a top-level `tool_links:` section. The current builder does not use one.

### Tool Proposal Notes

Tool proposals are markdown-first, not YAML-first.

Observed header bullets:

```md
- `proposal_id`: `tool_id`
- `recommended_initial_placement`: `tools/experimental_tools/` or `tools/final_tools/`
- `recommended_maturity`: `experimental` or `final-ready`
- `priority`: `highest|high|medium|low`
```

Observed section order:

1. `Problem`
2. `Business Use Case`
3. `Why A Tool Is Better Than A Workflow-Only Solution`
4. `Suggested Inputs`
5. `Suggested Outputs`
6. `Core Behaviors`
7. `Validation Expectations`
8. `Workflow Dependencies And Beneficiaries`
9. `Non-Goals`

What to preserve:

- proposals name repeated deterministic work, not one-off feature wishes
- proposals point to beneficiary workflows that already exist or are already staged
- proposals keep business judgment, broad remediation, and mutation out of scope
- proposals act as provenance for later `metadata.proposal_source`

### Tool Contracts

The workspace now has one real `tool_contract` schema. Reuse it directly.

Observed top-level order:

```yaml
version
kind
metadata
summary
goal_links
scope
workflow_relationships
package_assets
commands
global_output_contract
build_plan
promotion
```

Observed metadata shape:

```yaml
metadata:
  id:
  name:
  status:
  maturity:
  owner:
  implementation_status:
  proposal_source:
  tags:
```

Observed contract rules:

- `kind` is `tool_contract`
- `status` mirrors placement: `experimental` or `final`
- `maturity` carries the more specific readiness label such as `experimental` or `final-ready`
- `proposal_source` points to a real markdown file under `proposals/tool_proposals/`
- `goal_links` currently uses `business_goals` plus `workflow_targets`
- `package_assets` points to template files, a validation note, and usage docs
- every live tool contract uses `commands` as the real callable surface
- `global_output_contract` and `promotion` are part of the live shape, not optional decoration
- all four live tools are read-only against source inputs and write only under the chosen output directory
- current command counts stay narrow: three live tools expose two commands and one exposes one command

Important uncertainty:

- only 4 live tool contracts exist
- `project_goals` are common in workflows but not yet evidenced in live tool contracts
- runtime probing is still proposal-only; it is not a live tool family yet

## Strongest Families To Extend

Prefer extending one of these families before inventing another lane.

### Workflow Families

`foundation and governance`

- `workspace_inventory`
- `initiative_plan`
- `workflow_review_gate`

Use when the job is orientation, decomposition, lifecycle review, or decision gating.

`intake, planning, and gap shaping`

- `request_intake_brief`
- `goal_reconciliation`
- `proposal_backlog_triage`
- `workflow_gap_seeding`
- `example_usage_seed`
- `project_setup_brief`
- `workflow_goal_coverage_audit`

Use when the job is compressing a broad ask, comparing it to goals, or shaping the next authoring move.

`catalog maintenance and reconciliation`

- `docs_reconciliation`
- `docs_cleanup_threshold_seed`
- `workspace_delta_scan`
- `workflow_template_gap_review`
- `workflow_placeholder_audit`
- `workflow_catalog_refresh`
- `workflow_overlap_reconciliation`
- `workflow_handoff_reconciliation`
- `workspace_hygiene_triage`

Use when the job is comparison, thresholding, cleanup guidance, catalog upkeep, or cross-file consistency review.

`goal-linked evidence and cross-repo review`

- `factory_export_readiness_review`
- `human_view_evidence_brief`
- `release_evidence_packet_seed`
- `validation_infrastructure_readiness_review`
- `content_parity_traceability_review`
- `portfolio_signal_refresh`
- `tool_surface_contract_audit`

Use when the workflow is interpreting evidence for a business-facing, portfolio-facing, or cross-repo review lane.

`tool-aware interpretation and tool-input staging`

- `demo_refresh_sanitization_brief`
- `tool_wrapper_onboarding_brief`
- `artifact_contract_profile_seed`

Use when the workflow depends on real tool output or is still shaping a future tool input contract.

Observed promotion rule already present in the repo:

- `tool_wrapper_onboarding_brief` and `demo_refresh_sanitization_brief` started in `proposals/workflow_proposals/`
- both gained live `automations/experimental/` versions once supporting tool contracts existed
- `artifact_contract_profile_seed` remains staged because it is still defining tool inputs rather than interpreting a stable tool contract

### Tool Families

`evidence packaging and artifact validation`

- `media_evidence_packager`
- `artifact_contract_validator`

Extend this family for deterministic bundle collection, contract checking, partitioning, and report emission.

`tool-surface cataloging and drift`

- `tool_surface_snapshotter`

Extend this family for read-only discovery, catalog normalization, and declared-surface drift detection.

`portfolio normalization and mirror drift`

- `portfolio_signal_normalizer`

Extend this family for deterministic cross-doc extraction, source indexing, and mirror comparison.

What does not count as a live family yet:

- `runtime_health_snapshotter` is a proposal-backed gap, not a live `tool_contract`
- do not treat proposal-only tools as proof that a new tool lane already exists

## Extension Rules

1. Extend an existing family before creating a parallel lane.
2. Use a workflow when the repeated work is planning, interpretation, prioritization, reconciliation, or follow-up design.
3. Use a tool when the repeated work is extraction, packaging, validation, normalization, or deterministic diffing.
4. Move a tool-linked workflow into `automations/experimental/` only when a real supporting tool contract and validation note already exist.
5. Keep a tool-linked workflow in `proposals/workflow_proposals/` when it is still upstream of tool implementation, especially when it is defining config or contract inputs.
6. Preserve the current directory contract and status vocabulary instead of inventing alternate buckets or status values.
7. Keep uncertainty explicit in `notes` or nearby guidance instead of silently promoting a weak pattern into a rule.

## Expected Documentation Support

For a live tool-linked family to feel complete in this builder, the current corpus expects:

- a workflow spec or staged workflow proposal
- a markdown tool proposal when a tool is involved
- a live `tool_contract` YAML if the tool has graduated into `tools/`
- a matching template under `tools/tool_templates/`
- a matching validation note under `tools/validation/`
- usage or example support under `docs/examples/` or `docs/usage/`

If one of those pieces is missing, say so explicitly. The current library tolerates partial support, but it does not hide it.
