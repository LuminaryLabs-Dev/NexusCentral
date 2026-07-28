# Review-Driven Workflow Loop

This example shows how the incremental review and maintenance workflows can stay small after the core inventory and planning layer is already in place.

## Example Scenario

Goal: evaluate a newly changed workflow set, decide whether any bucket placement should change, and refresh the human-readable catalog without rerunning a full workspace inventory.

## Recommended Flow

1. Run `workspace_delta_scan` to compare the current workspace against the most recent reviewed baseline.
2. Run `goal_reconciliation` if the incoming request is broad and needs a reuse-versus-seed decision before more authoring starts.
3. Run `workflow_review_gate` on the changed workflow set to produce a current lifecycle recommendation.
4. Run `workflow_catalog_refresh` so the inventory, overview, and example docs reflect the reviewed state.

## Why This Flow Exists

- `workspace_delta_scan` is cheaper than redoing a full exploration pass for every small change.
- `goal_reconciliation` prevents duplicate planning and duplicate workflow authoring.
- `workflow_review_gate` keeps review evidence and lifecycle recommendations together in one current workflow.
- `workflow_catalog_refresh` prevents the docs from drifting away from the YAML inventory.

## Optional Sidecar

Re-enable `state_last_run_contract` when multiple automations are ready to start writing normalized markers under `state/last_runs/` and the workspace still needs an agreed file shape first.
