# Adapter And Host Boundaries

## Rule

DSKs own decisions and serializable state. Adapters perform unsafe or
environment-specific work. Hosts create processes, install kits, bind adapters,
and expose client surfaces.

## Adapter Inventory

| Adapter | Owns | Consumes |
|---|---|---|
| `electron-ipc` | Preload channels and typed IPC translation | CentralClient contract |
| `secure-storage` | Electron safeStorage or OS keychain | secret references from node/providers/policy |
| `filesystem` | dialogs, reads, writes, manifests, watches | workspace intents |
| `artifact-storage` | content bytes, retention execution | artifact descriptors |
| `json-store` | atomic local persistence during migration | DSK snapshots |
| `sqlite-store` | durable node state and indexes | DSK snapshots/events |
| `provider-http` | OpenAI-compatible/provider requests | provider and inference descriptors |
| `huggingface` | search/download transport | model catalog requests |
| `lm-studio` | list/load/unload/inference API | model runtime requests |
| `onnx-node` | native ONNX execution in workers | inference descriptors |
| `onnx-browser` | WASM/WebGPU execution while connected | inference descriptors |
| `mesh-network` | multicast, sockets, encryption, signed envelopes | membership/routing requests |
| `lan-host` | HTTPS, WebSocket, static UI files | LAN access decisions |
| `process-codex` | Codex CLI spawning and output capture | harness/job requests |
| `rust-worker` | optional native performance process | typed job descriptors |
| `clock` | monotonic time, deadlines, lease ticks | jobs/membership systems |

## Forbidden DSK Imports

```text
electron
node:fs
node:dgram
node:http or node:https
node:child_process
browser DOM or Canvas
requestAnimationFrame
onnxruntime-node
native binaries
process-global environment mutation
direct credential stores
```

Cryptographic policy and envelope meaning may be domain state. Actual key
generation, signing, encryption, random bytes, and key storage remain adapters.

## Host Responsibilities

### Nexus Node

- Create the NexusEngine runtime.
- Install DSKs in dependency order.
- Construct and bind adapters.
- Restore durable snapshots.
- Drive lease/job clocks.
- Expose CentralClient commands and subscriptions.
- Continue running independently of Electron and browsers.

### Electron

- Create the desktop window.
- Load the shared web UI.
- Connect CentralClient to the local Nexus Node.
- Provide desktop-only dialogs and OS integration through scoped adapters.
- Never become the durable job or mesh authority.

### LAN Host

- Serve the same exported web UI.
- Create authenticated browser sessions.
- Translate HTTPS/WebSocket traffic into CentralClient calls.
- Enforce LAN access decisions before forwarding commands.

## Adapter Result Contract

Adapters return typed results, never mutate DSK state directly:

```text
domain emits intent
-> host invokes adapter
-> adapter returns normalized result/event
-> domain validates and applies transition
-> snapshot/event becomes inspectable
```
