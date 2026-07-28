# Workflow Maintenance Loop

Date: 2026-03-25

This example shows a maintenance-oriented path for keeping a growing workflow catalog understandable after several seeding passes have already happened.

## Example Scenario

Goal: review a broadened workflow inventory, check whether similar workflows should stay separate, repair stale references, and then refresh the human-readable catalog.

## Recommended Flow

1. Run `workspace_delta_scan` to isolate what changed since the last reviewed baseline.
2. Run `workflow_review_gate` to decide whether the changed set is ready for wider catalog maintenance work.
3. Run `workflow_overlap_reconciliation` if the changed set now contains adjacent workflow specs with similar purpose or outputs.
4. Run `workflow_handoff_reconciliation` to find stale ids in handoff lists, examples, and usage notes.
5. Run `workflow_catalog_refresh` to update overview and example docs only after overlap and reference issues are understood.

## Expected Artifacts By Stage

- Delta stage: a narrowed change report and a small follow-up list.
- Gate stage: a concise proceed, refine, or defer recommendation.
- Overlap stage: merge, keep, rename, or narrow recommendations for nearby workflows.
- Handoff stage: a targeted fix queue for stale workflow references.
- Catalog stage: refreshed workflow inventory notes and example follow-ups.

## Why This Stays Modular

- Delta scan keeps the maintenance pass smaller than a full re-exploration.
- Review gate keeps workflow-quality and lifecycle evidence in one current pass before broader maintenance decisions.
- Overlap reconciliation checks scope boundaries without rewriting the whole inventory.
- Handoff reconciliation checks reference integrity without taking over catalog maintenance.
- Catalog refresh remains the last step so it can incorporate the resolved boundary and naming decisions.
