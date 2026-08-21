# Ensure Audit

## Environment

- NexusCentral worktree is dirty with existing user/agent changes; validation did not rewrite or discard them.
- NexusAgent is a dirty multi-project source workspace; `release:check` remains usable but archive/copy release is blocked until clean.
- NexusCentral launched from a disposable data root at `/tmp/nexuscentral-validate.UtflNk`.
- Loopback LM Studio `127.0.0.1:1234` refused connections.
- Known LAN endpoints `10.0.0.38:1234` and `10.0.0.137:1234` timed out.
- macOS LaunchAgent support exists but the service is not installed.

## Contract Findings

- The matrix JSON is structurally valid: 93 nodes, 13 domains, 38 capabilities, 41 slices, no duplicate IDs, and no missing references.
- `matrix:validate`, `matrix:queue`, and `setup:lfm` are absent from `package.json`.
- NexusAgent `Conversation-Harness/harness.manifest.json` is command-oriented and has no NexusCentral `stages` field.
- NexusCentral `harnesses.import` accepts only its internal profile shape and rejects the NexusAgent manifest with `Harness profile requires id and stages.`
- The UI exposes `Lab | Router | Models | Review | Settings`; the target Work, Workflows, and Runs ownership split is not implemented.
