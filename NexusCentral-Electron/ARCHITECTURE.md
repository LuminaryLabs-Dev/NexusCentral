# NexusCentral Electron Architecture

## Shape

```text
NexusCentral-Electron
|-- src
|   |-- index.html          browser-safe workflow UI
|   |-- styles.css          V1 dark green UI shell
|   |-- renderer.mjs        UI state and IPC calls
|   |-- domain-kits         browser-safe MJS kits
|   |-- preload.js          safe desktop bridge
|   |-- main.js             Electron process and local adapters
|   |-- workflow-contract.js
|-- sidecars
|   |-- rust-proof          native sidecar proof
|-- scripts
|   |-- check.js            static contract validation
|   |-- build.js            package staging
```

## Workflow Contract

The shell has five primary surfaces:

```text
Lab
Router
Models
Review
Settings
```

Router owns provider configuration and selection. Models owns local model search, download, loading, and publication as an LM Studio provider. Validation runs and generated proof artifacts stay in Review.

## Domain Kits

The renderer follows the NexusEngine kit rule in a small browser-safe form:

```text
identity-kit
surface-copy-kit
particle-field-kit
```

These are plain MJS modules so the same shape can lift into a future Next.js shell without pulling Electron into the web app.

## Adapter Boundary

```text
Shared UI
|-- works in browser with mock/unavailable adapters

Electron main process
|-- owns filesystem/process/native calls
|-- stores provider credentials securely
|-- manages provider and LM Studio adapters
|-- runs the sovereign LAN mesh router
|-- runs Conversation Harness
|-- runs Rust sidecars
```

The renderer must not spawn processes, read arbitrary files, or hold secrets.

## LAN Mesh Boundary

`MeshRoutingService` is a peer service, not a central controller. Every computer
keeps its own identity, policy, secrets, actions, and audit boundary.

```text
shared LAN code
-> code-derived network ID and encryption key
-> signed multicast discovery
-> direct authenticated peer connection
-> receiving computer applies its local action allow-list
-> encrypted signed result
```

The LAN code is encrypted at rest through Electron secure storage and is never
broadcast. Nodes use persistent Ed25519 identities, AES-256-GCM discovery details
and request payloads, HMAC authentication, expiring peer leases, and direct peer requests. Automatic
membership does not grant arbitrary execution: only `mesh.ping` and
`mesh.capabilities` are remotely callable by default.
