# Tool Proposal And Tool-Linked Workflow Checklist

Use this checklist when seeding new tool proposals, staged tool-linked workflow YAML, or live tool-linked workflow specs.

## Current Repo Reality

- The workflow YAML pattern is stable and reused in both `automations/` and `proposals/workflow_proposals/`.
- The live tool layer is real but still small: 4 live `tool_contract` files.
- The safest rule is still family extension before schema invention.

## Placement Checks

- Use `proposals/tool_proposals/` for markdown tool proposals.
- Use `tools/experimental_tools/` or `tools/final_tools/` only after a tool proposal exists and the tool contract surface is clear.
- Use `proposals/workflow_proposals/` when a tool-linked workflow is still shaping tool inputs or depends on tooling that is not yet live.
- Use `automations/experimental/` only when the workflow can honestly stay interpretive because the supporting tool contract and validation note already exist.
- Keep follow-up queues in `improvements/proposals/`, not in `proposals/`.

## Tool Proposal Checks

- The proposal uses the current header bullets:
  `proposal_id`, `recommended_initial_placement`, `recommended_maturity`, `priority`.
- The proposal follows the current section sequence:
  `Problem`, `Business Use Case`, `Why A Tool Is Better Than A Workflow-Only Solution`, `Suggested Inputs`, `Suggested Outputs`, `Core Behaviors`, `Validation Expectations`, `Workflow Dependencies And Beneficiaries`, `Non-Goals`.
- The proposal names repeated deterministic work, not a one-off product request.
- The proposal names the existing workflow family or beneficiary workflows it would support.
- The proposal keeps business judgment, runtime mutation, and broad remediation out of scope.
- The proposal implies a future validation note under `tools/validation/`.
- The proposal should fit an existing live tool family when possible; otherwise it stays proposal-only until the family boundary is clearer.

## Tool Contract Handshake Checks

- A real proposal file exists and can be referenced in `metadata.proposal_source`.
- The contract uses `kind: tool_contract`.
- `metadata.status` matches bucket placement.
- `metadata.maturity` is more specific than status alone.
- `goal_links` uses real workspace goal ids or workflow targets already present in the repo.
- `package_assets` points to a real template file, validation note path, and usage docs path set.
- `commands` define the callable contract surface instead of hiding it in prose.
- `global_output_contract` makes downstream file expectations explicit.
- `promotion` explains why the tool stays experimental or why final placement is justified.
- The contract keeps source inputs read-only and writes only under the chosen output directory.

## Tool-Linked Workflow Checks

- The workflow still follows the house `workflow_spec` section order.
- `metadata.status` matches the live bucket when the file is under `automations/`.
- For staged workflow proposals under `proposals/workflow_proposals/`, keeping `metadata.status: experimental` is acceptable because the path carries the staging meaning.
- `workspace_root` remains the first required input unless there is a strong reason not to.
- Tool linkage is expressed through `goal_links`, `reads`, and explicit inputs such as `tool_report_paths`, `supporting_tools`, or repo roots.
- No new top-level `tool_links:` block is introduced.
- If a real tool exists, `reads.required` references the live tool contract and its validation note.
- Tool-linked workflows usually also read `docs/usage/tool-package-call-order.md` when report consumption order matters.
- If the workflow is still defining tool inputs or waiting on a live tool, it stays in proposal space.
- `writes.primary` holds the main reviewable note.
- `writes.secondary` holds support matrices and follow-up queues.
- `notes` should keep the boundary explicit: the tool extracts, validates, or normalizes; the workflow interprets and recommends.

## Family Extension Checks

- The workflow extends an existing family instead of duplicating a nearby lane.
- The tool extends one of the current live tool families:
  evidence packaging and artifact validation, tool-surface cataloging and drift, or portfolio normalization and mirror drift.
- Proposal-only ideas such as runtime probing stay proposal-only until they have enough evidence to become a live tool family.
- The workflow interprets tool outputs; it does not restate the tool's extraction or packaging procedure as if that were workflow logic.
- When uncertainty remains, it is called out in `notes` instead of being promoted into an unproven rule.

## Validation And Scope Checks

- Validation checks refer to reviewer-visible evidence, not hidden runtime behavior.
- The workflow or proposal stays documentation-first, review-first, and non-executing.
- Follow-ups stay smaller than the repo, product, or tool family being discussed.
- The result preserves the current library shape instead of inventing alternate roots, statuses, or schemas.
