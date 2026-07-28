# Tool Proposal: `portfolio_signal_normalizer`

- `proposal_id`: `portfolio_signal_normalizer`
- `recommended_initial_placement`: `tools/experimental_tools/`
- `recommended_maturity`: `experimental`
- `priority`: `medium`

## Problem

The planning layer already recognizes mirror drift and ranked-portfolio maintenance as repeated work, but the workspace has no tool that converts owner-facing markdown, ledgers, and project summaries into a normalized machine-readable portfolio snapshot for later workflows to reason over.

That gap is visible in:

- `portfolio_signal_refresh`
- `proposal_backlog_triage`

And in sibling project evidence:

- [`../LuminaryInitiatives/docs/executive-summary.md`](<github-workspace>/LuminaryInitiatives/docs/executive-summary.md) calls out mirror drift explicitly.
- [`../LuminaryInitiatives/docs/state-of-the-business.md`](<github-workspace>/LuminaryInitiatives/docs/state-of-the-business.md) describes normalized records as a core operating layer.
- [`../LuminaryInitiatives/projects/release-evidence-hub/README.md`](<github-workspace>/LuminaryInitiatives/projects/release-evidence-hub/README.md) and the adjacent stage files show structured pipeline surfaces that would benefit from normalized indexing.

## Business Use Case

Prepare structured portfolio data before owner-summary refreshes, drift checks, or commercialization prioritization reviews.

Typical examples:

- normalize ranked initiatives, forecasts, and stage metadata into one comparison table
- detect owner-summary drift against canonical portfolio files
- prepare structured inputs for later brief-writing workflows

## Why A Tool Is Better Than A Workflow-Only Solution

The repeated work here is parsing, field extraction, normalization, and diffing. A tool can:

- pull structured records from ledgers and markdown headings
- compare canonical sources to mirrors
- emit a stable JSON layer for later summarization
- make portfolio workflows consume facts instead of re-parsing prose every run

## Suggested Inputs

- one or more portfolio roots
- config for canonical source files versus mirror files
- optional extraction rules for headings, key-value bullets, and ledger tables
- output directory

## Suggested Outputs

- `portfolio-signals.json`
- `mirror-drift.json`
- `owner-summary-seed.md`
- `source-index.json`

## Core Behaviors

- extract initiative names, priorities, rankings, forecast ranges, and stage metadata
- compare canonical and mirrored docs
- emit structured records suitable for later report generation
- highlight missing fields and stale mirrors without mutating the sources

## Validation Expectations

- every normalized record points back to its source path
- drift findings name canonical and mirror sources explicitly
- the tool remains read-only and does not rewrite summary docs

## Workflow Dependencies And Beneficiaries

Use this tool before:

- `portfolio_signal_refresh`
- future proposal or commercialization backlog workflows

## Non-Goals

- deciding business ranking automatically
- replacing owner judgment
- rewriting portfolio docs directly

