# Mock Workflow YAML Patterns

Date: 2026-03-25
Status: example-only reference

These examples are not live workflow candidates. They are compact mock shapes based on the currently observed Nexus Central YAML style so later automations can generate new workflow files without drifting away from the seeded conventions.

## Pattern 1: Discovery Report Workflow

Use this pattern when the workflow scans a surface, records what exists, and produces a narrow follow-up queue.

```yaml
version: 1
kind: workflow_spec
metadata:
  id: dependency_inventory
  name: Dependency Inventory
  status: experimental
  owner: nexus-central
  implementation_status: specification_only
  tags:
    - exploration
    - inventory
    - dependencies
summary:
  purpose: Inventory documented dependencies before planning cleanup, replacement, or ownership work.
  offloads:
    - repeated scanning of docs and templates for dependency references
    - repeated comparison of known dependency notes versus missing coverage
inputs:
  required:
    - name: workspace_root
      type: path
      description: Root directory containing the workspace to inspect.
  optional:
    - name: scan_roots
      type: list[path]
      default:
        - docs
        - templates
      description: Directories to inspect for dependency references.
reads:
  required:
    - README.md
    - docs/conventions/workspace-contract.md
  expected:
    - docs/
    - templates/
writes:
  primary:
    - path: docs/usage/dependency-inventory-report.md
      description: Human-readable report of dependency references, gaps, and next review targets.
  secondary:
    - path: improvements/proposals/dependency-followups.md
      description: Follow-up queue for ownership, cleanup, or missing-doc work found during inventory.
steps:
  - id: map-dependency-surface
    objective: Capture where dependency information currently lives.
    actions:
      - Inventory dependency references across the selected scan roots.
      - Separate confirmed references from ambiguous or missing notes.
  - id: publish-findings
    objective: Produce a reviewable report and a smaller follow-up queue.
    actions:
      - Summarize the high-signal findings in docs/usage.
      - Defer cleanup work into a proposal queue instead of mixing it into the report.
validation:
  checks:
    - The report names the directories that were scanned.
    - The follow-up queue stays narrower than the inventory report.
handoff:
  upstream: []
  downstream:
    - docs_reconciliation
notes:
  - Keep the workflow observational and report-first.
```

## Pattern 2: Planning Workflow Without `reads`

Use this pattern when the workflow turns a broad request into a scoped plan and the required context is better represented through inputs and outputs than through a fixed read contract.

```yaml
version: 1
kind: workflow_spec
metadata:
  id: release_plan_brief
  name: Release Plan Brief
  status: experimental
  owner: nexus-central
  implementation_status: specification_only
  tags:
    - planning
    - sequencing
    - release
summary:
  purpose: Turn a broad release-readiness request into a scoped plan with explicit validation and deferred follow-ups.
  offloads:
    - repeated release planning setup
    - repeated decomposition of a broad readiness request
inputs:
  required:
    - name: workspace_root
      type: path
      description: Root directory where planning artifacts will be stored.
    - name: initiative_name
      type: string
      description: Human-readable name of the release initiative.
    - name: goal_statement
      type: string
      description: One- or two-sentence statement of the release goal.
  optional:
    - name: constraints
      type: list[string]
      default: []
      description: Known limits that should shape the planning brief.
writes:
  primary:
    - path: docs/usage/{initiative_slug}-release-plan.md
      description: Main release planning brief with phases, assumptions, and validation.
      template: templates/doc_templates/planning_scaffold.md
  secondary:
    - path: improvements/proposals/{initiative_slug}-release-followups.md
      description: Deferred work that should stay outside the first release plan.
steps:
  - id: freeze-goal-and-limits
    objective: Record the real goal and constraints before breaking the work apart.
    actions:
      - Restate the release goal, non-goals, and open questions.
      - Capture assumptions that need later confirmation.
  - id: define-phases-and-validation
    objective: Produce a plan that can be reviewed without runtime context.
    actions:
      - Split the request into narrow phases with reviewable outputs.
      - Write validation criteria for each phase.
validation:
  checks:
    - The planning brief is specific enough to review without executing release work.
    - Deferred work is captured separately from the main plan.
handoff:
  upstream:
    - workspace_inventory
  downstream:
    - goal_reconciliation
notes:
  - Use placeholders only in output paths, not in metadata ids.
```

## Pattern 3: Setup Workflow With Multiple Primary Outputs

Use this pattern when one workflow naturally produces a paired primary result set, such as a brief plus a manifest.

