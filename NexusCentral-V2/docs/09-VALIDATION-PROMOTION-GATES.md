# Validation And Promotion Gates

## DSK Gate

Every DSK requires:

- metadata validation;
- provides/requires resolution;
- isolated API proof;
- deterministic snapshot proof;
- reset proof;
- invalid-input rejection;
- no forbidden host imports;
- integrated composition proof;
- graph visibility through `engine.n` inspection.

Promotion states:

```text
planned -> implemented -> isolated-proven -> composition-proven -> compatibility-proven -> promoted
```

## Adapter Gate

Every adapter requires:

- typed input/output contract;
- timeout and cancellation behavior;
- normalized errors;
- resource cleanup;
- secret redaction;
- failure injection proof;
- no direct cross-domain state mutation.

## Feature Gates

### Lab

- workspace opens and refreshes;
- Enter submits and Shift+Enter creates a newline;
- run continues without visible UI;
- cancellation and restart recovery work;
- Review -> Act -> Audit permissions remain correct;
- no automatic commit, push, deploy, or external write.

### Router And Models

- inactive state is truthful;
- provider health is live, not static;
- credentials remain encrypted;
- model search/load/unload/publish works;
- 16K load configuration is preserved;
- provider, local, browser, and mesh routes expose rationale.

### Mesh

- same-code nodes discover automatically;
- different-code nodes cannot join or inspect encrypted details;
- identity and request authentication work;
- leases expire dead peers;
- local deny-by-default survives restart;
- loops, replay, stale timestamps, and oversized payloads are rejected;
- multi-hop never bypasses executing-node policy.

### LAN Web

- shared UI loads from another LAN computer;
- authenticated session and revocation work;
- no secret/private-key exposure;
- WebSocket reconnect restores state safely;
- browser closure expires ephemeral capabilities.

### ONNX

- model hash and format validation work;
- memory, concurrency, timeout, and cancellation limits work;
- malformed and unauthorized model requests fail closed;
- Node and browser results use one normalized contract.

## Human-View Gate

Capture desktop and LAN browser proof for normal, empty, loading, failure, and
partial states. The first screen must remain workspace + conversation + prompt.
Advanced architecture and diagnostics stay outside Lab.

## Scale Gate

Before claiming support for 50 cooperating computers, prove:

- 50 simulated nodes with churn;
- bounded discovery traffic;
- lease convergence;
- route loop prevention;
- queue backpressure;
- deterministic policy decisions;
- node draining and reconnect;
- artifact and audit consistency;
- measurable CPU, memory, network, and latency budgets.

## Release Gate

Static checks are necessary but insufficient. Release requires real startup,
real provider/model calls, real harness execution, real LAN connection, real
packaging, migration proof, rollback proof, and retained evidence paths.
