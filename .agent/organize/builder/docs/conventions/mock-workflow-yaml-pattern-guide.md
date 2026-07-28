# Mock Workflow YAML Pattern Guide

Date: 2026-03-25
Status: inferred from the current seeded corpus

## Purpose

This guide summarizes the YAML workflow style currently evidenced inside Nexus Central. It is meant to help later automations draft mock workflow files without inventing a stronger runtime contract than the repository actually proves.

## Source Basis

This convention set is based on the current workflow-shaped YAML corpus:

- `automations/active/`: 3 files
- `automations/experimental/`: 14 files
- `automations/disabled/`: 3 files
- `templates/workflow_templates/`: 3 files
- `archive/state_last_run_seed.yaml`: 1 historical YAML seed

It is also cross-checked against the current authoring and lifecycle docs:

- `automations/README.md`
- `docs/conventions/workspace-contract.md`
- `docs/conventions/workflow-authoring-contract.md`
- `docs/conventions/automation-lifecycle.md`
- `docs/usage/workflow-seeding-overview.md`
- `docs/usage/workflow-seeding-report-2026-03-25.md`
- `docs/usage/workflow-gap-seeding-report-2026-03-25.md`
- `docs/usage/workflow-gap-seeding-report-2026-03-25-micro-coverage.md`
- `templates/validation/workflow_validation_checklist.md`

## Observed Baseline

Across all 24 workflow-shaped YAML files currently in scope:

- `version: 1` appears in 24 of 24.
- `kind: workflow_spec` appears in 24 of 24.
- `metadata.owner: nexus-central` appears in 24 of 24.
- `metadata.implementation_status: specification_only` appears in 24 of 24.
- `handoff` and `notes` appear in 24 of 24.
- `reads` appears in 21 of 24 and is therefore common but optional.
- Multiple primary outputs appear in 3 of 24, so they are allowed but not the default.

The live buckets also align with `metadata.status`:

- files in `automations/active/` use `status: active`
- files in `automations/experimental/` use `status: experimental`
- files in `automations/disabled/` use `status: disabled`

The archived historical seed still says `status: experimental`, so archive placement should be treated as a repository location decision, not as proof of an `archived` metadata value.

## Placement Rules

- Put reviewed, routine workflow specs in `automations/active/`.
- Put draft, speculative, or unproven workflow specs in `automations/experimental/`.
- Put intentionally paused but still relevant workflow specs in `automations/disabled/`.
- Put superseded or historical YAML material in `archive/`.
- Put authoring guidance in `docs/conventions/`.
- Put example-only reference material in `docs/examples/`, not `automations/`.
- Put reusable review aids in `templates/validation/`.

## File Naming Rules

- Keep the filename in lowercase snake_case.
- Match the live filename to `metadata.id`.
- Keep `metadata.id` in lowercase snake_case.
- Keep `metadata.name` short, human-readable, and outcome-focused.
- Keep tags lowercase. Hyphenated tags are already in use and fit the current style.
- Keep step ids lowercase and hyphenated.

Observed placeholder tokens belong in output paths, not in ids:

- `{initiative_slug}`
- `{project_slug}`
- `{review_slug}`
- `{example_slug}`
- `{automation_slug}`
- `{audit_slug}`
- `{workflow_id}`

## Preferred Top-Level Section Order

Use this order when drafting a new mock workflow:

```yaml
version
kind
metadata
summary
inputs
reads        # optional
writes
steps
validation
handoff
notes
```

If `reads` is omitted, keep the remaining section order unchanged.

## Field Conventions

### Metadata

Observed key order:

```yaml
metadata:
  id:
  name:
  status:
  owner:
  implementation_status:
  tags:
```

Observed norms:

- `status` should match the lifecycle bucket for live workflow files.
- `owner` is currently always `nexus-central`.
- `implementation_status` is currently always `specification_only`.
- tags usually describe category and intent rather than implementation detail.

### Summary

Observed structure:

```yaml
summary:
  purpose: One-sentence statement of the repeated task.
  offloads:
    - repeated task one
    - repeated task two
```

Observed norms:

- `purpose` is concise and declarative.
- `offloads` names repeated manual effort, review work, or quota-heavy reasoning the workflow removes.
- most files use two or three `offloads` bullets.

### Inputs

Observed structure:

```yaml
inputs:
  required:
    - name:
      type:
      description:
  optional:
    - name:
      type:
      default:
      description:
```

Observed norms:

