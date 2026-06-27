# Step 10: Release Hardening

## One Sentence

Harden release readiness with repeatable build, test, run, screenshot proof, clean source scan, and final docs.

## State Observes

- Build and tests are green.
- App launch proof exists, but older launch proof was partially blocked or stale.
- Later screenshots show visible app proof.
- State reports are useful but scattered.
- Generated outputs exist under active app folders.
- The repo has no commits yet on `development`.

## Exact Changes To Plan

- Add repeatable validation script or documented command set:
  - build
  - test
  - run
  - screenshot capture
  - source scan
  - generated-output scan
- Add final release docs:
  - active README
  - architecture summary
  - validation guide
  - known limitations
  - release checklist
- Add human-view proof expectations:
  - Lab opens by default
  - Lab selected in right rail
  - workspace tree visible
  - chat composer visible
  - console/status truthful
  - no fake-ready Router/Model/Agent/Automation claims
- Add source scan expectations:
  - no Dashboard primary tab
  - no Web Lab primary tab
  - no visible Integrations tab
  - no ticket module
  - no restored standalone Workflow page
  - no copied V2 source paths
- Decide commit boundary:
  - active app lane
  - reference lane
  - state docs
  - generated output excluded

## Hero Controls / User-Facing Impact

- User can run one documented validation flow and know whether the app is release-ready.

## Advanced Controls / Deferred

- Installer packaging.
- Code signing.
- Auto-update.
- Public release publishing.

## Validation

- Build passes.
- Tests pass.
- Native app opens.
- Screenshot proof shows intended shell.
- Git status contains only intended tracked source/docs.
- Generated outputs are ignored.

## Done Criteria

- Release readiness can be proven repeatably.
- Final docs explain current capabilities and limitations.
- Repo is clean enough to commit intentionally.
