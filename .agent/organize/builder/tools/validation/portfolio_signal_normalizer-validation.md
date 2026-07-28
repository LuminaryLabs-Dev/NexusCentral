# Tool Validation Note: `portfolio_signal_normalizer`

Date: `2026-03-26`
Status: `prepared for experimental review`

## Placement Decision

- `proposal_source`: `proposals/tool_proposals/portfolio_signal_normalizer.md`
- `tool_spec`: `tools/experimental_tools/portfolio_signal_normalizer.yaml`
- `template_asset`: `tools/tool_templates/portfolio_signal_normalizer_config_template.yaml`
- `example_usage_docs`: `docs/examples/tool-package-cli-examples.md`, `docs/examples/tool-package-template-examples.md`
- `current_decision`: Keep this tool in `experimental_tools/` because it is valuable, but extraction rules are still more variable than the other tool candidates and need cross-repo proof.

## Contract Checks To Run First

- Confirm the command surface stays limited to `normalize`.
- Confirm every normalized record cites canonical source paths and that drift findings cite both canonical and mirror sources when applicable.
- Confirm `owner-summary-seed.md` remains deterministic and derived from extracted fields rather than editorial logic.
- Confirm the tool never rewrites source docs.

## Minimum Fixture Coverage

- A ranked-initiative fixture from `LuminaryInitiatives` with mirrored owner-summary fields that proves extraction and drift comparison.
- A mixed-status fixture including missing required fields and archived material to prove gap reporting and archive filtering.

## Workflow Handshake To Verify

- Run this tool before `portfolio_signal_refresh` and optionally before `proposal_backlog_triage` when portfolio evidence should shape backlog ranking.
- Downstream workflows should consume `portfolio-signals.json`, `mirror-drift.json`, and `owner-summary-seed.md`.
- The workflow still owns business prioritization, archive judgment, and final narrative editing.

## Promotion Gate

- Promote only after extraction rules remain stable across at least two portfolio roots without repo-specific exceptions.

## Current Risks

- Field extraction could become brittle if early inputs vary too much in markdown structure.
- Summary seed generation may drift toward workflow behavior if it starts ranking or editorializing.
