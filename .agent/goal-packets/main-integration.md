# Main Integration Handoff

Status: BLOCKED

## Goal

Integrate all intended current NexusCentral work through the existing pull
request into the repository default branch while preserving ownership,
validation evidence, and the separate `gh-pages` deployment branch.

## Observed State

- Observed: `2026-07-28T12:25:33-0400 (EDT)`
- Repository: `LuminaryLabs-Dev/NexusCentral` (public)
- Worktree: `/Users/crimsonwheeler/Documents/GitHub/NexusCentral`
- Git common directory: `.git`
- Registered worktrees: `1`
- Source branch: `agent/publish-current-work-20260713`
- Source HEAD: `e6e73ebbfdaba0b5a7282c13e1eb0e503d642792`
- Upstream: `origin/agent/publish-current-work-20260713`
- Default and target branch: `main`
- Local and remote `main`: `551c647440a540853a2d0dc1f5291b7080a3a96b`
- Local divergence from `main`: `0 behind / 1 ahead`
- Remote source branch: `e6e73ebbfdaba0b5a7282c13e1eb0e503d642792`
- Protected deployment ref: `gh-pages@e09c80b03d87747aafea756cdd634bd01c22664d`
- Pull request: `https://github.com/LuminaryLabs-Dev/NexusCentral/pull/1`
- Pull request state: open draft, base `main`, head
  `agent/publish-current-work-20260713`, mergeable and clean
- Pull request currently contains only `e6e73eb`; its existing successful checks
  predate every local change in this handoff.

Before this handoff file was added, Git reported 282 visible entries: 21
modified tracked files and 261 untracked files. One additional imported
`.gitkeep` is ignored by the root `artifacts/` rule and must be handled
explicitly. The pre-handoff visible-tree content fingerprint was
`cd5d92f6473ad4fc2c8a9a6a145d59e86bfb2faa6648259771dffd796d9f4f42`.

## Ownership

- The 23 V2 implementation files and six July 19 browser evidence files came
  from the user-approved NexusCentral V2 execution-server task in Codex task
  `019f22cc-917d-7581-9a49-ebf1b6dc1df8`.
- The three V2 `.agent` files record a later streamlined-UI intake in that same
  Codex task. The intake remains `in_progress`; these are planning records, not
  authorization for additional implementation.
- The root `.agent/organize/` import belongs to task
  `NC-20260724-BUILDER-IMPORT`, originating from Codex task
  `019f93c2-2284-72e0-b97b-3c57f6b9c56b`. The imported copy is validated; only
  manual removal of the retired Codex project entry remains.
- Both originating Codex tasks are completed/not loaded. No other recent active
  Codex task uses the NexusCentral checkout.
- There is one Git worktree and no competing checked-out NexusCentral branch.

Ownership is clear for preparation. Existing task boundaries remain visible
through separate commits and are not reinterpreted as new product approval.

## Classification And Inclusion

Every current candidate path is approved for a local preparation commit under
the class below. Prompt 2 must stage by explicit path, never with
`git add -A`.

### NexusCentral V2 Implementation: 23 Files

- `NexusCentral-V2/adapters/runtime-adapters/index.mjs`
- `NexusCentral-V2/adapters/runtime-adapters/src/artifact-store.mjs`
- `NexusCentral-V2/adapters/runtime-adapters/src/harness-executors.mjs`
- `NexusCentral-V2/adapters/runtime-adapters/src/macos-service.mjs`
- `NexusCentral-V2/adapters/runtime-adapters/src/process-harness.mjs`
- `NexusCentral-V2/adapters/runtime-adapters/src/provider-http.mjs`
- `NexusCentral-V2/adapters/runtime-adapters/src/simspace-experiment.mjs`
- `NexusCentral-V2/apps/web/app/page.jsx`
- `NexusCentral-V2/apps/web/app/styles.css`
- `NexusCentral-V2/kits/artifacts-kit/index.mjs`
- `NexusCentral-V2/kits/experiments-kit/index.mjs`
- `NexusCentral-V2/kits/harnesses-kit/index.mjs`
- `NexusCentral-V2/kits/jobs-kit/index.mjs`
- `NexusCentral-V2/kits/routing-kit/index.mjs`
- `NexusCentral-V2/package.json`
- `NexusCentral-V2/packages/central-client/index.mjs`
- `NexusCentral-V2/packages/contracts/index.mjs`
- `NexusCentral-V2/scripts/check.mjs`
- `NexusCentral-V2/scripts/integration-check.mjs`
- `NexusCentral-V2/scripts/macos-service.mjs`
- `NexusCentral-V2/services/nexus-node/src/lan-server.mjs`
- `NexusCentral-V2/services/nexus-node/src/nexus-node.mjs`
- `NexusCentral-V2/services/nexus-node/src/run-coordinator.mjs`

