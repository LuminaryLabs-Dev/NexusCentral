# NexusCentral Decision Coverage

Purpose: steer question workers toward material gaps. Do not repeat locked,
answered, active, or queued decisions. Stop before 100 if every consequential
choice is resolved.

## Locked

- Scope: complete production-ready system, not a shell or partial tab set.
- Navigation: Work, Router, Models, Workflows, Runs, Settings.
- Work: explicit Chat, Coding, and Research conversation modes.
- Router: named drag-configured queue groups with FIFO atomic leases.
- Providers: direct, LAN mesh, and PeerJS use one transport-neutral queue.
- Eligibility: heartbeat and capability match; endpoint failure returns an error
  and routes to the next queue object.
- Workflows: schedules, batches, and multi-agent stages with automatic,
  approval-gated, or manual authority per stage.
- State: each local computer is its own NexusCentral user/node and owns its
  local context, checkpoints, artifacts, memory, sandboxes, workflows, runs,
  approvals, and credentials. This user state does not sync between machines.
- Sharing: only model routing is shared between nodes: route requests,
  provider/model availability, and routing outcomes. Model and harness output
  remains untrusted.
- Memory: Work is isolated per conversation. Workflow runs are isolated and may
  promote validated records into versioned workflow memory.

## In Flight

- Acceptance proof and recovery validation.
- Workflow-memory promotion authorization.
- Lease retry and failure behavior.
- Primary operator and product success priority.
- Approval surface and immutable approval binding.
- Lease coordinator and normalized provider adapter ownership.
- Untrusted-output admission and Router edit activation.
- Failover attempt identity.
- Sensitive evidence handling.
- V1 deployment boundary.
- Canonical metadata and artifact persistence boundary.
- Routing-only shared-state topology, enrollment, and recovery.

## Priority Gaps

1. Work and conversations
   - New conversation defaults and resumability.
   - Chat streaming, stop, retry, branch, and model visibility.
   - Coding workspace selection, diff review, validation, and promotion.
   - Research source policy, citations, crawl limits, and deliverables.
   - Attachments, context limits, and explicit prior-memory attachment.

2. Models and runtimes
   - Discovery and import sources.
   - Download, verification, storage quotas, and cleanup.
   - Load, unload, warmup, eviction, and resource arbitration.
   - Model identity, aliases, variants, capabilities, and benchmarks.
   - Runtime adapters, compatibility failures, and health evidence.

3. Router and queue groups
   - Priority classes, fairness, concurrency, backpressure, and capacity.
   - Capability constraints, context limits, modality, and tool requirements.
   - Group versioning, rollback, validation, and configuration conflicts.
   - Provider trust, enrollment, disconnect, drain, and quarantine.
   - Queue visibility, manual intervention, cancellation, and starvation.

4. Workflow authoring
   - Editor model, stage contracts, branching, and dependencies.
   - Trigger, schedule, batch-input, and concurrency semantics.
   - Draft, test, publish, version, pause, resume, cancel, and rollback.
   - Retry budgets, cost limits, time limits, and partial-batch policy.
   - Template reuse, parameterization, secrets, and environment binding.
   - Reviewer stages, delivery gates, and memory-promotion placement.

5. Runs, evidence, and approvals
   - Canonical run and stage state machines.
   - Live logs, streams, artifacts, diffs, validation, and provenance views.
   - Search, filter, compare, replay, export, retention, and deletion.
   - Approval roles, expiry, delegation, rejection, and re-request behavior.
   - Artifact containment, hashes, download authorization, and large files.

6. Settings and service operations
   - Localhost versus trusted-LAN authentication and authorization.
   - Credential storage, redaction, rotation, and provider secret boundaries.
   - Start-on-login, service status, restart, diagnostics, and degraded mode.
   - CPU, GPU, RAM, disk, network, cost, and concurrency ceilings.
   - Backup, restore, migration, update, and rollback policy.

7. Local state and routing-only sharing
   - Per-node state ownership and local durable persistence.
   - Exact shared routing records, route-result visibility, and retention.
   - Node enrollment, route advertisement, disconnect, and offline recovery.
   - No synchronization of conversations, workflows, runs, artifacts, approvals,
     sandboxes, credentials, or user memory across machines.

8. Canonical state and memory
   - Transaction boundaries, schema versioning, and restart recovery.
   - Context assembly, token budgets, compaction, and source preservation.
   - Memory promotion, supersession, provenance, retention, and revocation.
   - Artifact index rebuild, corruption detection, and garbage collection.
   - Conversation export, fork, archive, delete, and explicit context sharing.

9. Harnesses, tools, and sandboxes
   - Direct-inference and Codex CLI harness selection.
   - Profile tool declarations and fail-closed capability enforcement.
   - Workspace-copy sandbox creation, cleanup, reuse, and path containment.
   - Tool-call approvals, cancellation, timeout, and validation contracts.
   - Loopback OpenAI gateway isolation and per-run configuration.

10. Reliability and observability
   - Process restart recovery and orphaned lease reconciliation.
   - Idempotency, duplicate output, late output, and checkpoint consistency.
   - Health states, circuit breakers, fallback exhaustion, and terminal errors.
   - Audit events, metrics, resource usage, alerting, and diagnostic bundles.

11. API and integration
    - Canonical runs.submit contract and compatibility aliases.
    - Run, artifact, approval, provider, model, and workflow API boundaries.
    - Streaming/event protocol, pagination, errors, auth, and API versioning.
    - OpenAI-compatible gateway scope and external client behavior.

12. Six-tab UX
    - Hero actions versus advanced controls for each tab.
    - Cross-tab links without duplicating ownership.
    - Empty, loading, offline, degraded, failure, and recovery states.
    - Responsive layout, keyboard access, accessibility, and destructive gates.

13. Delivery and acceptance
    - V1 rollback boundary and V2 data migration.
    - Existing check, integration, scale, live-model, Codex, and human-view proof.
    - Release packaging, support diagnostics, and acceptance sign-off.

## Question Standard

- Ask one decision per question with two to four short choices.
- Start with a verified current-state statement naming the relevant V2 file,
  symbol, domain kit, command, or visible surface.
- Explain what the decision changes now and how it shapes the long-term system.
- Define technical terms with a concrete NexusCentral example.
- Give every choice an immediate operational consequence and long-term
  tradeoff; explain why choice 1 is recommended.
- Prefer decisions that alter architecture, UX ownership, reliability, security,
  operating cost, or acceptance proof.
- Put the best long-term default first.
- Do not ask implementation trivia that can be safely inferred from locked
  decisions or existing conventions.
