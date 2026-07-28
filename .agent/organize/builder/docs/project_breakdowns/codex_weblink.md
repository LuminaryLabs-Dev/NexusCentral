# Project Breakdown: CodexWebLink

- `project_slug`: `codex_weblink`
- `confidence`: high
- `apparent_purpose`: CLI utility that opens a browser session, exposes a phone-friendly monitoring link, and attaches a Codex-backed PTY and TUI.
- `key_components`: `src/cli.ts`, `SessionSupervisor`, `HttpServer`, TUI runtime, Playwright and PTY dependencies.
- `evidence_signals`: CLI advertises live browser monitoring from a phone and local attach flows; dependencies include `playwright`, `node-pty`, `express`, and `ws`.
- `likely_business_function`: Remote supervision and lightweight operator control for Codex-driven browser sessions.
- `patterns_present`: Session registry, auth snapshot and PIN flow, phone link generation, attachable terminal supervision.
- `automation_relevance`: High for session bootstrap briefs, operator handoff docs, telemetry review, and mobile-operator workflow proposals.
