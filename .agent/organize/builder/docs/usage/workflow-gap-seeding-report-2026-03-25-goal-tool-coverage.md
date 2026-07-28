# Workflow Gap Seeding Report

Date: `2026-03-25`
Status: `current seeding pass`
Workspace Root: `NexusCentral-Builder`
Author: `Codex`

## Reviewed Existing Coverage First

Workflow inventory reviewed:

- `automations/active/`
- `automations/experimental/`
- `automations/disabled/`

Templates, docs, goals, and lessons reviewed:

- `templates/workflow_templates/`
- `docs/conventions/`
- `docs/examples/`
- `docs/usage/`
- `goals/business_goals/`
- `goals/project_goals/`
- `goals/workflow_targets/`
- `docs/project_breakdowns/`
- `lessons/`
- `improvements/proposals/`

Tool inventory check:

- `tools/experimental_tools/` is absent
- `tools/final_tools/` is absent
- `proposals/tool_proposals/` is absent

## Existing Families Already Covered

The current library already has credible coverage for:

- workspace exploration, planning, intake normalization, setup briefs, and docs reconciliation
- workflow maintenance through delta scans, handoff reconciliation, overlap review, placeholder audits, template-gap review, and catalog refresh
- business-facing workflow families for workspace hygiene, portfolio refresh, factory export readiness, human-view evidence, release evidence packets, and tool-surface contract audits

That means the remaining gaps are not generic governance gaps. The real thin spots are:

1. Library-level proof of which goals and workflow targets are actually covered by the current workflow set.
2. Shared validation infrastructure readiness between human-view planning and release-evidence work.
3. Tool-heavy extensions suggested by the goals that should not be promoted until a real tool inventory exists.

## Gaps Handled As Live Experimental Workflow Seeds

Created in `automations/experimental/`:

- `workflow_goal_coverage_audit`
  Reason: the library had `goal_reconciliation` for a single incoming request, but no workflow that audits the whole library against business goals, workflow targets, and project-goal evidence.
  Why it is not a duplicate: it is library-wide, not request-specific, and it explicitly separates workflow-shaped gaps from tool-shaped gaps.
- `validation_infrastructure_readiness_review`
  Reason: the human-view and release-evidence family had proof-planning workflows, but no shared readiness pass for screenshot pipelines, launch bridges, overlays, service health, and evidence export shape.
  Why it is not a duplicate: it sits upstream of `human_view_evidence_brief` and `release_evidence_packet_seed` instead of replacing either one.

## Gaps Kept As Tool-Aware Workflow Proposals

Created in `proposals/workflow_proposals/`:

- `tool_wrapper_onboarding_brief`
  Reason: the tool-surface family currently stops at contract audit even though the business goals also call for wrapper onboarding and discoverability support.
  Why it stays proposed: route, schema, and health-surface compilation should be backed by a real tool inventory first.
- `demo_refresh_sanitization_brief`
  Reason: the release family currently covers evidence packets, but the goals also call out demo refresh and sanitization review as repeated manual work.
  Why it stays proposed: public-shareability and artifact-boundary review are too inspection-heavy to treat as reliable without supporting tools.

## Template And Example Support Added

Created reusable templates:

- `templates/workflow_templates/goal_linked_review_template.yaml`
- `templates/workflow_templates/tool_aware_review_template.yaml`

Created example guidance:

- `docs/examples/goal-coverage-and-tool-aware-loop.md`

## Workflow Versus Tool Boundary Decisions

- A gap became a workflow seed when a report, matrix, and follow-up queue could describe the work honestly with the current repo surface.
- A gap stayed proposed when the workflow would otherwise imply reliable extraction, comparison, or artifact inspection that the current workspace cannot actually support.
- No new tool proposal files were written because the requested pass was for workflow seeding, but the report now leaves the missing tool surfaces explicit instead of hiding them inside vague future-work notes.

## Created File Set

- `automations/experimental/workflow_goal_coverage_audit.yaml`
- `automations/experimental/validation_infrastructure_readiness_review.yaml`
- `proposals/workflow_proposals/tool_wrapper_onboarding_brief.yaml`
- `proposals/workflow_proposals/demo_refresh_sanitization_brief.yaml`
- `templates/workflow_templates/goal_linked_review_template.yaml`
- `templates/workflow_templates/tool_aware_review_template.yaml`
- `docs/examples/goal-coverage-and-tool-aware-loop.md`
- `docs/usage/workflow-gap-seeding-report-2026-03-25-goal-tool-coverage.md`

## Next Review Focus

- decide whether the new goal-coverage audit should become the standard precursor to future gap-seeding passes
- confirm that validation infrastructure really stays separate from per-project evidence briefs after one or two real uses
- create or catalog actual tool proposals before promoting the two tool-aware workflow proposals into `automations/experimental/`
