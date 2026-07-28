# Workflow Gap Seeding Report

Date: 2026-03-25
Status: historical micro-coverage seeding report; later reviewed state lives in `docs/usage/reconciliation-report-2026-03-26.md`
Workspace Root: NexusCentral-Builder
Author: Codex

## Coverage Before Seeding

- Existing workflow files: 3 active, 10 experimental, and 3 disabled workflow specs covering exploration, planning, setup, documentation reconciliation, backlog triage, gap seeding, review, delta scanning, overlap review, handoff review, catalog refresh, and deferred state-contract work.
- Existing reusable templates: a general micro-automation template and a review-reconciliation template, plus planning, setup, report, and validation Markdown templates.
- Existing usage docs: overview, seeding history, reconciliation notes, and example compositions for the foundation, authoring, review, and maintenance loops.

## Gaps Discovered

- Gap: request intake still starts too late in the planning flow.
  Repeated work: broad requests still need manual compression into constraints, assumptions, and next-step workflow handoffs before `goal_reconciliation` or `initiative_plan` can stay narrow.
  Why it matters: this is quota-heavy planning work that recurs before the current workflow set can even be applied cleanly.
- Gap: template coverage is still inferred by hand.
  Repeated work: workflow authors still need to manually compare output shapes against the current template inventory and decide whether a new template is justified.
  Why it matters: this causes avoidable duplication and weakens the benefit of a template-first authoring workspace.
- Gap: placeholder and output-path consistency is not reviewed explicitly.
  Repeated work: new seeding passes still require manual checks for slug placeholders, output path naming, and consistency between YAML, usage notes, and examples.
  Why it matters: inconsistent mock paths make later conversion and review harder even when the workflow intent is sound.
- Gap: docs cleanup thresholds remain underspecified.
  Repeated work: `docs_reconciliation` can identify cleanup candidates, but reviewers still have to improvise the actual keep, merge, move, and archive thresholds.
  Why it matters: recurring documentation cleanup stays subjective until those thresholds are expressed as reusable guidance.

## Workflows Seeded

- [`automations/experimental/request_intake_brief.yaml`](../../automations/experimental/request_intake_brief.yaml)
  Purpose: normalize a broad incoming request before reuse review or initiative planning.
  Expected outputs: an intake brief, a constraint matrix, and a narrow intake follow-up queue.
- [`automations/experimental/workflow_template_gap_review.yaml`](../../automations/experimental/workflow_template_gap_review.yaml)
  Purpose: compare workflow output shapes against the template inventory and identify real template gaps.
  Expected outputs: a template coverage review, a workflow-to-template matrix, and a template follow-up queue.
- [`automations/experimental/workflow_placeholder_audit.yaml`](../../automations/experimental/workflow_placeholder_audit.yaml)
  Purpose: audit placeholder tokens, slug names, and output-path consistency across YAML and supporting docs.
  Expected outputs: a consistency audit, a placeholder matrix, and a small fix queue.
- [`automations/experimental/docs_cleanup_threshold_seed.yaml`](../../automations/experimental/docs_cleanup_threshold_seed.yaml)
  Purpose: turn recurring documentation cleanup judgment into reusable thresholds, checklist support, and examples.
  Expected outputs: a threshold note, a reviewer checklist, and example cleanup cases.

## Supporting Templates and Docs

- [`templates/workflow_templates/consistency_audit_template.yaml`](../../templates/workflow_templates/consistency_audit_template.yaml)
  Why it was added: the current template set had a general reconciliation starter but no dedicated starter for cross-file consistency audits such as placeholder, path, and reference reviews.
- [`docs/examples/template-and-consistency-review-loop.md`](../../docs/examples/template-and-consistency-review-loop.md)
  Why it was added: the current examples covered broader seeding and maintenance loops but not the narrower path that fronts intake normalization, template review, threshold seeding, and placeholder auditing before another authoring pass.

## Cleanup and Reconciliation Next Review

- Review item: decide whether `request_intake_brief` is a durable workflow or just a temporary planning support layer.
  Reason: it is clearly useful for large asks, but repeated use should prove whether it stays separate from `initiative_plan`.
- Review item: compare `workflow_template_gap_review` against actual future template additions before promoting it.
  Reason: the gap is credible, but promotion should depend on seeing it influence real template reuse decisions.
- Review item: confirm whether `workflow_placeholder_audit` should remain separate from `workflow_handoff_reconciliation` and `workflow_review_gate`.
  Reason: they are adjacent review workflows, and future evidence should confirm that placeholder drift is distinct enough to justify its own pass.
- Review item: use the new docs-threshold workflow to sharpen `docs_reconciliation` before promoting either one.
  Reason: threshold guidance is the clearest unresolved blocker in the documentation cleanup path.
