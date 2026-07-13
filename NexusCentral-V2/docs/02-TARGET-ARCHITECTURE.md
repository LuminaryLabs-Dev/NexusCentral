# Target Architecture

## Repository Shape

```text
NexusCentral/
|-- apps/
|   |-- web/                    shared Next.js/static-exportable UI
|   `-- electron/               thin desktop shell
|-- services/
|   `-- nexus-node/             persistent local service
|-- kits/
|   `-- <domain-kit>/           NexusEngine ESM DSKs
|-- adapters/
|   |-- electron-ipc/
|   |-- filesystem/
|   |-- secure-storage/
|   |-- mesh-network/
|   |-- lan-host/
|   |-- providers/
|   |-- model-runtimes/
|   |-- process/
|   `-- native/
|-- packages/
|   |-- central-client/
|   |-- contracts/
|   |-- composition/
|   `-- test-support/
|-- docs/
`-- package.json               npm workspace root
```

## Process Shape

```text
Web browser
|-- shared UI
`-- LAN CentralClient adapter

Electron
|-- shared UI
`-- Electron CentralClient adapter

Nexus Node
|-- NexusEngine domain graph
|-- durable jobs and state
|-- LAN web/API host
|-- mesh participation
|-- provider/model/harness adapters
`-- optional Rust workers
```

One repository does not mean one process. DSKs are shared ESM contracts and
state owners. Processes install the graph and provide adapters appropriate to
their environment.

## Composition Rule

```text
create NexusEngine runtime
-> install n:central foundation
-> install foundational state domains
-> install policy and membership domains
-> install routing and execution domains
-> install feature orchestration domains
-> bind host adapters
-> expose CentralClient API
```

The composition package owns install order, dependency validation, duplicate
provider detection, graph diagnostics, and public API projection. It does not
own product state.

## Runtime Authority

The headless Nexus Node is authoritative for durable service state. Electron
and browsers are replaceable clients. Browser-local ONNX workers may contribute
temporary capabilities while connected but never become the durable authority.
