# Workflow Validation Checklist

Use this checklist when reviewing a workflow specification, template, or seeded planning document.

## Workflow Shape

- The workflow has a clear `id`, name, purpose, and status.
- Inputs are separated into required and optional fields.
- Reads and writes are explicit enough to review without guessing.
- Steps are narrow and composable rather than broad and vague.

## Reusability

- The workflow targets a repeated task, not a one-off situation.
- Outputs are reusable templates, reports, or scaffolds.
- The workflow does not depend on undocumented runtime behavior.

## Validation

- Validation checks describe what a reviewer should confirm.
- Handoffs name the next likely workflow instead of collapsing multiple phases together.
- The workflow stays specification-only when implementation is intentionally out of scope.

## Documentation

- Supporting template paths are included where relevant.
- Usage docs explain where the workflow belongs in the overall process.
- Any deferred cleanup or reconciliation work is captured for later review.
