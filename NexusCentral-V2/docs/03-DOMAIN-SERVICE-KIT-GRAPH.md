# Domain Service Kit Graph

## Contract Standard

Every kit must use `defineDomainServiceKit()` from the pinned `nexusengine`
dependency and declare:

```text
stable id
domain and domainPath
parentDomainPath when nested
apiName and public visibility
version and stability
provides and requires
serializable owned state
accepted inputs and emitted outputs
deterministic snapshot behavior
explicit reset behavior
isolated and composition proof
forbidden host imports
```

## Installation Order

| Order | Stable ID | Domain path | API name | Requires |
|---|---|---|---|---|
| 00 | `n-central-kit` | `n:central` | `central` | `n:realtime` |
| 05 | `n-central-node-kit` | `n:central:node` | `centralNode` | `n:central` |
| 10 | `n-central-audit-kit` | `n:central:audit` | `centralAudit` | node |
| 15 | `n-central-policy-kit` | `n:central:policy` | `centralPolicy` | node, audit |
| 20 | `n-central-workspace-kit` | `n:central:workspace` | `centralWorkspace` | central |
| 25 | `n-central-artifacts-kit` | `n:central:artifacts` | `centralArtifacts` | workspace, audit |
| 30 | `n-central-providers-kit` | `n:central:providers` | `centralProviders` | central, audit |
| 35 | `n-central-model-catalog-kit` | `n:central:model-catalog` | `centralModelCatalog` | central, audit |
| 40 | `n-central-model-runtime-kit` | `n:central:model-runtime` | `centralModelRuntime` | model catalog, node, audit |
| 45 | `n-central-mesh-membership-kit` | `n:central:mesh-membership` | `centralMeshMembership` | node, audit |
| 50 | `n-central-routing-kit` | `n:central:routing` | `centralRouting` | providers, model runtime, membership, policy |
| 55 | `n-central-harnesses-kit` | `n:central:harnesses` | `centralHarnesses` | central |
| 60 | `n-central-jobs-kit` | `n:central:jobs` | `centralJobs` | routing, policy, audit |
| 65 | `n-central-inference-kit` | `n:central:inference` | `centralInference` | jobs, model runtime |
| 70 | `n-central-workspace-transfer-kit` | `n:central:workspace-transfer` | `centralWorkspaceTransfer` | workspace, artifacts, jobs, policy |
| 75 | `n-central-proof-kit` | `n:central:proof` | `centralProof` | jobs, artifacts, audit |
| 80 | `n-central-lab-kit` | `n:central:lab` | `centralLab` | workspace, harnesses, jobs, proof |
| 82 | `n-central-experiments-kit` | `n:central:experiments` | `centralExperiments` | harnesses, workspace, jobs, proof, audit |
| 85 | `n-central-lan-access-kit` | `n:central:lan-access` | `centralLanAccess` | node, policy, audit |

## Ownership Contracts

### Central

Owns product graph version, runtime mode, feature availability, and aggregate
readiness. It does not own feature state. Snapshot is a graph summary. Reset
clears transient readiness only.

### Node

Owns public node profile, display name, public identity reference, platform
descriptor, and advertised local capabilities. Private keys and hardware probes
remain adapters. Soft reset preserves identity; factory reset requires approval.

### Audit

Owns append-only security and operation receipts. Inputs are normalized domain
events. Outputs are cursor-based audit views. Normal reset rotates the active
segment; test reset may clear an isolated in-memory ledger.

### Policy

Owns peer grants, action permissions, approvals, quotas, budgets, and local
admission decisions. Reset is deny-by-default. Snapshots exclude secrets.

### Workspace

Owns selected workspace descriptor, revision, manifest, logical paths, and
access intent. Filesystem access remains an adapter. Reset deselects the
workspace and never deletes user files.

### Artifacts

Owns content-addressed artifact metadata, ownership, provenance, and retention
intent. Storage bytes and deletion remain adapters. Reset clears indexes only.

### Providers

Owns provider profiles without secrets, health observations, supported models,
and active selection. HTTP and secret storage remain adapters. Reset clears
health/selection while preserving configured profiles unless explicitly removed.

### Model Catalog

Owns model manifests, source references, hashes, formats, compatibility, and
local/remote availability. Search and download transport remain adapters.

### Model Runtime

Owns load requests, loaded-session descriptors, resource claims, readiness,
and unload lifecycle. LM Studio, ONNX, GPU, and native execution remain adapters.

### Mesh Membership

Owns peer records, leases, topology, advertised capabilities, trust state, and
peer expiry. UDP, HTTP, cryptography, clocks, and persistence remain adapters.
Reset removes peers while preserving local identity.

### Routing

Owns local/provider/peer candidates, route constraints, selection rationale,
fallback order, and route lifecycle. It never performs network requests.

### Harnesses

Owns harness profiles, declared tools, stages, safe selectable options, and selection.
CLI/process execution remains an adapter. Policy remains the authority for tool
permission.

### Experiments

Owns disposable run state, attempts, stage receipts, validation summaries, diff
identity, approval, and promotion status. SimSpace paths, process execution,
recording bytes, and patch application remain host adapters.

### Jobs

Owns queue state, idempotency, budgets, leases, retry, cancellation, timeout,
progress, and terminal outcomes. Timers and workers are host adapters. Snapshot
must restore safely without replaying completed side effects.

### Inference

Owns inference request descriptors, batching intent, token/context limits,
stream state, and normalized results. ONNX and provider calls remain adapters.

### Workspace Transfer

Owns content snapshot identity, transfer plans, conflict state, chunk receipts,
and completion. Network and storage transfer remain adapters.

### Proof

Owns evidence references, assertions, stage receipts, validation summaries, and
promotion decisions. It references artifacts but does not store their bytes.

### Lab

Owns the Review -> Act -> Audit state machine, active run, stage transitions,
resume/cancel rules, and final outcome. It composes other domain APIs and owns no
filesystem, process, network, or UI behavior.

### LAN Access

Owns browser sessions, pairing grants, scopes, expiry, revocation, and public
capability projection. HTTPS/WebSocket serving remains an adapter.

## Feature Composition

```text
Lab      = workspace + harnesses + jobs + routing + proof
Router   = providers + routing + mesh membership + policy
Models   = model catalog + model runtime + inference
Review   = jobs + artifacts + proof + audit
Settings = node + policy + LAN access + adapter configuration
```

There is no Settings DSK, Review UI DSK, Electron DSK, or LAN website DSK.
Those are presentation or host compositions over domain APIs.
