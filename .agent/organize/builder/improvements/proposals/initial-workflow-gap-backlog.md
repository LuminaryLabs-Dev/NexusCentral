# Initial Workflow Gap Backlog

Date: `2026-03-25`
Status: `proposed`

## Purpose

This backlog captures the highest-value gaps that remain after workspace reconciliation. These are support gaps, not execution tasks.

## Priority Gaps

1. Produce validation evidence for `project_setup_brief` and `docs_reconciliation` so later promotion decisions are based on reviewed outputs rather than intent alone.
2. Define a lightweight promotion and rejection record format for workflows moving between `experimental/`, `active/`, `disabled/`, and `archive/`.
3. Decide whether `project_setup_brief` should remain separate from `initiative_plan` or collapse into a more constrained planning extension.
4. Document a lightweight file format for `state/last_runs/` once more than one automation begins writing shared run markers.
5. Define concrete merge, disable, and archive thresholds that `docs_reconciliation` should recommend instead of leaving those decisions fully situational.
6. Add more examples only when another credible multi-workflow composition exists and adds information beyond the current example.
7. Seed any missing lifecycle-support templates only when an actual review pass proves the current set is insufficient.

## Deferred By Design

- No additional workflow files were promoted here beyond the two clearest foundation specs because reconciliation should not invent validation history for weaker or more situational candidates.
- No polished outputs were generated because the workspace is still establishing review evidence and lifecycle confidence.
