# Interface Module - BREAKDOWN

## Current State

- `Interface/docs/CLI_ARCHITECTURE.md` exists and describes DDD command architecture.
- Commands available: `models download`, `ui change-tab`, `ui screenshot`.
- Code structure: `src/nexuscentral_interface/cli.py`, domains for models/ui/workflows.

## Bug / Interaction Audit

- No docs for `nexuscentral` domain commands in existing code.
- No explicit command response format contracts (JSON output not guaranteed for all actions).
- Dependency on running Desktop integration server has no auto-retry logic.
- UI endpoints may fail if Desktop is not started or integration server is down.

## Cross-module Interaction Matrix

- Interface CLI -> Desktop IntegrationServer (3005).
- Interface -> existing model path via Desktop shared states.
- Interface -> scripts for build/release automation.

---
