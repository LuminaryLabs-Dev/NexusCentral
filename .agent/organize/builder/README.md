# Nexus Central Workspace

This workspace is a planning and workflow-authoring environment for Nexus Central. It is intentionally documentation-first and contains reviewed workflow specs, goal-linked workflow targets, staged workflow and tool proposals, and tool-family planning structure, not runnable implementations.

Current starting point:

1. Read [`docs/usage/reconciliation-report-2026-03-25.md`](docs/usage/reconciliation-report-2026-03-25.md).
2. Read [`docs/conventions/workspace-contract.md`](docs/conventions/workspace-contract.md).
3. Read [`docs/conventions/automation-lifecycle.md`](docs/conventions/automation-lifecycle.md) before adding or moving workflow specs.
4. Read [`docs/conventions/tool-lifecycle.md`](docs/conventions/tool-lifecycle.md) and [`docs/conventions/workflow-tool-proposal-boundaries.md`](docs/conventions/workflow-tool-proposal-boundaries.md) before introducing tool-family inventory or tool proposal material.

Top-level contract:

- `automations/`: workflow inventory and lifecycle buckets
- `tools/`: tool-family inventory, templates, validation notes, and final versus experimental tool buckets
- `proposals/`: staged workflow and tool proposals
- `templates/`: reusable templates and scaffolds
- `logs/`: run logs and operational notes
- `artifacts/`: generated intermediate artifacts
- `outputs/`: final generated deliverables
- `lessons/`: captured learnings and retrospectives
- `improvements/`: queued follow-up changes and enhancement notes
- `docs/`: human-readable workspace documentation
- `goals/`: business goals, project goals, and workflow-target briefs
- `state/`: lightweight machine-readable state or snapshots
- `archive/`: retired outputs or historical material

Current workspace status:

- Three reviewed workflow specs are in `automations/active/`: `workspace_inventory`, `initiative_plan`, and `workflow_review_gate`.
- Twenty follow-on workflow specs remain in `automations/experimental/`, split across planning intake, catalog maintenance, and goal-linked domain review families.
- Reusable workflow, documentation, project setup, and validation templates are seeded under `templates/`.
- Three specs are intentionally deferred in `automations/disabled/`: `state_last_run_contract`, `workflow_validation_review`, and `workflow_lifecycle_review`.
- `tools/` contains three experimental tool contracts, one final-ready tool contract, reusable tool templates, and tool validation guidance.
- `proposals/` contains staged workflow proposals plus tool proposal records preserved as provenance for the promoted tool contracts; run-generated follow-up queues remain under `improvements/proposals/`.
- Superseded normalization, bootstrap-verification, and seeding-followup history has been moved to `archive/`.
- Historical bootstrap notes remain in `docs/` for context, but the reconciliation report is the current source of truth.

Guardrails:

- Reuse the existing directories instead of creating alternates.
- Use `proposals/` for staged workflow and tool proposals and `improvements/proposals/` for run-specific follow-up queues.
- Treat `automations/disabled/` as inactive-but-still-relevant material and `tools/final_tools/` versus `tools/experimental_tools/` as the tool maturity split; use `archive/` only for retired or superseded history.
- Treat polished final outputs as the end of the process, after workflow or tool review and acceptance, not as bootstrap content.
