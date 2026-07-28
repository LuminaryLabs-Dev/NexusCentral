# Product Intent

## Outcome

NexusCentral is a local-first agentic workbench where one person can work from
one UI while local computers safely cooperate through a sovereign LAN mesh.

The user sees outcomes:

```text
Lab -> Router -> Models -> Review -> Settings
```

The user does not need to understand internal kits, transports, IPC channels,
processes, provider adapters, or cryptographic envelopes.

## Primary Workflows

### Lab

Open a workspace, describe a task, run a selected harness, watch progress, and
receive reviewable output.

### Router

Choose whether work runs through a hosted provider, local model, browser ONNX
worker, or trusted mesh peer.

### Models

Find, acquire, load, unload, inspect, and publish models as available execution
capabilities.

### Review

Inspect job history, artifacts, proof, policy decisions, errors, and audit
receipts without crowding the Lab.

### Settings

Manage local paths, LAN membership, peer permissions, provider credentials,
runtime limits, diagnostics, and appearance.

## Product Constraints

- Closing the UI must not terminate accepted background work.
- Joining a LAN code must not grant arbitrary execution.
- Every machine retains authority over its files, tools, models, and resources.
- The same web UI must work through Electron and LAN hosting.
- Browser and desktop surfaces must consume the same application contract.
- Secrets and private keys never enter renderer or DSK snapshots.
- A static build check never substitutes for live model, harness, mesh, or packaging proof.

## Non-Goals

- A second independently designed Electron interface.
- A central controller that owns every peer.
- Remote shell access as a default mesh capability.
- Copying NexusEngine source into NexusCentral.
- Turning visual helpers or every utility function into DSKs.
