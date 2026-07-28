# Implementation Status

## Implemented

- npm/ESM workspace with shared web, Electron, Nexus Node, contracts, clients,
  adapters, and 19 NexusEngine DSKs;
- SQLite WAL snapshots, jobs, events, audit cursors, host values, and recovery;
- typed CentralClient transports for direct tests, Electron IPC, and HTTP/WS;
- host-owned provider routing, 16K compatibility filtering, ordered streaming,
  cancellation, pre-token fallback, and retry after route recovery;
- presets for OpenRouter, OpenAI, NVIDIA NIM, LM Studio, Ollama, llama.cpp,
  vLLM, LocalAI, Groq, Together, Fireworks, Mistral, Cerebras, DeepInfra,
  Anthropic, Gemini, Vertex AI, Azure OpenAI, AWS Bedrock, and custom endpoints;
- normalized OpenAI-compatible, Anthropic Messages, Google Generate, Azure
  OpenAI, and AWS Bedrock protocol adapters;
- Hugging Face GGUF search and LM Studio download, load, unload, and inventory;
- safe Codex CLI profiles for Conversation, Review-only, Coding, and Audit-only;
  disposable Review -> Act -> Audit experiments, artifacts, proof, and receipts;
- libp2p TCP, mDNS, Noise, Yamux, secure host identity, LAN-code membership proof,
  direct requests, expiry, hop history checks, and deny-by-default execution;
- self-hosted PeerServer signaling, PeerJS/WebRTC room transport, derived room
  membership, direct request streaming/cancellation, STUN/TURN configuration,
  room isolation, sharing budgets, and receiver-side grants;
- host-served TLS LAN Web, short-lived pairing, scoped secure cookies, revocation,
  origin checks, WebSocket events, browser runtime leases, and disconnect expiry;
- Next.js static UI with `Lab | Router | Models | Review | Settings` and a thin
  Electron launcher/native workspace dialog;
- browser ONNX import, hashing, local smoke, WebGPU/WASM selection, Web Worker
  tensor execution, heartbeat, cancellation, and host queue registration;
- dry-run/apply migration and macOS/Linux/Windows packaging assembly scripts;
- content-hashed SimSpace copies, Codex JSONL, validation logs, NexusSimulator
  evidence, binary diffs, bounded repair, and separate approve/apply commands.

## Proven

```text
19/19 DSK lifecycle contracts
integration: provider families, opt-in sharing, libp2p, PeerJS, room isolation
50 simulated PeerJS nodes, 10 churned, 1,000 queued jobs, 74 MB measured heap
live LM Studio: 10.0.0.137, qwen3.5-4b, 16K, host-routed response
desktop + mobile Playwright: all primary surfaces, Enter submission, 0 page errors
macOS Electron: starts headless node; node survives window closure
migration: existing Codex CLI harness detected in dry-run
live Codex: Review-only and Coding profiles, gpt-5.4, npm validation
NexusSimulator: report, before/after screenshots, Playwright trace, and WebM retained
promotion: source unchanged until separate approve and apply commands
```

## Not Promoted

- Browser ONNX currently accepts known tensor manifests. Tokenizer and autoregressive
  text-generation packages are not implemented.
- Mesh requests are direct. Signed multi-hop relay, relay selection, and route
  resumption are not implemented.
- Workspace Transfer has a DSK contract but no production byte-transfer adapter.
- Host ONNX and optional Rust execution workers are not implemented.
- Cloud providers are contract-mocked; opt-in real credential checks are pending.
- PeerJS proves two local Node peers through the self-hosted broker. TURN,
  hostile-network behavior, and 50 physical peer connections remain unproven.
- Codex CLI `0.141.0` rejects the configured `gpt-5.6-sol` default as requiring a
  newer CLI. The retained live proof selects compatible `gpt-5.4`.
- Interrupted inference jobs resume after restart. Interrupted harness stages are
  retained as retryable but require explicit recovery; automatic stage replay is
  not implemented.
- Source diff application requires explicit approve and apply commands. Commit,
  push, deploy, credentials, and external submission still have no approved
  command surface.
- Linux and Windows bundles assemble on macOS but have not been launched on those
  operating systems. Signing, installers, upgrade, and rollback remain pending.
- Windows secure credential storage is not implemented. LAN TLS uses a generated
  development certificate unless an operator provides trusted certificate paths.
- The 50-node result is an in-memory domain simulation, not 50 physical machines
  or bounded live mDNS traffic proof.
- `npm audit` reports two moderate PostCSS findings through the latest stable
  Next.js 16.2.10 dependency. Do not use the suggested forced Next.js downgrade.

These items keep `goal.md` active and block an enterprise/open-source release
claim. V1 and the existing Electron paths remain rollback references.

## Validation Commands

```bash
npm run check
npm run check:integration
npm run check:scale
npm run simulate
npm run check:lm-studio
npm run check:harness-live
npm audit --audit-level=moderate
npm run migrate:electron
npm run package:desktop -- darwin
```