Intent: complete the user-approved execution-server slice: canonical runs,
artifact persistence, harness execution, provider routing, macOS service
control, LAN/API coordination, client contracts, checks, and Review UI.

### Root Agent Coordination: 6 Files After Part 2

- `.agent/change-log.md`
- `.agent/start-here.md`
- `.agent/workflow.md`
- `.agent/tasks.jsonl`
- `.agent/goal-packets/main-integration.md`
- `.agent/organize/.gitattributes`

Intent: preserve the Builder import record, this integration handoff, and the
narrow path-specific whitespace policy required to retain the exact imported
payload.

### Imported Builder Provenance: 247 Files On Disk

- `.agent/organize/README.md`
- `.agent/organize/import-manifest.json`
- `.agent/organize/checksums.sha256`
- All 244 files enumerated by `.agent/organize/checksums.sha256` under
  `.agent/organize/builder/`

The 244 payload checksums revalidated `244/244`. Git currently exposes 243
payload files plus the three wrapper files. The remaining payload file,
`.agent/organize/builder/artifacts/.gitkeep`, is ignored by `.gitignore:20`
(`artifacts/`) and must be added explicitly with `git add -f` so the committed
snapshot still matches its 244-file manifest.

Intent: retain the validated, read-only legacy Builder planning library. Its
historical lifecycle labels do not activate workflows.

### V2 Agent Coordination: 3 Files

- `NexusCentral-V2/.agent/change-log.md`
- `NexusCentral-V2/.agent/streamlined-ui-intake.md`
- `NexusCentral-V2/.agent/tasks.jsonl`

Intent: preserve the unresolved UI/workflow design intake as planning history.
Do not implement or mark that intake complete during integration.

### Historical Validation Evidence: 6 Files

- `NexusCentral-V2/.playwright-cli/page-2026-07-19T08-29-11-597Z.yml`
- `NexusCentral-V2/.playwright-cli/page-2026-07-19T08-29-48-080Z.yml`
- `NexusCentral-V2/.playwright-cli/page-2026-07-19T08-32-40-437Z.yml`
- `NexusCentral-V2/.playwright-cli/page-2026-07-19T08-33-33-126Z.yml`
- `NexusCentral-V2/.playwright-cli/page-2026-07-19T08-35-11-836Z.yml`
- `NexusCentral-V2/.playwright-cli/page-2026-07-19T08-36-36-216Z.png`

Intent: retain the existing execution-server browser proof. The repository
already tracks the same `.playwright-cli` evidence class from July 13. Fresh
Part 2 evidence is validation-only and is excluded from commits unless it is
separately added to this handoff and re-scanned.

## Exclusions

- Never stage, merge, delete, or rewrite the `gh-pages` branch.
- Exclude all files not enumerated above or by the 244-entry Builder checksum
  manifest.
- Exclude future caches, dependencies, build output, logs, temporary files,
  browser profiles, authentication state, environment files, and credentials.
- Exclude fresh Part 2 screenshots and snapshots by default.
- Do not change V1, the prior Electron app, repository workflows, or product
  implementation during preparation.

At Part 1, no existing visible status entry was excluded. The ignored Builder
`.gitkeep` was explicitly included. Seven fresh Part 2 Playwright files are
excluded and listed in the preparation result below.

## Publication Hazard Scan

