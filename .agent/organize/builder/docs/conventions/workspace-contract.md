# Workspace Contract

## Purpose

This workspace is a planning and workflow-authoring environment for Nexus Central. It is intentionally structure-first: directories define where later automations should read, write, preserve, and review workflow, tool, and proposal material.

## Authoritative Documents

- [`README.md`](../../README.md): short entry point for humans and later automations
- [`docs/conventions/workflow-tool-proposal-boundaries.md`](../../docs/conventions/workflow-tool-proposal-boundaries.md): distinction between live workflows, live tool surfaces, staged proposals, and follow-up queues
- [`docs/conventions/tool-lifecycle.md`](../../docs/conventions/tool-lifecycle.md): rules for classifying tool-family inventory and tool proposal material
- [`docs/conventions/automation-lifecycle.md`](../../docs/conventions/automation-lifecycle.md): rules for classifying workflow material as active, experimental, disabled, or archived
- [`docs/usage/reconciliation-report-2026-03-26.md`](../../docs/usage/reconciliation-report-2026-03-26.md): current reviewed state of the workspace after reconciliation
- [`docs/usage/bootstrap-record.md`](../../docs/usage/bootstrap-record.md): historical record of the initial scaffold pass

## Baseline Directory Contract

- `automations/`: workflow assets, drafts, and workflow-specific planning material
- `automations/active/`: workflows that are ready for routine use
- `automations/experimental/`: drafts, prototypes, or trial workflows
- `automations/disabled/`: intentionally inactive workflows kept for reference
- `templates/`: reusable scaffolds for future work
- `templates/workflow_templates/`: templates for automation and workflow definitions
- `templates/project_setup_templates/`: templates for workspace or project initialization
- `templates/doc_templates/`: templates for docs, records, and summaries
- `templates/presentation_templates/`: templates for static HTML, CSS, and JavaScript presentation demos or interactive decks
- `templates/validation/`: templates for checks, validation prompts, or review criteria
- `tools/`: tool-family inventory, tool surface notes, templates, validation notes, and tool maturity buckets
- `tools/experimental_tools/`: draft tool contracts, wrappers, or helper surfaces that still need review
- `tools/final_tools/`: reviewed tool assets that should be extended instead of re-created elsewhere
- `tools/tool_templates/`: reusable templates for tool contracts, wrappers, or interface definitions
- `tools/validation/`: tool-specific validation notes, checklists, or review artifacts
- `proposals/`: staged workflow and tool proposals that are not yet part of the live library
- `proposals/workflow_proposals/`: workflow concepts that are still pre-spec or need review before joining `automations/`
- `proposals/tool_proposals/`: tool concepts that are still pre-contract or pre-implementation before joining `tools/`
- `docs/`: human-readable workspace guidance
- `docs/conventions/`: stable operating contracts and workspace rules
- `docs/examples/`: concrete examples when later automations need them
- `docs/project_breakdowns/`: reusable project-specific reference briefs
- `docs/usage/`: handoff notes, run records, and how-to guidance
- `goals/`: durable goal inputs that drive workflow and tool authoring decisions
- `goals/business_goals/`: cross-project business or portfolio outcomes
- `goals/project_goals/`: project-specific goals and operating targets
- `goals/workflow_targets/`: named workflow targets linked to business and project goals
- `lessons/`: captured learning from past work
- `lessons/patterns/`: reusable good patterns
- `lessons/failures/`: failure cases and breakdowns
- `lessons/successes/`: proven outcomes worth repeating
- `lessons/anti_patterns/`: approaches to avoid
- `improvements/`: follow-up change tracking
- `improvements/proposals/`: run-specific follow-up queues and implementation-side proposal backlogs
- `improvements/accepted/`: approved improvements awaiting or undergoing execution
- `improvements/rejected/`: declined proposals retained for context
- `logs/`: run logs and operational notes
- `artifacts/`: intermediate generated material
- `outputs/`: finalized user-facing outputs
- `state/`: lightweight machine-readable coordination state
- `state/locks/`: lock or claim files used to avoid concurrent conflicts
- `state/last_runs/`: last-run markers or summarized automation state
- `archive/`: retired or historical material preserved without being active

## Boundaries

- Preserve these directory names unless an explicit migration is requested.
- Prefer adding task-specific files inside the existing contract instead of inventing additional top-level structure.
- Use `docs/` for durable human-readable guidance, `state/` for lightweight coordination state, `artifacts/` for intermediate material, and `outputs/` for final deliverables.
- Use `proposals/` for staged workflow and tool proposals. Use `improvements/proposals/` for run-generated follow-up queues, not as the canonical library of staged proposal specs.
- Do not treat this workspace as an execution runtime. It is a repository for planning, documentation, templates, and workflow-authoring support.
- Keep `automations/` and `tools/` readable. Empty tool maturity buckets are acceptable when no reviewed tool definitions are ready.
- Use `automations/disabled/` for paused or deferred workflow items. Use `archive/` only for retired, superseded, or historical material that should no longer participate in normal selection.
- Template folders may remain empty until a real workflow, doc pattern, or validation loop justifies seeding them.

## Expectations For Later Automations

- Read this contract before creating new structure.
- Reuse the narrowest matching directory that already exists.
- Check `goals/`, `proposals/`, and `tools/` before seeding new workflow or tool material so family placement stays intentional.
- If new substructure is required, document it in `docs/conventions/` or `docs/usage/` before relying on it broadly.
- Before producing polished final outputs, confirm the workflow or tool inventory and bucket placement have already been reviewed and accepted.

## Reconciliation Readiness

- The baseline workspace tree described in this document is present and should be treated as the stable default.
- Workflow discovery should start with `automations/`, `templates/workflow_templates/`, `goals/`, `proposals/workflow_proposals/`, `tools/`, and `docs/examples/`.
- Write run-specific findings, deltas, and reconciliation notes to `docs/usage/` instead of expanding this contract with per-run status.