- `workspace_root` is the first required input in every current live spec and template.
- required inputs typically capture the subject under review, such as `initiative_name`, `project_name`, `goal_statement`, or `workflow_ids`.
- optional inputs usually define scope, comparison bias, report mode, scan roots, or reusable template overrides.
- `default` may be a scalar, an empty list, or a populated list.

### Reads

Observed structure:

```yaml
reads:
  required:
    - path-or-directory
  expected:
    - path-or-directory
```

Observed norms:

- `reads` is common but optional.
- use `required` for context the workflow expects every pass to consult.
- use `expected` for surfaces that are likely to exist but may not be guaranteed yet.
- entries can be file paths or directory roots.
- directory-style reads often end with a trailing slash, but not always. Keep usage consistent inside one file.

### Writes

Observed structure:

```yaml
writes:
  primary:
    - path:
      description:
      template: optional-template-path
  secondary:
    - path:
      description:
      template: optional-template-path
```

Observed norms:

- all current specs include both `primary` and `secondary`.
- every write item includes `path` and `description`.
- `template` appears only when a reusable doc scaffold is part of the output.
- `primary` must contain at least one main deliverable.
- multiple primary outputs are allowed when they form one reviewed result set, such as a brief plus manifest or an inventory plus report.
- `secondary` usually holds supporting artifacts, examples, or follow-up queues.

Observed output destinations:

- `docs/usage/` for reviewable notes, reports, briefs, and contracts
- `artifacts/` for matrices, inventories, and intermediate supporting material
- `improvements/proposals/` for deferred work and follow-up queues
- `docs/examples/` for scenario or example-only outputs
- `automations/experimental/` only when the workflow itself seeds a new mock YAML spec
- `state/last_runs/` only in deferred historical state-contract examples

### Steps

Observed structure:

```yaml
steps:
  - id:
    objective:
    actions:
      - action one
      - action two
```

Observed norms:

- current files use two to four steps.
- step ids are short, verb-led, and hyphenated.
- each step has one `objective` and usually two `actions`.
- steps stay narrow and composable instead of describing a full end-to-end implementation.

### Validation

Observed structure:

```yaml
validation:
  checks:
    - reviewer-visible check
```

Observed norms:

- validation checks describe evidence a reviewer can inspect in the produced artifact.
- checks focus on clarity, scope control, citations, path specificity, and non-destructive behavior.
- validation does not describe hidden runtime behavior or execution success signals.

### Handoff

Observed structure:

```yaml
handoff:
  upstream:
    - workflow_id
  downstream:
    - workflow_id
```

Observed norms:

- `handoff` is always present, even when one side is empty.
- use workflow ids, not paths or prose.
- handoffs explain composition and sequencing, not runtime triggering.

### Notes

Observed structure:

```yaml
notes:
  - guardrail
  - scope reminder
```

Observed norms:

- notes are a short list of guardrails, lifecycle caveats, or review boundaries.
- most files use one to three notes.

## Common Workflow Categories

The current corpus clusters around a few recurring workflow types:

- exploration and inventory
- planning and decomposition
- intake normalization and request compression
- setup and scaffolding
- template coverage and consistency audit
- documentation, examples, and catalog refresh
- review, reconciliation, validation, and lifecycle decisions
- deferred state and coordination contracts

These categories are reflected in both tags and output style.

## Output Style Expectations

The current house style is strongly report-first and non-destructive:

- prefer reports, briefs, manifests, matrices, examples, and follow-up queues
- separate observed facts from recommended actions
- keep follow-up work in `improvements/proposals/` instead of inflating the main output
- avoid implementation code, execution promises, or hidden runtime assumptions
- keep `docs/usage/` and `docs/examples/` distinct: usage notes explain boundaries, examples show composition or scenarios

## Provisional Areas And Uncertainty

These areas are still lightly evidenced and should be treated as provisional:

- no runtime trigger fields, schedules, executors, or machine state fields appear in the live workflow corpus
- no schema versions other than `version: 1` appear
- no owner values other than `nexus-central` appear
- no implementation markers other than `specification_only` appear
- `reads` is common but not mandatory
- multiple primary outputs are valid but still uncommon
- `state/last_runs/` output appears only in deferred historical examples, not in active workflow practice
- archive placement does not yet prove a separate `metadata.status` value

## Authoring Default

If later automation needs to draft a new mock workflow and no stronger precedent exists:

1. Start from `templates/workflow_templates/micro_automation_template.yaml`.
2. Keep the top-level section order above.
3. Use `experimental` placement unless active readiness is already documented elsewhere.
4. Produce one clear primary reviewed outcome plus any narrow supporting outputs.
5. Document uncertainty in notes or nearby docs instead of inventing runtime behavior.