```yaml
version: 1
kind: workflow_spec
metadata:
  id: project_slice_setup
  name: Project Slice Setup
  status: experimental
  owner: nexus-central
  implementation_status: specification_only
  tags:
    - setup
    - scaffolding
    - workspace-onboarding
summary:
  purpose: Define the minimum structure for a new project slice and capture the immediate follow-up work it needs.
  offloads:
    - repeated setup-note drafting for new project slices
    - repeated directory and artifact planning at project start
inputs:
  required:
    - name: workspace_root
      type: path
      description: Root directory containing the larger workspace.
    - name: project_name
      type: string
      description: Human-readable name of the project slice being initialized.
    - name: target_root
      type: path
      description: Path inside the workspace where the slice will live.
  optional:
    - name: initial_needs
      type: list[string]
      default:
        - docs
        - artifacts
        - validation
      description: High-priority setup concerns to cover in the first pass.
writes:
  primary:
    - path: docs/usage/{project_slug}-setup-brief.md
      description: Setup brief describing scope, constraints, and first validation points.
      template: templates/project_setup_templates/project_setup_brief.md
    - path: artifacts/{project_slug}-manifest.md
      description: Paired manifest describing the initial directory and file contract for the slice.
      template: templates/project_setup_templates/workspace_manifest.md
  secondary:
    - path: improvements/proposals/{project_slug}-setup-followups.md
      description: Missing automation, validation, or documentation follow-ups discovered during setup.
steps:
  - id: gather-slice-context
    objective: Capture what the new slice is and what it needs first.
    actions:
      - Record the project goal, target root, and known constraints.
      - Note which existing workspace conventions already apply.
  - id: define-starting-structure
    objective: Produce the minimum durable structure without creating implementation files.
    actions:
      - Write the setup brief and the paired manifest as one reviewed result set.
      - Distinguish durable outputs from follow-up work.
validation:
  checks:
    - The setup brief and manifest describe the same target root.
    - The follow-up queue stays outside the primary result set.
handoff:
  upstream:
    - initiative_plan
  downstream:
    - docs_reconciliation
notes:
  - Multiple primary outputs are acceptable when they form one reviewed package.
```

## Pattern 4: Recommendation-Only Reconciliation Workflow

Use this pattern when the workflow compares current assets against a contract and should only recommend next actions.

```yaml
version: 1
kind: workflow_spec
metadata:
  id: workflow_boundary_review
  name: Workflow Boundary Review
  status: experimental
  owner: nexus-central
  implementation_status: specification_only
  tags:
    - review
    - reconciliation
    - boundaries
summary:
  purpose: Compare nearby workflow candidates and publish recommendation-only guidance about overlap, separation, and next actions.
  offloads:
    - repeated comparison of similar workflow specs after multiple authoring passes
    - repeated drafting of merge-versus-keep rationale for adjacent workflows
inputs:
  required:
    - name: workspace_root
      type: path
      description: Root directory containing the workflow inventory to compare.
  optional:
    - name: workflow_roots
      type: list[path]
      default:
        - automations/active
        - automations/experimental
      description: Workflow directories to inspect for neighboring candidates.
reads:
  required:
    - automations/
    - docs/conventions/workflow-authoring-contract.md
    - docs/conventions/automation-lifecycle.md
  expected:
    - docs/examples/
    - docs/usage/
writes:
  primary:
    - path: docs/usage/workflow-boundary-review.md
      description: Main review note describing where nearby workflows overlap and where they should stay separate.
  secondary:
    - path: artifacts/workflow-boundary-matrix.md
      description: Supporting comparison matrix for the reviewed workflows.
    - path: improvements/proposals/workflow-boundary-followups.md
      description: Ordered queue for renames, narrowing, or merge-review follow-ups.
steps:
  - id: group-nearby-candidates
    objective: Find the workflows most likely to overlap before comparing them in detail.
    actions:
      - Group nearby workflows by purpose, outputs, and handoff role.
      - Ignore distant workflows that do not create real ambiguity.
  - id: publish-recommendation-only-guidance
    objective: Leave a reviewable recommendation set without mutating workflow files.
    actions:
      - Recommend keep, narrow, rename, or merge-review outcomes for each nearby group.
      - Record the smallest useful next action for every recommendation.
validation:
  checks:
    - Every finding cites the workflow ids involved.
    - The result remains recommendation-only and does not move files directly.
handoff:
  upstream:
    - workflow_review_gate
  downstream:
    - workflow_catalog_refresh
notes:
  - Separate observed overlap from proposed follow-up actions.
```

## Reuse Notes

- Keep examples narrow and reviewable.
- Reuse the observed top-level section order.
- Omit `reads` only when the workflow still stays understandable without it.
- Keep uncertain fields provisional in nearby docs instead of silently normalizing them into the YAML contract.
