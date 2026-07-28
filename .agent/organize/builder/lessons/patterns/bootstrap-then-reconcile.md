# Bootstrap Then Reconcile

Date: `2026-03-25`

## Pattern

Create the minimum shared folder structure first, then run an explicit reconciliation pass before seeding actual workflows.

## Why It Helps

- It preserves a stable workspace contract early.
- It keeps later cleanup small because lifecycle rules are documented before content spreads.
- It makes empty buckets intentional instead of looking like unfinished accidental structure.

## Reuse Guidance

Repeat this pattern when starting a new planning workspace: scaffold first, reconcile second, author workflows third.