- Scanned all 282 visible files; the ignored `.gitkeep` is empty.
- High-confidence private keys or provider tokens: `0`
- Credential-bearing URLs or sensitive query parameters: `0`
- Email, long encoded-value, and machine-specific path findings: `0`
- Suspicious credential filenames: `0`
- Changed symlinks: `0`
- `.DS_Store` files: `0`
- Files at or above 1 MiB: `0`
- Binary files: one intentional PNG, 189,171 bytes
- Generic credential assignment: one test fixture,
  `apiKey: "integration-secret"`, in `scripts/integration-check.mjs`
- Initial tracked-file `git diff --check`: clean; the complete staged import
  later exposed six provenance-preserved EOF findings resolved below
- Builder checksum verification: `244/244`

No publication blocker was found.

## Required Local Commit Groups

1. V2 execution-server implementation: exactly the 23 implementation files.
2. Historical browser evidence: exactly the six listed evidence files.
3. Agent workspace and Builder provenance: the root and V2 `.agent` files plus
   all 247 imported Builder paths, including the force-added ignored `.gitkeep`.
4. Final coordination and the six-path Builder whitespace policy:
   `.agent/goal-packets/main-integration.md`, `.agent/tasks.jsonl`,
   `.agent/change-log.md`, and `.agent/organize/.gitattributes`.

Prompt 2 may update this handoff, task ledger, and change log before creating
the final agent-workspace commit. It must not rewrite implementation to make a
failed check pass.

## Required Validation

Run from `NexusCentral-V2`:

```bash
npm run check
npm run check:integration
npm run check:scale
npm run build
npm run simulate:contracts
npm run simulate:browser
```

Also require:

- `git diff --check`
- staged-path review against this handoff
- post-commit secret, binary, personal-path, and accidental-file scan
- start `npm run dev`, inspect `http://127.0.0.1:47800`, and perform fresh
  Playwright/Human View checks of the changed Review surface at desktop and
  mobile widths with no console or page errors

`npm run check:lm-studio` and `npm run check:harness-live` depend on live
external runtimes and are not promotion requirements unless those runtimes are
configured during Prompt 2.

## Prompt 2 Acceptance Gate

Prompt 2 may begin only when all of these remain true:

- This handoff status is `READY_FOR_PREPARATION`.
- Branch is `agent/publish-current-work-20260713`.
- HEAD is `e6e73ebbfdaba0b5a7282c13e1eb0e503d642792`.
- `origin/main` is still
  `551c647440a540853a2d0dc1f5291b7080a3a96b`.
- Exactly one NexusCentral worktree remains registered.
- The payload fingerprint recorded below matches when calculated over every
  visible dirty file except this handoff file.
- The ignored Builder `.gitkeep` still exists and remains the only approved
  ignored payload file.
- No active competing NexusCentral owner appears.

Payload fingerprint after Part 1:
`0490c73844d01480c2890f9f406e0e3382f5be48746310a62a8b39e5778c121c`

If any gate fails, set this handoff to `BLOCKED`, append the discrepancy to the
task ledger and change log, and stop without staging.

## Next Permitted Action

Do not push while the Part 3 deployment-side-effect blocker below remains
unresolved. Return to Prompt 2 if a workflow change is authorized, or resume
Prompt 3 only if an automatic deployment-only `gh-pages` update is explicitly
accepted as compatible with the protected-branch boundary.

## Part 2 Preparation Result

- Prepared: `2026-07-28T12:39:28-0400 (EDT)`
- Branch: `agent/publish-current-work-20260713`
- Original branch HEAD:
  `e6e73ebbfdaba0b5a7282c13e1eb0e503d642792`
- Remote source branch remained:
  `e6e73ebbfdaba0b5a7282c13e1eb0e503d642792`
- Remote target remained:
  `origin/main@551c647440a540853a2d0dc1f5291b7080a3a96b`
- Protected deployment ref remained:
  `origin/gh-pages@e09c80b03d87747aafea756cdd634bd01c22664d`
