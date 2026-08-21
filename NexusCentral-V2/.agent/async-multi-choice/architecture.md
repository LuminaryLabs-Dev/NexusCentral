# Verified NexusCentral-V2 Shape

Use these facts as evidence. They describe the current worktree, not a proposed
rewrite.

## Authority And Processes

- `docs/00-START-HERE.md` makes the V2 docs the implementation contract.
- `packages/composition/index.mjs` installs 19 NexusEngine domain-service kits.
  Kits own serializable decisions and state; adapters perform filesystem,
  process, network, credential, and model-runtime I/O.
- `services/nexus-node/src/nexus-node.mjs` is the durable host authority.
  Electron and browsers are clients; closing either must not stop accepted work.
- `adapters/runtime-adapters/src/sqlite-store.mjs` persists domain snapshots,
  events, and host values in SQLite WAL.

## Runs And Harnesses

- `services/nexus-node/src/run-coordinator.mjs` routes `runs.submit`,
  `inference.submit`, and `lab.submit` through one coordinator and returns
  canonical run status, route, result, experiment, and artifact records.
- `packages/contracts/index.mjs` defines queued, running, approval_required,
  completed, failed, and canceled run states plus the per-run context contract.
- `adapters/runtime-adapters/src/harness-executors.mjs` supports
  `direct-inference` and `codex-cli`. Profiles constrain stages, tools, and
  read-only or workspace-write sandbox modes.
- `docs/13-HARNESS-EXPERIMENTS.md` requires Codex work to occur in a
  content-hashed SimSpace copy. Validation, approval, and a separate promotion
  command protect the source workspace.
- A harness or LLM may assemble evidence, validate, and recommend. NexusCentral
  policy and immutable run state must enforce approval gates; model output is
  never its own authority.

## Providers, Models, And Routing

- `packages/contracts/index.mjs` provides presets across OpenAI-compatible,
  Anthropic, Google, Azure, and AWS Bedrock protocol families.
- `kits/providers-kit/index.mjs` owns secret-free profiles and health.
  Credentials remain in host secure storage.
- `kits/routing-kit/index.mjs` owns candidate constraints, rationale, fallback,
  and route lifecycle; network calls remain adapters.
- `kits/jobs-kit/index.mjs` owns durable queue, lease, retry, idempotency,
  cancellation, timeout, progress, and terminal state.
- `docs/14-PROVIDERS-AND-PEERJS-ROOMS.md` keeps libp2p LAN and PeerJS remote-room
  transport separate from provider and routing authority.
- `docs/12-IMPLEMENTATION-STATUS.md` records live LM Studio proof at 16K and
  contract-level cloud provider proof; real cloud credentials remain unproven.

## Client And UI

- `packages/central-client/index.mjs` is the single outcome-level command
  boundary for direct tests, Electron IPC, and HTTP/WebSocket clients.
- `apps/web/app/page.jsx` currently renders five tabs: Lab, Router, Models,
  Review, and Settings.
- The new product decision replaces that shell with six single-purpose tabs:
  Work, Router, Models, Workflows, Runs, and Settings.
- Work will expose Chat, Coding, and Research conversation modes. Workflows will
  own schedules, batches, and multi-agent stage definitions. Runs will own
  immutable execution evidence, artifacts, validation, and approvals.
- The primary interaction is one technical operator per user/session, while one
  NexusCentral deployment must support multiple concurrent users collaborating.

## Current Proof And Gaps

- `docs/12-IMPLEMENTATION-STATUS.md` reports local checks for 19 DSKs,
  integration, scale simulation, live LM Studio, desktop/mobile Playwright,
  Electron service survival, migration, and live Codex harnesses.
- Current release gaps include browser ONNX text generation, multi-hop relay,
  workspace byte transfer, host native workers, real cloud credentials,
  interrupted harness replay, expanded approval command surfaces, non-macOS
  launch proof, trusted LAN certificates, physical mesh scale, and dependency
  audit findings.
- Product code must remain unchanged until the intake produces a complete plan
  and the user explicitly approves implementation.

## Question Boundary

- Do not ask whether an implemented/documented boundary exists.
- Ask whether and how that boundary should change to satisfy the six-tab,
  multi-user, workflow, approval, memory, and production-readiness goals.
- State the current file or subsystem in plain English before asking.
- When a term is unavoidable, define it using a concrete NexusCentral example.
