# Ensure Audit

## Boundary

- Canonical workspace: `/Users/crimsonwheeler/Documents/GitHub/NexusCentral/NexusCentral-V2`.
- Existing unrelated dirty UI and agent files were preserved.
- No runtime, UI, API, branch, remote, deployment, credential, payment, provider, or model state changed.
- The prior matrix, queue, ledger, goal, and memory files were captured in `/tmp/nexuscentral-v2-goal-control-pre-revision-2.tar` before reconciliation.

## Inputs Audited

- Repository instructions, `goal.md`, `memory.md`, architecture docs, existing matrix, queue, and ledger.
- Goal-matrix, DAG, route, harness, evidence, ledger, validation, and simulation skill contracts.
- Existing NexusCentral and NexusAgent harness surfaces.
- Official non-executed interface references for Codex CLI, Claude Code, Gemini CLI, Aider, mini-swe-agent, and OpenHands.

## Defects Found And Repaired

| Defect | Effect | Repair |
|---|---|---|
| 41 inherited subgoals lacked `domain` | Violated the required node contract. | Inherited the owning parent domain. |
| `active` was used but absent from the status vocabulary | Made valid ancestor states fail schema validation. | Defined `active` as an ancestor-path state, not an executing leaf. |
| Public visibility could be confused with API permission | Hidden controls could be treated as security. | Separated visibility, enablement, authorization, and support. |
| Static web could assume a same-origin Nexus Node | GitHub Pages would fail or expose a false contract. | Required browser-local behavior and optional authorized endpoints. |
| Small browser model could be routed to coding work | Capability mismatch would create unreliable output. | Restricted LFM2-350M to bounded browser tasks. |
| Failover did not distinguish emitted output or side effects | Retries could duplicate work. | Split pre-output and post-output failure policy. |
| External harnesses could own tools or workspace state | Canonical authority could fork. | Held admission until routing, tools, sandbox, and event parity are proven. |

## Audit Decision

Pass for matrix setup. Product implementation and live behavior remain unproven and are not promoted by this run.
