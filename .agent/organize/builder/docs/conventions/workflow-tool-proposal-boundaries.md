# Workflow, Tool, And Proposal Boundaries

## Purpose

This note explains how to separate live workflows, live tool surfaces, staged proposals, and follow-up queues inside Nexus Central Builder.

## Live Workflows

- Store live workflow specifications in `automations/`.
- Use `automations/active/` for review-ready workflows, `automations/experimental/` for provisional workflow specs, and `automations/disabled/` for intentionally paused workflow specs.
- A workflow should describe a repeated planning, review, reconciliation, seeding, or reporting flow. It is usually declarative and documentation-first.

## Live Tools

- Store tool-facing material in `tools/`.
- Use `tools/experimental_tools/` for draft tool contracts, wrappers, or helper surfaces that still need review.
- Use `tools/final_tools/` for reviewed tool assets that should be extended instead of re-created elsewhere.
- Use `tools/tool_templates/` and `tools/validation/` for reusable tool scaffolds and review support.
- Tool assets should describe or support a callable surface, interface wrapper, adapter, manifest, or validation loop rather than a planning workflow.

## Staged Proposals

- Store pre-library workflow ideas in `proposals/workflow_proposals/`.
- Store pre-library tool ideas in `proposals/tool_proposals/`.
- A proposal belongs here when the concept is real enough to preserve but not yet strong enough to enter `automations/` or `tools/`.
- Promote a proposal into `automations/experimental/` or `tools/experimental_tools/` once its scope, outputs, and placement are clear.

## Follow-Up Queues

- Store backlog items and deferred cleanup in `improvements/proposals/`.
- Do not use `improvements/proposals/` as a substitute for `proposals/workflow_proposals/` or `proposals/tool_proposals/`.
- If the file is a queue of next actions, fixes, or debt, it belongs under `improvements/proposals/`.
- If the file is a candidate spec or contract that may graduate into the library, it belongs under `proposals/`.

## Preservation Rule

- Extend the existing workflow families before inventing parallel workflow categories.
- Extend the existing tool-surface and contract-audit family before inventing alternate tool roots.
- Preserve the reviewed workflow library already living under `automations/`; proposals should feed that library, not replace it.
