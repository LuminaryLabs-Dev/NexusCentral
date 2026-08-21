# Legacy Lane Audit

Purpose: identify reusable behavior from older NexusCentral lanes without merging old application architectures into V2.

## Decision

`NexusCentral-V2` is canonical. Older lanes are evidence and rollback references, not parallel products.

| Lane | Useful evidence | V2 disposition |
| --- | --- | --- |
| `NexusCentral-V1` | Proven cross-platform release discipline and prior Lab/Router/Models/Validation work | Keep as rollback/reference. Reuse release gates, not Avalonia application code. |
| `NexusCentral-Electron` | Earlier Electron adapter boundary, secure loopback shell concept, LAN mesh work, Rust sidecar proof | V2 already owns the Electron shell and mesh boundary. Retain Rust sidecar as a future native-worker reference only. |
| `NexusCentral-V0` | Historical wrapper/runtime experiments, including explicit memory-context prompt composition | Do not port wrappers. The memory behavior is useful only as product evidence for a future V2 Context/Memory domain. |

## Findings

### V1

V1 contains backed Lab, Harnesses, Models, Router, Validation, and Workspaces modules. Its `Agents`, `Automations`, `Devices`, `Settings`, and `WebLab` feature directories are empty scaffolds in the current tree, so there is no missing implementation to port from those folders.

V1's strongest reusable asset is operational: the repository already proved release artifacts across Windows x64, Linux x64, macOS x64, and macOS arm64. V2 adopts that target discipline through native Node/Electron CI rather than copying the .NET application.

### Earlier Electron lane

The earlier Electron lane contains a Rust sidecar proof. V2 currently does not have host Rust execution workers, so the proof remains a legitimate future reference. It is not required for the first universal Electron deployment and should not be copied into V2 until a concrete native-worker capability requires it.

The older Electron shell itself is superseded by `NexusCentral-V2/apps/electron`; maintaining two desktop hosts would create duplicate IPC, credential, packaging, and lifecycle ownership.

### V0

V0's memory wrapper accepts explicit `memory_context`, prepends it to a prompt, calls the thinking endpoint, and records the supplied memory context as an artifact. This is useful evidence that memory-assisted execution was part of earlier NexusCentral experiments, but it is not a durable memory subsystem.

A future V2 Context/Memory domain should be designed around ingestion, retrieval, provenance, context budgeting, and revocation rather than porting this Python wrapper.

## Keep / Discard

Keep:

- V1 release-validation lessons;
- earlier Electron Rust sidecar as reference;
- historical product intent around memory/context;
- old lanes as rollback and behavior evidence until V2 deployment is stable.

Do not port:

- Avalonia UI/view-model architecture;
- the earlier Electron application shell;
- V0 Python wrapper/runtime structure;
- empty feature scaffolds;
- branch-specific publication machinery already superseded by `main`.
