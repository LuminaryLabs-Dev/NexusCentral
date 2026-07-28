# Implementation Sequence

Each phase ends at its proof gate. Do not begin the next phase while the current
phase has unresolved contract or behavior failures.

## Phase 0: Workspace Foundation

- Add root npm workspaces without moving production code.
- Pin NexusEngine.
- Add ESM package conventions, graph checks, and shared test support.
- Create the V2 composition package and in-memory adapter fixtures.

Gate: existing Electron still starts and V2 workspace checks run independently.

## Phase 1: Central And Policy

- Implement `n:central`, node, audit, and policy DSKs.
- Prove provides/requires, `engine.n.*`, snapshot, reset, and deny-by-default.
- Wrap current mesh policy calls through compatibility adapters.

Gate: current mesh ping works and unapproved actions remain denied by the DSK.

## Phase 2: Mesh Extraction

- Implement membership and routing DSKs.
- Extract UDP, HTTP, crypto, secure storage, and clock adapters.
- Preserve same-code auto discovery and encrypted direct requests.
- Add lease expiry and isolated multi-node proof.

Gate: two-node direct proof, partition/expiry proof, rejection proof, and restored snapshot proof pass.

## Phase 3: Providers And Models

- Implement providers, model catalog, and model runtime DSKs.
- Wrap OpenRouter, OpenAI, Gemini, NIM, LM Studio, Hugging Face, and custom endpoints.
- Preserve encrypted credentials and 16K model load configuration.

Gate: empty, healthy, unhealthy, load, unload, and publish-provider states match current behavior.

## Phase 4: Workspace, Jobs, Harnesses, Artifacts, Proof

- Implement workspace, artifacts, harnesses, jobs, proof, and Lab DSKs.
- Move Review -> Act -> Audit lifecycle out of Electron main.
- Add cancellation, idempotency, budgets, and restart-safe job recovery.

Gate: a real workspace run survives UI closure and produces reviewable proof without commit, push, or deploy.

## Phase 5: Shared CentralClient And Web UI

- Implement CentralClient contract and direct test transport.
- Move current UI into `apps/web`.
- Bind Electron through the IPC transport.
- Preserve visual and workflow parity before further redesign.

Gate: Electron uses the shared UI and all user workflows pass human-view validation.

## Phase 6: Headless Nexus Node And LAN Host

- Move durable composition into `services/nexus-node`.
- Serve the same web build over authenticated LAN access.
- Add browser session scopes and event streaming.
- Make Electron a client of the node.

Gate: closing Electron does not stop mesh participation, accepted jobs, or LAN access.

## Phase 7: ONNX And Workspace Transfer

- Implement inference and workspace transfer DSKs.
- Add Node ONNX worker first, browser ONNX second.
- Add content-addressed workspace transfer and conflict handling.
- Enable explicitly scoped remote inference only after policy proof.

Gate: authenticated peer inference respects model hash, budget, cancellation, artifacts, proof, and audit.

## Phase 8: Packaging And Promotion

- Package Nexus Node and Electron for macOS, Linux, and Windows.
- Add install, upgrade, migration, rollback, and signed artifact proof.
- Run 50-node simulation before claiming enterprise mesh readiness.

Gate: every requirement in `09-VALIDATION-PROMOTION-GATES.md` has retained evidence.
