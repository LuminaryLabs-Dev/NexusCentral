# Tool Proposal: `publish_bundle_validator`

- `proposal_id`: `publish_bundle_validator`
- `recommended_initial_placement`: `tools/experimental_tools/`
- `recommended_maturity`: `experimental`
- `priority`: `highest`

## Problem

The current tool layer can package evidence and validate declared artifact contracts, but public-share bundle safety is still mostly manual. That leaves a gap between "the files exist" and "the public payload is actually safe to publish."

That gap is visible in:

- `release_evidence_packet_seed`
- proposed `demo_refresh_sanitization_brief`

And in sibling project evidence:

- [`../ProjectArrowhead-Demo/README.md`](<github-workspace>/ProjectArrowhead-Demo/README.md) explicitly separates sanitized public `build/` output from private generated artifacts.
- [`../ProjectArrowhead-Demo/outputs-to-externalize/README.md`](<github-workspace>/ProjectArrowhead-Demo/outputs-to-externalize/README.md) lists raw state, SQL exports, and private manifests that must stay out of the public tree.
- [`../VendingBackpack/README.md`](<github-workspace>/VendingBackpack/README.md) shows a multi-surface release shape with public UI, backend health, and deployment configuration that becomes risky when publish boundaries are checked informally.

## Business Use Case

Validate that a stakeholder-facing demo or release bundle is public-safe before a workflow writes the readiness brief.

Typical examples:

- confirm a demo build contains the intended public payload while keeping seeded passwords, SQL exports, claim pins, and private manifests out of the published tree
- verify that only approved asset classes, file extensions, and metadata fields ship in a shareable review bundle
- produce a machine-readable leak report before a release packet is sent outside the team

## Why A Tool Is Better Than A Workflow-Only Solution

This is deterministic inspection work:

- inventory the public bundle
- compare it against allowlist and denylist rules
- scan filenames, metadata, and optional content signatures for sensitive markers
- compare the public payload against the externalized private payload
- emit exact leak findings and publish-blocking failures

A workflow should explain the business impact of a leak. The tool should be the layer that proves whether a leak exists.

## Suggested Inputs

- public bundle directory
- optional private or externalized artifact directory
- YAML or JSON rule file for allowlist, denylist, filename patterns, and sensitive markers
- optional expected public manifest
- output directory

## Suggested Outputs

- `publish-boundary-report.json`
- `publish-boundary-report.md`
- `leak-findings.json`
- `public-manifest.json`
- `unexpected-public-files.json`

## Core Behaviors

- inventory the full public bundle with relative paths, sizes, hashes, and types
- compare public files against declared allowed paths, extensions, and size ceilings
- flag likely secret or private artifacts such as raw exports, SQL dumps, credential files, or forbidden metadata fields
- compare the public tree against optional externalized outputs so private-only artifacts are not mirrored into the publishable bundle
- emit severity-based findings suitable for workflow gating

## Validation Expectations

- every finding cites the exact offending path and rule id
- public versus private boundary findings stay distinct from generic missing-file findings
- the tool stays read-only against both the public and private source trees
- repeated runs against the same payload produce stable manifests and stable finding ids

## Workflow Dependencies And Beneficiaries

Use this tool before:

- `release_evidence_packet_seed`
- proposed `demo_refresh_sanitization_brief`

It also sharpens the current handoff between `media_evidence_packager` and any later stakeholder-facing publish review.

## Non-Goals

- rewriting or deleting unsafe files automatically
- deciding legal or policy exceptions
- replacing broader release-readiness interpretation
