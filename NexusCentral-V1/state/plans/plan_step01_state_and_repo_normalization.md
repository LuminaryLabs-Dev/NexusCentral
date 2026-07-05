# Step 01: State And Repo Normalization

## One Sentence

Normalize repo/state docs so the active app, automation outputs, naming, and ignored generated files are truthful before feature work continues.

## State Observes

- Current repo branch is `development`, with no commits yet and untracked `NexusCentral-V0/` and `NexusCentral-V1/`.
- Active app content is under `NexusCentral-V1/`.
- Existing state docs still describe paths as `NexusCentral-V1/...`.
- Automations were updated to resolve `ACTIVE_VERSION_DIR`, but existing reports still contain stale V1 wording.
- `Desktop/bin`, `Desktop/obj`, `Tests/bin`, and `Tests/obj` exist under the active app folder.
- Active app has `ARCHITECTURE.md`, but no active `memory.md`, `goal.md`, `Questions.md`, or `DESIGN_DNA.md`.
- Deprecated/reference naming is confusing because `NexusCentral-V1/Desktop/NexusCentral-V1.csproj` still contains `V1` in the project name.

## Exact Changes To Plan

- Add active lane docs:
  - `NexusCentral-V1/memory.md`
  - `NexusCentral-V1/goal.md`
  - `NexusCentral-V1/Questions.md`
  - `NexusCentral-V1/DESIGN_DNA.md`
- Update state docs from hard-coded `NexusCentral-V1` wording to latest-active-version wording.
- Add or update ignore rules for:
  - `**/bin/`
  - `**/obj/`
  - `.DS_Store`
  - temporary automation screenshots if they are not intended to be committed.
- Decide whether project files should be renamed:
  - current: `Desktop/NexusCentral-V1.csproj`
  - possible: `Desktop/NexusCentral.csproj` or `Desktop/NexusCentral-V1.csproj`
- Keep existing state evidence files; do not delete historical proof just because wording is stale.
- Add a short note in `ARCHITECTURE.md` explaining that folder version names are repository lanes, not product architecture names.

## Hero Controls / User-Facing Impact

- No app UI changes in this step.
- User impact is repo clarity and automation output correctness.

## Advanced Controls / Deferred

- Do not rename project files until after deciding whether the app should expose a neutral product name or explicit version name.
- Do not delete `NexusCentral-V0` or old evidence.
- Do not commit generated outputs.

## Validation

- Run `rg -n "NexusCentral-V1/state|Inspect NexusCentral-V1|Write .*NexusCentral-V1" NexusCentral-V1/state`.
- Run `find NexusCentral-V1 -path '*/bin/*' -o -path '*/obj/*'`.
- Run `git status --short`.
- Confirm automations no longer create `NexusCentral-V1/state`.

## Done Criteria

- Active lane docs exist.
- State docs no longer instruct future runs to write into hard-coded `NexusCentral-V1/state`.
- Generated outputs are ignored or clearly untracked.
- Future steps can reference `latest active version` without ambiguity.
