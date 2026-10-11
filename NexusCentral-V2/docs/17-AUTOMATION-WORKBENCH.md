# Automation workbench

## Current implementation

The October 11, 2026 workbench replaces the primary chat layout with Home,
Workflows, Apps, and Activity. Settings retains existing provider, model, device,
Codex experiment, and evidence controls. Earlier Lab screenshots and navigation
specifications describe the prior UI.

A block presents one action and one Configure control. Input/output dots connect
blocks. The configuration panel owns fields, compatible input selection,
connection conditions, duplication, removal, and keyboard-accessible position
fields. The action library supports dragging or clicking to add an action.
The editor supports moving blocks, fit/zoom, undo/redo, autosave with revision
conflict detection, copies, deletion, and an optional project reference.

## First complete business workflow

Use the Operations review template or assemble these actions:

1. Read task records: upload or enter a JSON array with `title`, `owner`, `status`,
   `due`, and optional record identifiers. No remote data is fetched implicitly.
2. Select tasks: include all, open, blocked, or complete records.
3. Prepare operations report: produce a Markdown table and next-action list.
4. Review result: stop for an administrator to approve the actual output.

This is a deterministic report assembled from supplied records. It does not
research, infer facts, send email, update a tracker, or claim AI-generated analysis.
A brief action is also available. Reports can be previewed and downloaded.

## Ownership and execution

`n:central:workflows` is a NexusEngine DSK. It owns saved definitions, revision
history, immutable run definitions, per-step progress/output, review decisions,
and app enablement. CentralClient commands work through existing direct, IPC,
and HTTP transports. SQLite snapshots preserve this state independently of UI.

The host coordinator validates the graph and exact action versions before
execution. Connections carry typed records or documents. DAG roots and ready
steps run with at most three concurrent steps per run and three active runs.
Conditions support always, nonempty, and empty inputs. Cycles, missing required
fields, unavailable versions, incompatible connections, duplicate IDs, and
oversized definitions are rejected. Each workflow permits one active run;
a repeated submission key returns the same run.

Pause stops new step dispatch; an active pure transformation may finish.
Cancellation aborts workers. Retry keeps completed outputs and reruns only failed
steps. Restart pauses interrupted workflows; explicit resume restores pending
steps. Reviews survive restart without being replayed. Runs and artifacts retain
the definition revision, action versions, and project metadata.

## Sandbox and tools

**This release enables only trusted, fixed built-in transformations. It does not
execute generated code, repository code, plugins, or agent processes in this
worker.** Node's permission model is not a security boundary for hostile code.
External agent apps remain disabled until stronger isolation is implemented and
proved on supported targets.

On Linux the worker uses Python 3 + libseccomp to deny socket/network and process
inspection system calls, then launches Node with its permission model. File read
and write grants cover only the current attempt folder and the trusted worker
source. Child processes, addons, workers, and inspectors are not permitted.
The environment is reduced to PATH, HOME, and TMPDIR. CPU time, output file size,
open files, V8 heap, and wall time have limits. Inputs are bounded before launch.
The adapter rejects linked sandbox directories. No shell is invoked.

Every run has a distinct sandbox; each attempt has its own directory. Host tools
import configured data, read worker results, register hashed report artifacts,
request/record review, and export artifacts to an authorized client. These
operations produce audit receipts. No external destinations or URLs are accepted
by these transformations. Temporary files can be cleared for finished inactive
runs while reports, run history, and receipts remain retained.

Linux requires Node 24+, Python 3, and libseccomp (Debian/Ubuntu: `python3` and
`libseccomp2`). Capability probing fails closed. Other operating systems can edit
and save workflows, but execution is disabled until a supported worker exists.
The existing desktop package does not bundle Python or a cross-platform isolation
runtime. No claim of full native deployment readiness is made.

## Luminary Framework boundary

Workflow definitions support metadata containing `framework`, `projectRef`, and
`sourceRefs`. Run snapshots preserve those references. Action definitions declare
version, configuration fields, accepted input/output types, tools, and permissions.
This provides an extension point for future Framework actions and connectors.

The Framework's canonical documents, business trackers, decisions, and access
rules must remain authoritative. Future connectors must resolve exact source
records, import through scoped tools, propose changes with provenance, and use
idempotent approved write tools. A completed report does not silently update
canonical business state. No live Drive, CRM, outreach, or ticketing connection
is included here, and no claim is made that existing Framework skills run already.

## Request coverage

| Requested area | Current delivery | Remaining work |
| --- | --- | --- |
| Navigation | Home / Workflows / Apps / Activity, advanced Settings | Further usability feedback |
| Home | Launch, saved workflows, operations template, live counts, review entries | More business templates |
| Builder | Drag/drop, connections, moving, duplicate/delete, zoom/fit, undo/redo, autosave, accessible Add/position controls | Multi-selection and optional richer layout tools |
| Configuration | Field schemas, JSON upload, input picker, conditions, defaults | Real repository/account pickers with connectors |
| Action contract | Versioned built-in registry, typed inputs/outputs, tool declarations | Trusted plugin manifest installation and migrations |
| Execution | Durable DAG runs, parallel steps, conditions, pause/cancel, retry/recovery, revisions | External action exactly-once reconciliation |
| Sandbox | Linux restricted trusted worker, scoped attempts, resource limits, cleanup | Hostile-code container/VM isolation; macOS/Windows workers |
| Tool boundary | Bounded imports, worker result reads, artifacts, review, audited export | Scoped external app tools and cross-workflow transfers |
| Apps | Curated catalog, built-in connect/disconnect, dependency checks, explicit unavailable states | Credentials, health/update/rollback lifecycle for external apps |
| GitHub harness installation | Not enabled | Manifest validation, pinned checkout, isolated dependencies, update proof/rollback |
| NexusAgent | Catalog entry with unavailable reason | Child-harness inventory, adapter implementation, live proof |
| Codex / Hermes / Claude / Antigravity | Existing Codex experiments preserved in Settings; workflow apps unavailable | Verified sandbox adapters and supported external surfaces |
| Review | Per-action outputs, artifacts, approval, revised-run copies, retained diagnostics | More formats and external action approval packets |
| Reuse | Definitions, revisions, copies, history, operations template | Parameter substitution, triggers, schedules |
| Reliability | SQLite/jobs/audit integration, recovery, bounded workers, graceful LAN restriction error | Production transfer, live cloud/device/mesh proof, native upgrades |
| Validation | Domain, real worker, security boundary, desktop/mobile browser checks | Four native targets and real external integrations |

This delivers the requested first foundation. The complete 16-area integration
roadmap is not finished and must not be advertised as complete business autonomy.

## Validation commands

```sh
npm ci
npm run build
npm run check:workflows
npx playwright install chromium
npm run check:workbench-browser
```

`NEXUS_SKIP_NETWORK_PROOF=1 npm run check:integration` is a bounded local regression
mode. It explicitly returns `partial` and marks LAN/PeerJS proof skipped. Default
integration and CI retain the full network checks. Browser validation launches
the real Nexus Node and built static app together on localhost, and stores fresh
screenshots and machine-readable evidence under `output/workbench-browser`.

Retained local proof: [October 11 validation](evidence/workbench-2026-10-11/README.md).
