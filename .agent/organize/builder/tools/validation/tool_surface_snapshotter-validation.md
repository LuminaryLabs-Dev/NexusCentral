# Tool Validation Note: `tool_surface_snapshotter`

Date: `2026-03-26`
Status: `prepared for final placement`

## Placement Decision

- `proposal_source`: `proposals/tool_proposals/tool_surface_snapshotter.md`
- `tool_spec`: `tools/final_tools/tool_surface_snapshotter.yaml`
- `template_asset`: `tools/tool_templates/tool_surface_snapshotter_config_template.yaml`
- `example_usage_docs`: `docs/examples/tool-package-cli-examples.md`, `docs/examples/tool-package-template-examples.md`
- `current_decision`: Place this tool directly in `final_tools/` because the contract is read-only, narrowly scoped, and already anchored by a stable workflow target plus clear outputs.

## Contract Checks To Run First

- Confirm the command surface stays limited to `snapshot` and `diff`.
- Confirm every catalog entry records repo root, discovery path, surface type, and normalized identifier.
- Confirm diff output is computed from normalized identifiers rather than unstable file ordering.
- Confirm the tool never calls live endpoints or mutates the scanned repos.

## Minimum Fixture Coverage

- A repo-set fixture containing manifests, schemas, wrapper descriptors, route declarations, and README hints that proves mixed-surface extraction.
- A two-snapshot fixture that proves stable drift classification for added, removed, changed, and renamed surfaces.

## Workflow Handshake To Verify

- Run this tool before `tool_surface_contract_audit` and before `workflow_catalog_refresh` when tool-facing workflow coverage expands.
- Downstream workflows should read `tool-catalog.json`, `tool-catalog.md`, `missing-contract-signals.json`, and `drift-report.json`.
- The workflow still owns risk ranking, documentation-fix recommendations, and handoff prioritization.

## Promotion Gate

- No directory move is needed now; revisit placement only if implementation pressure expands the command surface beyond read-only cataloging.

## Current Risks

- Extractor coverage may vary across repos with inconsistent manifest and route conventions.
- README hint parsing could become noisy unless the implementation clearly prefers explicit contracts over prose hints.
