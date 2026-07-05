# NexusCentral V1 Memory

## Purpose

`NexusCentral-V1` is the active app lane for the V1-native NexusCentral rebuild. The goal is a truthful local desktop control plane for Lab, Workspaces, Router, Models, Agents, Automations, and Settings.

## Architecture Shape

- `Desktop/` is the Avalonia host.
- `Core/` owns shared configuration, logging, state, and execution contracts.
- `Features/` owns user-facing product modules.
- `Integrations/Rpc/` is infrastructure and must not become primary navigation.
- `Integrations/Providers/` is the future provider adapter boundary.

## Major Conventions

- Resolve the active lane by required files, not by folder number.
- Treat folder names like `NexusCentral-V1` as repo lanes, not public product versions.
- Do not copy V2 source or view-model shapes into this lane.
- Use service-backed state or explicit inactive state; do not show static ready/available claims.
- Treat harnesses as service-owned imports with persisted capability selection before exposing execution controls.
- Harness orchestration is derived from all imported harnesses and enabled capabilities. The Agents surface should show a truthful orchestration plan before any future real queue/job runner executes multi-harness work.
- Harness orchestration smoke is service-owned and may execute only smoke-safe steps: manual hosted harness turns and Codex CLI read-only exec. Approval-gated capabilities stay skipped until a real approval/job queue exists.
- Harness orchestration smoke results are persisted in `harnesses/orchestration-smoke-history.json` so the latest run survives UI refresh/restart.
- Keep Codex CLI behind a small wrapper/job boundary; discovery is safe, workspace writes require approval-gated jobs.
- The first callable Codex CLI harness endpoint is read-only exec; workspace-write and app-server bridge stay approval-gated.
- The Models surface is endpoint/probe only; do not download models on this computer.
- The main local model endpoint is LM Studio OpenAI-compatible `http://10.0.0.38:1234/v1` with `lfm2.5-8b-a1b`; hosted harness commands can keep `http://10.0.0.38:1234` because the harness adapter appends `/v1/chat/completions`. Harness calls should use `16000` max tokens.
- Model endpoint identity is the normalized base URL plus selected model. Re-adding the same LM Studio target should reuse the existing record rather than creating duplicate setup rows.
- The Models surface includes a Hugging Face search view for discovery-only model lookup; downloader/storage work stays deferred.
- Router best-route selection is service-owned and persisted. When a healthy model endpoint and active hosted harness are both ready, prefer the `hosted-harness` route over the raw local/provider route; otherwise fall back to the best available local/provider route.
- Use `.agent/use-case-catalog/original-buildable-apps-catalog.json` as the broad app-validation catalog for choosing NexusCentral build and harness validation cases.
- Validation execution writes review artifacts under `validation/scenario-executions/<run>/`: `scoreboard.md`, `capability-execution-ranking.md`, `full-project-index.md`, `result-index.json`, progress files, per-scenario results, and per-project `project-proof.json` files.
- Validation scenario prompts must include the configured endpoint, model, and `16000` max-token budget in-band so harness output can be traced back to the intended LM Studio target.
- Validation execution also writes `learning-notes.md` in the run folder so output review produces a persistent next-improvement artifact, not just a score report.
- Desktop supports a headless `validate-scenarios` command for live hosted-harness validation without launching Avalonia; use `--count` for broad runs and `--ids` for targeted retries.
- Hosted harness output parsing should recover useful content from `assistant`, then `nextBest`, then `raw`; the Conversation Harness can split labeled evidence into `nextBest`.
- Lab validation job review should expose the selected run folder directly so scoreboards, rankings, progress, and generated sandbox projects are reviewable from one place.
- Lab validation review should also read `result-index.json` and summarize pass count, full-project count, top capability, watch-list failures, model, and max-token evidence in the UI.
- The shell footer owns one concise setup readiness line plus one action button derived from model, harness, and router state. The action opens Models/Agents when setup is missing, selects the best route when routing is stale, and returns to Lab when the hosted route is ready.
- `NEXUSCENTRAL_START_SURFACE=Models` or `Agents` may be used for screenshot QA of setup surfaces; leave it unset for normal Lab-first launch.
- Keep hero UI controls limited to backed primary workflow actions.
- Keep advanced or future actions hidden, disabled with clear inactive state, or moved out of first-screen flow.

## Update Pipeline Direction

- Use GitHub releases and semver tags for public app versions.
- Use a small update index such as `updates/stable.json` and `updates/beta.json`.
- Use package manifests for internal modules with `id`, `version`, `compatibility`, `source`, `sha256`, and `capabilities`.
- Use a local installed lockfile so the app can tell what versions are installed.
- Verify hash/signature before applying updates and keep rollback possible.

## Build And Deployment Pipeline

- `updates/release-targets.json` owns the target matrix.
- `.github/workflows/nexuscentral-build.yml` builds every discovered `NexusCentral-V*` lane that contains `ARCHITECTURE.md`, `Desktop/*.csproj`, and `Tests/*.csproj`.
- `.github/workflows/nexuscentral-release.yml` publishes the same target matrix to GitHub Releases on `v*` tags or manual release runs.
- `.github/workflows/nexuscentral-pages.yml` publishes the latest successful build outputs for each pushed branch to the `gh-pages` branch.
- The public downloads page is expected at `https://luminarylabs-dev.github.io/NexusCentral/` after GitHub Pages is configured to deploy from `gh-pages` `/`.
- Generated local publish output belongs under `artifacts/` and must stay out of git.
- Current measured self-contained zipped build sizes for `NexusCentral-V1` are about:
  - `osx-arm64`: 45 MB zipped, 113 MB expanded.
  - `osx-x64`: 50 MB zipped, 111 MB expanded.
  - `linux-x64`: 49 MB zipped, 108 MB expanded.
  - `win-x64`: 76 MB zipped, 211 MB expanded.
