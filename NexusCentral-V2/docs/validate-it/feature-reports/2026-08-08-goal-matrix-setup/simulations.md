# Matrix Simulations

Simulations are forecasts. They harden gates and repair routes but cannot promote implementation.

## Domain Review

| Domain | Injected failure | Reconciled result | Stability |
|---|---|---|---|
| Goal control | Stale transition writer | Reject the write and preserve the revision. | Stable |
| Source authority | Overlapping dirty and duplicate history | Compare trees, preserve work, and require approval. | Stable |
| Shared application | Browser imports host-only code | Fail the browser boundary without creating a fork. | Stable |
| Runtime profiles | Hidden Codex control is called directly | Server authorization decides execution. | Stable |
| Local node state | Restart after work acceptance | Recover locally owned accepted state. | Conditional |
| Security | Anonymous privileged call | Reject before provider, tool, or harness execution. | Stable |
| Models and runtimes | WebGPU model generation fails | Retry with WASM, then return a contained error. | Conditional |
| Router server | First route fails before output | Record failure and try the next eligible route. | Stable |
| Runs and proof | Crash during stream | Restore context and avoid duplicate side effects. | Conditional |
| Harness and sandbox | Subprocess exits after partial output | Retain evidence, cancel tools, and fail the stage. | Stable |
| Work | Coding harness returns a valid diff | Keep it in SimSpace until human promotion. | Stable |
| Workflows | Middle fail-forward stage fails | Follow declared fallback and retain final human gate. | Conditional |
| Internal API | Custom client discovers endpoint | Permit safe discovery, enforce scoped authorization. | Stable |
| UI | Public profile has no Nexus Node | Show browser-safe controls without crashing. | Stable |
| Operations | Pages runs under repository subpath | Load static assets without assuming a local API. | Conditional |
| Release proof | Required proof is missing | Deny release and retain the exact missing gate. | Stable |

## Harness And Model Review

| Harness | Model or route | Decision | Stability |
|---|---|---|---|
| Scripted replay | Deterministic fixture | Test-only admission | Stable |
| Direct inference | Browser LFM2-350M | Admit after browser proof | Conditional |
| Codex CLI | Strong coding model | Admit after live proof | Conditional |
| Codex CLI | Browser LFM2-350M for coding | Reject capability mismatch | Stable |
| NexusAgent Conversation | Local OpenAI-compatible route | Admit after adapter proof | Conditional |
| NexusAgent LiquidPro | Browser LFM2 bounded chain | Experimental after proof | Conditional |
| NexusAgent A1 | Planning route | Hold to plan-only | Stable |
| Aider | Strong coding model | Experimental after SimSpace proof | Conditional |
| mini-swe-agent | Strong coding model | Experimental after tool proof | Conditional |
| Claude Code | Provider-native route | Hold | Speculative |
| Gemini CLI | Provider-native route | Hold | Speculative |
| OpenHands SDK | Full agent route | Hold | Speculative |

## Reconciliation

- Stable: `14`.
- Conditional: `11`.
- Speculative: `3`.
- Drifting: `0`.
- Review pass remained stable after normalizing missing domains and defining the ancestor `active` state.
- Every conditional or speculative scenario has a contained repair route and remains unpromoted.
