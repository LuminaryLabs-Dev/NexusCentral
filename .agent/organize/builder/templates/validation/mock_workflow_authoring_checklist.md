# Mock Workflow Authoring Checklist

Use this checklist when drafting or reviewing a mock Nexus Central workflow YAML from the current inferred pattern set.

## Placement

- The file belongs in the correct lifecycle bucket for its current confidence.
- Live workflow specs in `automations/` use folder placement that matches `metadata.status`.
- Specification-only or unproven drafts stay in `automations/experimental/`.
- Proposal-stage workflow YAML belongs in `proposals/workflow_proposals/` and can still keep `metadata.status: experimental`.
- Historical or superseded workflow YAML belongs in `archive/`, not `automations/disabled/`.
- Example-only references stay in `docs/examples/`, not `automations/`.

## Naming

- The filename matches `metadata.id`.
- `metadata.id` is lowercase snake_case.
- `metadata.name` is short, human-readable, and outcome-focused.
- Tags are lowercase and describe category or intent rather than implementation detail.
- Step ids are lowercase and hyphenated.
- Placeholder tokens such as `{initiative_slug}` or `{review_slug}` appear only in output paths, not in ids.

## Shape

- The top-level section order follows the current observed pattern:
  `version`, `kind`, `metadata`, `summary`, optional `goal_links`, `inputs`, optional `reads`, `writes`, `steps`, `validation`, `handoff`, `notes`.
- `version` is `1`.
- `kind` is `workflow_spec`.
- `metadata.owner` is `nexus-central`.
- `metadata.implementation_status` is `specification_only`.
- `handoff` includes both `upstream` and `downstream`, even if one side is empty.
- `notes` is present as a short guardrail list.
- Do not introduce new top-level workflow sections unless the live corpus already proves them.

## Inputs And Outputs

- `workspace_root` is the first required input unless there is a strong reason not to follow the house style.
- Every input includes `name`, `type`, and `description`.
- Optional inputs use `default` only where it clarifies the intended baseline.
- `writes.primary` contains at least one main deliverable.
- Multiple primary outputs are acceptable only when they form one reviewed result set.
- `writes.secondary` captures supporting artifacts, examples, or follow-up queues.
- Every write item includes `path` and `description`.
- `template` is used only when a reusable scaffold is actually being referenced.
- Output paths stay inside the current workspace contract.

## Read Contract

- `reads` is present when the workflow depends on fixed workspace context.
- If `reads` is present, it uses `required` and adds `expected` only when the surface may exist without being guaranteed.
- Required reads list the minimum recurring context, not every possible supporting file.
- If `goal_links` is present, `reads.required` usually includes the relevant goal files or goal roots.
- If `reads` is omitted, the workflow still remains understandable and reviewable.

## Steps And Validation

- Each step has `id`, `objective`, and `actions`.
- Steps stay narrow and composable instead of collapsing discovery, writing, and cleanup into one vague block.
- Validation checks describe reviewer-visible evidence, not hidden runtime behavior.
- Validation confirms scope control, clarity, citations, path specificity, or non-destructive behavior.
- Handoffs use workflow ids, not file paths or prose-only descriptions.

## Family Extension

- The workflow extends the nearest existing family instead of creating a parallel lane.
- Foundation and governance work should look like `workspace_inventory`, `initiative_plan`, or `workflow_review_gate`.
- Planning and gap-shaping work should look like `request_intake_brief`, `goal_reconciliation`, `proposal_backlog_triage`, or `workflow_gap_seeding`.
- Catalog and reconciliation work should look like `docs_reconciliation`, `workflow_catalog_refresh`, `workflow_overlap_reconciliation`, or `workflow_handoff_reconciliation`.
- Goal-linked review work should look like `factory_export_readiness_review`, `release_evidence_packet_seed`, `portfolio_signal_refresh`, or `tool_surface_contract_audit`.
- Tool-aware work should follow the same workflow shape and then add tool context through `inputs`, `reads`, and `notes`, not through a new schema.

## Scope Control

- The workflow targets repeated manual work rather than a one-off request.
- The workflow produces docs, reports, manifests, matrices, examples, contracts, or follow-up queues rather than implementation code.
- The spec stays declarative and specification-only.
- The spec does not invent execution triggers, schedules, machine state, or runtime guarantees not supported by the current seeds.
- Any unclear or lightly evidenced pattern is documented as provisional in nearby docs or notes instead of silently becoming contract.
