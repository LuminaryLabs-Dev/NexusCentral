# Workflow Authoring Contract

## Purpose

This note defines the minimum placement and authoring rules for workflow YAML discovery in this workspace.

## Authoring Start Points

- Capture pre-spec workflow ideas in `proposals/workflow_proposals/` when they are still exploratory and not ready to become live workflow specs.
- Create new workflow specifications from `templates/workflow_templates/`.
- Place unproven or specification-only workflow YAML files in `automations/experimental/`.
- Promote a workflow into `automations/active/` only after its purpose, inputs, outputs, and validation expectations are clear.
- Move intentionally paused but still relevant workflow YAML files into `automations/disabled/`.
- Move retired or superseded workflow material into `archive/`, not `automations/disabled/`.
- Keep backlog or cleanup follow-ups in `improvements/proposals/` rather than mixing them with workflow proposals.

Specification-only workflows can still move into `automations/active/` when they are the clearest reviewed planning-layer candidates and their remaining gaps are explicitly documented elsewhere.

## Minimum YAML Expectations

- Keep workflow ids in lowercase snake_case.
- Keep workflow specs declarative: purpose, inputs, reads, writes, steps, validation, and handoff should be understandable without runtime knowledge.
- Prefer a narrow reusable output under `docs/usage/`, `artifacts/`, or `improvements/proposals/` instead of broad implementation behavior.
- Read the current workspace contract before introducing new paths outside the existing directory model.
- Do not place workflow specs under `tools/`; use `proposals/workflow_proposals/` if the asset is still pre-spec.

## Discovery Baseline

Later pattern-discovery or workflow-authoring automations should start by checking:

- `automations/active/`
- `automations/experimental/`
- `automations/disabled/`
- `proposals/workflow_proposals/`
- `goals/`
- `tools/`
- `templates/workflow_templates/`
- `docs/conventions/`
- `docs/usage/`

## Minimum Discovery Order

To keep later passes consistent, start in this order:

1. Read `docs/conventions/workspace-contract.md` for the directory contract.
2. Read [`docs/conventions/workflow-tool-proposal-boundaries.md`](../../docs/conventions/workflow-tool-proposal-boundaries.md) for the live-workflow versus proposal distinction.
3. Read this file for workflow placement and discovery boundaries.
4. Inspect `automations/active/`, `automations/experimental/`, and `automations/disabled/` for existing YAML patterns.
5. Inspect `proposals/workflow_proposals/`, `goals/`, and `tools/` so new workflow material extends an existing family instead of bypassing it.
6. Inspect `templates/workflow_templates/` for the current template baseline.
7. Record new review notes in `docs/usage/` and record follow-up gaps in `improvements/proposals/` instead of creating new top-level structure.

## Scope Boundary

This workspace is ready for workflow authoring when later automations can discover existing YAML specs, staged workflow proposals, goal targets, tool-family inventory, reuse the template contract, and write new reviewable documentation without inventing parallel structure.
