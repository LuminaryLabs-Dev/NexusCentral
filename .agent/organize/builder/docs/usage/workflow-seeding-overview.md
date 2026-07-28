# Workflow Inventory Overview

Date: 2026-03-25

This workspace started with a bootstrap-level contract, a bootstrap record, and no reusable workflow specifications or authoring templates. The reviewed files now establish a small core workflow layer that focuses on repeated workspace operations instead of implementation.

This overview focuses on the core planning flow. The broader maintenance and reconciliation layer is summarized in [`docs/usage/reconciliation-report-2026-03-26.md`](../../docs/usage/reconciliation-report-2026-03-26.md).

The current library is now broader than this core flow. In addition to the four workflows summarized here, the repo also contains maintenance workflows, goal-linked domain review workflows, a tool-family planning surface under [`tools/README.md`](../../tools/README.md), and curated proposal intake under [`proposals/README.md`](../../proposals/README.md).

## Current Workflow Set

- Active: `workspace_inventory` inspects the workspace, inventories coverage, and identifies recurring manual work worth automating.
- Active: `initiative_plan` turns an initiative into a scoped plan with phases, validation, and workflow handoffs.
- Experimental: `project_setup_brief` creates a dedicated setup brief and workspace manifest when a project slice needs structure beyond the initiative plan.
- Experimental: `docs_reconciliation` reconciles documentation sprawl, placeholder content, and cleanup candidates into a reviewable queue.

## Intended Sequence

1. Start with `workspace_inventory` to learn what already exists and what gaps remain.
2. Use `initiative_plan` when a goal is still too broad or underspecified.
3. Use `project_setup_brief` only when a new project slice needs a dedicated structure brief before implementation.
4. Use `docs_reconciliation` after exploration or setup when documentation placement and stale notes need review.

## Output Philosophy

- Prefer reports, briefs, manifests, and templates over direct implementation.
- Keep each workflow narrow enough to validate independently.
- Record deferred work in `improvements/proposals/` instead of inflating the first pass.
- Preserve the bootstrap contract: docs under `docs/`, intermediate material under `artifacts/`, and lightweight state under `state/`.

## Reusable Template Set

- Workflow authoring starts from [`templates/workflow_templates/micro_automation_template.yaml`](../../templates/workflow_templates/micro_automation_template.yaml).
- Planning and report outputs reuse the files in `templates/doc_templates/`.
- Project initialization notes reuse the files in `templates/project_setup_templates/`.
- Workflow review should use [`templates/validation/workflow_validation_checklist.md`](../../templates/validation/workflow_validation_checklist.md).

## Naming and Placeholder Convention

- Use lowercase snake_case for workflow ids.
- Use `{initiative_slug}` and `{project_slug}` in spec-only output paths where a later executor will substitute a concrete value.
- Keep workflow YAML declarative: purpose, inputs, outputs, steps, validation, and handoffs should be readable without runtime knowledge.