- Registered worktrees remained: `1`
- No competing owner appeared.
- The accepted payload fingerprint matched before staging.
- No push, PR mutation, branch change, rebase, merge, or `gh-pages` mutation was
  performed.

### Local Commits

1. `a05da3abb19f7c3ad7d79da71dd679d971c60cdc`
   - Subject: `Complete NexusCentral V2 execution server`
   - Files: exactly the 23 approved V2 implementation paths
2. `f0bcaca1ed4d7055df34f34a14c6476677f1efad`
   - Subject: `Add NexusCentral V2 browser validation evidence`
   - Files: exactly the six approved July 19 Playwright evidence paths
3. `6e48356412daa918b35b868afe515c48c4b2d1f0`
   - Subject: `Preserve NexusCentral agent workspace and Builder provenance`
   - Files: exactly 255 approved paths: five root coordination files, 247
     imported Builder paths, and three V2 coordination files
4. `711b6f44668384de22dd58fa2b52751e4679fee2`
   - Subject: `Record NexusCentral publication gate blocker`
   - Files: three coordination files recording the initially discovered
     Builder EOF gate

The final coordination commit follows these four commits and adds the
path-specific whitespace policy. Because a commit cannot contain its own SHA,
the authoritative final-commit identity is the local lightweight tag
`nexuscentral-main-integration-ready-20260728`. Prompt 3 must require that tag
and `HEAD` to resolve to the same commit before publishing. The tag itself is
local validation metadata and must not be pushed.

### Validation Results

All product and runtime checks passed from `NexusCentral-V2`:

- `npm run check`: passed, including the 19-domain graph, deny-by-default
  routing, leases, host routing, ordered streams, safe Codex flags, browser
  scopes, snapshot round trip, Next.js build, and Electron syntax checks
- `npm run check:integration`: passed all provider, retry, experiment, repair,
  approval, recovery, persistence, browser lease, workspace, libp2p, and
  PeerJS cases
- `npm run check:scale`: passed with 50 nodes, 1,000 queued jobs, churn
  convergence, bounded connections, and 76 MB heap use
- `npm run build`: passed the Next.js static and Electron shell builds
- `npm run simulate:contracts`: passed `19/19`
- `npm run simulate:browser`: passed rendering, input, visual-state change,
  retained trace/video, and zero console errors
- Fresh Playwright/Human View: passed at `1440x900`, `720x900`, and `390x844`;
  the Review surface rendered completed runs, artifacts, downloads, and audit
  receipts with no clipping, horizontal overflow, console errors, or warnings
- Historical evidence binary review: one intentional 189,171-byte PNG
- High-confidence secret and personal-path scan of intended publication
  content: no unexplained findings
- Builder manifest: all 244 payload paths match and all 244 checksums verify
- Implementation and historical-evidence range `git diff --check`: passed
- Complete publication range `git diff --check`: passed with the committed,
  path-specific Builder policy described below

### Resolved Whitespace Policy

The first complete default `git diff --check` reported `new blank line at EOF`
for six checksummed, read-only Builder Markdown files:

- `.agent/organize/builder/docs/examples/tool_and_workflow_composition.md`
- `.agent/organize/builder/docs/usage/tool-layer-before-workflow-guidance.md`
- `.agent/organize/builder/proposals/tool_proposals/artifact_contract_validator.md`
- `.agent/organize/builder/proposals/tool_proposals/media_evidence_packager.md`
- `.agent/organize/builder/proposals/tool_proposals/portfolio_signal_normalizer.md`
- `.agent/organize/builder/proposals/tool_proposals/tool_surface_snapshotter.md`

Changing those six payload files would have broken the byte-for-byte provenance
contract. A versioned `.agent/organize/.gitattributes` now assigns only these
paths:

```bash
whitespace=trailing-space,-blank-at-eof,space-before-tab
```

This retains checks for trailing spaces and spaces before indentation tabs while
disabling only `blank-at-eof` for the six historical files. The rule was proven
against a temporary commit object before staging. The exact full publication
range now passes the unmodified `git diff --check`, and the Builder manifest
still verifies `244/244`. No payload byte or checksum changed.

### Remaining Excluded Changes

