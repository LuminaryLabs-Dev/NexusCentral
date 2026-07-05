# NexusCentral Master Update Plan

## Purpose

This plan converts the current state reports into a step-by-step rebuild path for the latest active NexusCentral version folder.

The plan is based on observed state, not assumptions:

- `state/evidence/build-health.md` reports green build and test results.
- `state/reports/lab-workspaces-audit.md` reports Lab + Workspaces as the only backed product slice.
- `state/reports/v1-gap-scan.md` reports Router, Models, Agents, Automations, Settings, provider/RPC, and shell status as mostly placeholder or static.
- `state/automation/AUTOMATION_MANIFEST.md` reports stale path wording and missing durable docs.

## Step Index

1. `plan_step01_state_and_repo_normalization.md`
   - Normalize repo/state docs, active-version wording, generated-output ignores, and durable project control docs.
2. `plan_step02_navigation_truth.md`
   - Fix primary navigation truth so Lab is visible, selected, and reachable.
3. `plan_step03_lab_workspace_completion.md`
   - Finish the Lab + Workspaces slice with workspace-owned uploads, tree interaction, and file preview.
4. `plan_step04_router_truth.md`
   - Replace fake Router readiness with real inactive/available route state.
5. `plan_step05_shell_status_truth.md`
   - Bind shell status, console, queue, route, model, and session panels to real state or explicit inactive state.
6. `plan_step06_models_foundation.md`
   - Add the Models foundation for endpoint records, provider config, and real health checks.
7. `plan_step07_agents_foundation.md`
   - Add the Agents foundation for harness manifests, tool permissions, and registry-backed agent rows.
8. `plan_step08_automations_foundation.md`
   - Add the Automations foundation for draft workflow editing and validation without fake execution.
9. `plan_step09_settings_foundation.md`
   - Add Settings persistence for paths, auth profiles, routing defaults, and theme/density.
10. `plan_step10_release_hardening.md`
   - Harden release readiness with repeatable build, test, run, screenshot, source scan, and final docs proof.

## Operating Rules

- Execute steps in order unless a blocker forces a narrower prerequisite.
- Each step must end with build/test validation when source changes are made.
- Do not implement later modules early.
- Do not copy V2 code.
- Do not restore Dashboard, Web Lab as a primary tab, Integrations as a primary tab, ticketing, or the old standalone Workflow page.
- Keep evidence under the resolved latest active version folder.
- Use state reports to update this plan when observations change.

## Current Priority

Start with `plan_step01_state_and_repo_normalization.md`.

Reason: the repo currently has stale `V1` wording in state docs, untracked version folders, generated `bin/obj` output under the active folder, and missing durable `memory.md`, `goal.md`, `Questions.md`, and `DESIGN_DNA.md` for the active app lane.
