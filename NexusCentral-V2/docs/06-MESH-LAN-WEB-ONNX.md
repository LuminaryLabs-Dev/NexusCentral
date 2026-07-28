# Mesh, LAN Web, And ONNX

## Sovereign Mesh

Every node is an equal peer. A peer may request work, but the receiving node
authenticates the request and applies local policy before any execution.

```text
discover peer
-> validate identity and LAN membership
-> update membership DSK
-> evaluate local policy
-> plan route
-> create job
-> invoke local adapter
-> record artifact, proof, and audit receipt
-> return encrypted result
```

## LAN Code

Matching LAN codes enable automatic discovery. The raw code never enters DSK
state, renderer state, announcements, logs, snapshots, or diagnostics. A secure
storage/crypto adapter derives required keys and network identifiers.

Joining grants membership only. It never grants workspace, harness, shell,
model, inference, or relay permission.

## Multi-Hop Routing

Multi-hop requests require:

- immutable request ID;
- origin and target identities;
- signed route history;
- hop limit and expiry;
- idempotency key;
- local admission at every executing node;
- no automatic privilege inheritance through relays;
- signed result and audit receipts.

Mesh membership owns topology. Routing owns path selection. Policy owns local
permission. Jobs own execution lifecycle. The network adapter only transports.

## LAN Web Host

The Nexus Node serves the shared web build and a versioned API:

```text
GET  /api/v1/capabilities
GET  /api/v1/state
POST /api/v1/commands
GET  /api/v1/jobs/:id
WS   /api/v1/events
```

Actual endpoint expansion must follow CentralClient contracts rather than
creating feature-specific HTTP business logic.

## ONNX Modes

### Node Runtime

`onnx-node` is the durable mesh execution adapter. It runs inference in an
isolated worker with model hash verification, memory/concurrency limits,
timeouts, cancellation, and normalized metrics.

### Browser Runtime

`onnx-browser` is an ephemeral WASM/WebGPU adapter. Its capability exists only
while the authenticated page is connected. Browser closure or suspension
expires the capability and requeues eligible jobs.

## Inference Admission

Before inference begins, local policy checks:

- requester and route;
- model hash and format;
- allowed runtime;
- token/context limit;
- CPU/GPU/memory budget;
- concurrency and queue depth;
- timeout and cancellation;
- artifact/result retention.

No model path, arbitrary URL, JavaScript module, or native library supplied by
a remote peer may be loaded directly.