Fresh Part 2 Playwright evidence remains untracked and excluded:

- `NexusCentral-V2/.playwright-cli/attempt-1-result.json`
- `NexusCentral-V2/.playwright-cli/page-2026-07-28T16-32-29-928Z.yml`
- `NexusCentral-V2/.playwright-cli/page-2026-07-28T16-32-48-012Z.yml`
- `NexusCentral-V2/.playwright-cli/page-2026-07-28T16-33-03-201Z.png`
- `NexusCentral-V2/.playwright-cli/page-2026-07-28T16-33-16-482Z.yml`
- `NexusCentral-V2/.playwright-cli/page-2026-07-28T16-33-30-448Z.png`
- `NexusCentral-V2/.playwright-cli/page-2026-07-28T16-33-49-681Z.png`

Ignored validation output remains under:

- `NexusCentral-V2/.simspaces/`
- `NexusCentral-V2/apps/simulator-proof/dist/`
- `NexusCentral-V2/apps/web/.next/`
- `NexusCentral-V2/output/`

### Publication Gate

All approved content is committed locally, all required product and human-view
checks pass, the complete default whitespace check passes, Builder provenance
remains exact, and only the seven documented excluded paths remain. Prompt 3
may publish only the commit identified by
`refs/tags/nexuscentral-main-integration-ready-20260728`.

## Part 3 Preflight Blocker

- Observed: `2026-07-28T12:50:13-0400 (EDT)`
- Live default branch: `main`
- Fetched `origin/main`:
  `551c647440a540853a2d0dc1f5291b7080a3a96b` (unchanged)
- Local validated tip and ready tag:
  `c2d5c47dcdbe669a98103582f08e49ce0a6b15a0`
- Remote source branch:
  `e6e73ebbfdaba0b5a7282c13e1eb0e503d642792` (unchanged)
- Protected deployment branch:
  `origin/gh-pages@e09c80b03d87747aafea756cdd634bd01c22664d`
  (unchanged)
- PR #1 remains open and draft with base `main`, the expected source branch,
  and remote head `e6e73ebb`.
- GitHub authentication is valid as `thecrimsondeveloper`.
- Worktree ownership remains singular and status contains only the seven
  excluded Playwright files.
- No push, PR mutation, merge, branch deletion, or `gh-pages` mutation occurred.

### Incompatible Live Gates

The current `.github/workflows/nexuscentral-pages.yml` runs on every branch push
that changes `NexusCentral-V*/**`. The validated branch contains V2 changes, so
publishing it will start `NexusCentral Pages Downloads`. Its `publish-pages`
job calls `.github/scripts/update_pages_downloads.py` with the branch name,
commit SHA, run ID, and timestamp, commits the resulting site, and pushes
directly to `gh-pages`.

This is not hypothetical: the current protected ref is commit
`e09c80b03d87747aafea756cdd634bd01c22664d`, titled
`Update NexusCentral downloads for agent/publish-current-work-20260713`. Its
branch record embeds the current remote source SHA `e6e73ebb`. A push of the
validated tip would replace that record with `c2d5c47` and necessarily advance
`gh-pages`.

Therefore both of these objective requirements cannot currently be true:

1. Let every new check finish green before merge.
2. Verify `gh-pages` remains at its accepted unchanged ref.

Canceling or bypassing the Pages workflow would violate the green-check gate.
Allowing it to finish would violate the unchanged-ref gate. Reverting its output
would touch `gh-pages`, and force-restoring the ref is expressly forbidden.

### Resolution Required

Choose one policy:

1. Accept the normal automated deployment update as compatible with the
   boundary, while still proving that no `gh-pages` commit or content enters
   `main`. Prompt 3 can then publish and verify the new deployment commit
   independently.
2. Return to Prompt 2 and authorize a reviewed workflow change that prevents
   V2/agent-only pushes from publishing V1 download output. This changes a path
   explicitly excluded by the accepted handoff and therefore cannot be inferred
   during publication.

Option 1 preserves existing CI/CD behavior. Option 2 preserves the exact
`gh-pages` ref but changes deployment policy.
