# Template And Consistency Review Loop

Date: 2026-03-25

This example shows a narrower follow-up path for situations where the workflow inventory already exists but authoring quality still depends on better intake normalization, template coverage, and naming consistency.

## Example Scenario

Goal: take a large incoming workflow-authoring request, normalize it, confirm template support, seed clearer cleanup thresholds, and check placeholder consistency before another seeding pass lands.

## Recommended Flow

1. Run `request_intake_brief` when the incoming request is long enough that direct planning would waste time restating the same constraints.
2. Run `goal_reconciliation` if the normalized request still needs a reuse-versus-seed decision.
3. Run `workflow_template_gap_review` to compare the intended outputs against the current template inventory.
4. Run `docs_cleanup_threshold_seed` if the request depends on documentation cleanup decisions that are still too situational.
5. Run `workflow_placeholder_audit` after drafting or updating workflow specs so placeholder and output-path conventions stay aligned.
6. Run `workflow_gap_seeding` only for the remaining repeated work that is still uncovered after the earlier checks.
7. Run `workflow_review_gate` once the new or revised specs are ready for lifecycle review.

## Expected Artifacts By Stage

- Intake stage: a compact request brief, a constraint matrix, and a small follow-up list.
- Reconciliation stage: a reuse map plus only the truly uncovered remainder.
- Template stage: a template coverage note, a workflow-to-template matrix, and template follow-ups.
- Threshold stage: cleanup thresholds, a reviewer checklist, and illustrative examples.
- Placeholder stage: a consistency audit, placeholder matrix, and targeted fix queue.
- Seeding stage: new mock workflow specs plus a rationale report.
- Review stage: keep, refine, merge, or promote guidance for the resulting workflow set.

## Why This Stays Modular

- Intake normalization stays smaller than full planning.
- Template review stays separate from workflow authoring so missing templates do not get hidden inside another pass.
- Threshold seeding turns recurring documentation judgment into a reusable support layer.
- Placeholder auditing catches naming drift without taking over broader catalog maintenance.
- Gap seeding remains the last authoring step rather than the first reaction to every large request.
