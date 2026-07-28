# Main Integration Handoff

Status: READY_FOR_PREPARATION

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

### Root Agent Coordination: 5 Files After Part 1

- `.agent/change-log.md`
- `.agent/start-here.md`
- `.agent/workflow.md`
- `.agent/tasks.jsonl`
- `.agent/goal-packets/main-integration.md`

Intent: preserve the Builder import record and this integration handoff.

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

No existing visible status entry is excluded. The ignored Builder `.gitkeep`
is explicitly included.

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
- Root `git diff --check`: clean
- Builder checksum verification: `244/244`

No publication blocker was found.

## Required Local Commit Groups

1. V2 execution-server implementation: exactly the 23 implementation files.
2. Historical browser evidence: exactly the six listed evidence files.
3. Agent workspace and Builder provenance: the root and V2 `.agent` files plus
   all 247 imported Builder paths, including the force-added ignored `.gitkeep`.

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

Prompt 2 may verify this packet, stage the three explicit groups, create local
commits, and run the required validation. It may not push, update PR #1, change
branches, rebase, merge, or touch `gh-pages`.
