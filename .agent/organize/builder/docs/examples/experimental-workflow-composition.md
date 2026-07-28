# Workflow Composition Example

This example shows how the reviewed core planning-layer workflows are meant to work together without collapsing into one large workflow.

It is intentionally not the full maintenance catalog. For the broader maintenance and reconciliation path, see [`workflow-maintenance-loop.md`](../../docs/examples/workflow-maintenance-loop.md) and [`review-driven-workflow-loop.md`](../../docs/examples/review-driven-workflow-loop.md).

## Example Scenario

Goal: prepare a new project slice inside the workspace while keeping documentation and planning consistent.

## Recommended Flow

1. Run `workspace_inventory` to inventory the current workspace and identify missing coverage.
2. Run `initiative_plan` for the project initiative if the goal is still broad or underspecified.
3. Run `project_setup_brief` when the project slice needs a dedicated setup brief and workspace manifest.
4. Run `docs_reconciliation` after setup if the new notes create overlap with existing docs, lessons, or improvement proposals.

## Expected Artifacts by Stage

- Exploration stage: workspace inventory, gap report, and automation candidates.
- Planning stage: scoped plan, smaller task breakdown, and deferred follow-ups.
- Setup stage: setup brief, workspace manifest, and automation follow-up list.
- Cleanup stage: docs inventory, cleanup report, and ordered reconciliation actions.

## Why This Stays Modular

- Exploration stays observational.
- Planning stays focused on decomposition and validation.
- Setup stays focused on structure and onboarding.
- Cleanup stays focused on reconciliation rather than creation.

Keeping these concerns separate makes each workflow easier to validate, replace, or promote to `automations/active/` later.
