# Streamlined UI Intake

Status: active intake

## Operating Rules

- Ask one numbered question at a time.
- Keep questions and choices short while stating the main long-term tradeoff.
- Record decisions and concise design rationale here.
- When the intended change is clear, produce a bounded implementation plan.
- Do not edit product code until the user answers yes to the implementation gate.

## Locked Decisions

- Primary capabilities: coding and research jobs plus model, provider, and runtime management.
- Navigation: use six tabs with one purpose each.
  - `Work`: compose, submit, and follow one Chat, Coding, or Research job.
  - `Router`: control provider selection, fallback, and routing policy.
  - `Models`: discover, download, load, unload, and inspect models and runtimes.
  - `Workflows`: define and operate autonomous work that repeats, batches, or scales beyond one job.
  - `Runs`: review history, status, artifacts, validation, and approvals.
  - `Settings`: control the NexusCentral service, access, credentials, and advanced host configuration.
- Work interaction: explicit Chat, Coding, and Research modes.
  - Rationale: mode selection gives each request a clear harness contract and avoids hidden routing mistakes.
  - Long term: new work types can be added without turning the Work tab into an infrastructure control surface.
  - Tradeoff: submitting work requires one deliberate mode choice.

## Resolved Decision: Work Interaction

1. One request box with automatic routing.
   - Immediate: fastest and least cluttered submission flow.
   - Long term: scales only if route decisions stay visible and correctable.
   - Tradeoff: hidden classification can send work to the wrong harness.
2. Separate Chat, Coding, and Research modes.
   - Immediate: makes intent and harness selection explicit.
   - Long term: gives each work type a stable contract while keeping one Work tab.
   - Tradeoff: one extra choice before submission.
   - Recommendation: strongest balance of clarity, control, and extensibility.
3. Saved job templates.
   - Immediate: accelerates repeated workflows.
   - Long term: useful as a secondary feature after core work modes stabilize.
   - Tradeoff: weak for new or one-off requests and adds template management.
4. Workspace-first coding interface with chat attached.
   - Immediate: strongest experience for repository work.
   - Long term: over-specializes Work around coding and makes research feel secondary.
   - Tradeoff: conflicts with the selected mixed coding and research scope.

Selected: option 2.

## Resolved Decision: Scaled Workflow Scope

1. Recurring schedules.
   - Immediate: makes periodic jobs simple and predictable.
   - Long term: supports maintenance and monitoring, but not broad parallel work.
   - Tradeoff: too narrow for the requested scaled-workflow direction.
2. Batch execution.
   - Immediate: runs one operation across many inputs.
   - Long term: creates a strong throughput primitive, but does not model multi-step autonomy.
   - Tradeoff: batch size and concurrency controls become mandatory.
3. Multi-agent pipelines.
   - Immediate: supports planner, worker, reviewer, and approval stages.
   - Long term: enables sophisticated automation, but carries the highest coordination complexity.
   - Tradeoff: failure recovery and observability must be designed from the start.
4. Unified workflow definitions covering schedules, batches, and multi-agent stages.
   - Immediate: one tab owns the complete autonomous workflow lifecycle.
   - Long term: provides the most extensible control plane while keeping individual execution evidence in Runs.
   - Tradeoff: requires a deliberately small first version to avoid premature complexity.
   - Recommendation: best match for autonomous scaled workflows.

Selected: option 4.

The Workflows tab owns reusable definitions, schedules, batches, agent stages,
live workflow control, and capacity limits. The Runs tab remains the immutable
history, evidence, artifact, validation, and approval surface for each execution.

## Resolved Decision: Workflow Authority

1. Approve every execution before it starts.
   - Immediate: maximum control and minimal surprise.
   - Long term: blocks unattended schedules and creates an approval bottleneck.
   - Tradeoff: autonomy exists only after a human is present.
2. Approve each workflow definition once, then run within fixed limits.
   - Immediate: enables unattended operation with explicit budgets and tool policy.
   - Long term: scales safely if definition revisions invalidate prior approval.
   - Tradeoff: requires versioned definitions and enforceable limits.
3. Run automatically and request approval only for sensitive actions.
   - Immediate: maximizes useful autonomy while guarding writes, spending, publishing, and credentials.
   - Long term: supports broad workflows if action classification remains rigorous.
   - Tradeoff: policy mistakes can either interrupt safe work or permit excessive authority.
4. Assign authority per stage: automatic, approval-gated, or manual.
   - Immediate: each planner, worker, reviewer, and delivery stage gets an explicit boundary.
   - Long term: best supports mixed-risk workflows and reusable governance.
   - Tradeoff: adds configuration, so defaults and summaries must stay simple.
   - Recommendation: strongest long-term control model.

Selected: option 4.

Each workflow stage declares its authority level. Definition revisions preserve
prior execution evidence but require the changed stage policy to be reviewed
before the revised workflow can run unattended.

## Resolved Decision: Router Control Model

1. Select one active provider and model.
   - Immediate: simplest possible routing control.
   - Long term: cannot express fallbacks or different needs for Chat, Coding, Research, and workflows.
   - Tradeoff: simplicity creates a single point of failure.
2. Set a global ordered fallback chain.
   - Immediate: improves availability with little configuration.
   - Long term: remains understandable, but treats every workload as equivalent.
   - Tradeoff: a fallback suitable for chat may be unsuitable for coding or batch work.
3. Assign routes independently to each work mode and workflow stage.
   - Immediate: gives precise control over model quality, cost, locality, and capability.
   - Long term: scales with new harnesses and stage types.
   - Tradeoff: repeated route configuration becomes difficult to maintain.
4. Create named routing profiles containing a primary route, fallbacks, limits, and capability rules.
   - Immediate: Work and Workflows select a concise reusable profile instead of raw infrastructure.
   - Long term: centralizes policy and supports versioned routing across modes and stages.
   - Tradeoff: profiles need clear defaults and usage visibility to avoid configuration sprawl.
   - Recommendation: strongest separation between Router, Work, Models, and Workflows.

Selected: option 4, refined into named queue groups.

Each queue group is a reusable routing target containing multiple model
endpoints rather than one fixed primary. The Router tab provides a drag-and-drop
group builder, ordered membership, endpoint/model configuration, group limits,
and visible fallback behavior. Work modes and workflow stages select a group,
not a raw provider. New endpoints and models enter the Models inventory first
and can then be dragged into one or more queue groups.

## Resolved Decision: Queue Group Selection

1. Strict ordered fallback.
   - Immediate: always tries models in a visible deterministic order.
   - Long term: easy to debug, but underuses healthy secondary models and concentrates load.
   - Tradeoff: later models receive work only after failures.
2. Round-robin across every healthy group member.
   - Immediate: distributes work evenly and uses all available capacity.
   - Long term: scales throughput, but ignores differences in model quality, cost, and capability.
   - Tradeoff: an unsuitable model can receive a task merely because it is next.
3. Best-fit scoring across the whole group.
   - Immediate: chooses by capability, health, queue depth, cost, locality, and latency.
   - Long term: adapts well as endpoints grow, but route outcomes are less predictable.
   - Tradeoff: requires transparent scores and deterministic tie-breaking.
4. Tiered pools with scoring inside each tier and fallback between tiers.
   - Immediate: multiple default models share work while explicit tiers preserve fallback intent.
   - Long term: combines capacity, specialization, and predictable degradation as groups expand.
   - Tradeoff: adds one concept, tier, which the drag-and-drop editor must make obvious.
   - Recommendation: closest match to multiple defaults plus a global-style fallback chain.

Selected: shared FIFO claim queue, supplied by multiple transport types.

Jobs enter a FIFO queue for their selected group. Eligible direct endpoints,
LAN mesh providers, and PeerJS providers participate through the same provider
contract. The first healthy provider with available capacity to atomically claim
the next compatible job receives a time-bounded lease. Lease expiry, disconnect,
or pre-output failure returns the job to the queue; late output from a previous
lease cannot complete the job. Transport affects connectivity, not job priority.

## Deferred Decision: Provider Eligibility

1. Health and availability only.
   - Immediate: lets every connected provider claim work with minimal setup.
   - Long term: maximizes capacity, but may assign tasks to incompatible or weak models.
   - Tradeoff: fastest queue with the least quality control.
2. Declared capability matching.
   - Immediate: filters by context, tools, modality, and task type before a claim.
   - Long term: scales across diverse models if declarations remain accurate.
   - Tradeoff: trusts provider metadata without runtime proof.
3. Validation or benchmark threshold.
   - Immediate: admits only models that pass a group-specific test.
   - Long term: improves quality consistency, but requires reruns after model or configuration changes.
   - Tradeoff: stronger proof with more onboarding work.
4. Policy, health, capability, and validation together.
   - Immediate: only approved, connected, compatible, proven providers can race for the FIFO lease.
   - Long term: safely supports unknown LAN and PeerJS capacity without weakening host authority.
   - Tradeoff: highest admission rigor and the most provider state to explain.
   - Recommendation: required for dependable first-available routing.

## Locked Reliability Invariant

NexusCentral owns canonical context, memory, run state, and checkpoints. Models,
providers, and harnesses are fallible execution workers. Their output is an
untrusted proposal until it passes the stage's parse, schema, validation, and
approval gates.

- Persist the request, selected context references, stage state, tool events,
  outputs, failures, and artifacts independently of any model process.
- Checkpoint before and after every stage and provider lease.
- Assemble bounded model context from canonical records; summaries and retrieval
  indexes are rebuildable derivatives, never replacements for source records.
- A malformed response, timeout, disconnect, harness crash, or failed validation
  records evidence and triggers the configured retry, fallback, approval, or
  failure path without mutating the last valid checkpoint.
- Recovered work resumes from the last valid checkpoint with idempotent stage
  operations and cannot accept late output from an expired provider lease.
- Large context remains in contained artifacts and indexed records; each model
  receives only a capability-compatible context packet.

Long term, this keeps the control plane stable while models, endpoints, context
windows, and harness quality change independently.

## Current Decision: Memory Scope

1. Run-only memory.
   - Immediate: strongest isolation and simplest recovery.
   - Long term: every new run starts without reusable workflow or project knowledge.
   - Tradeoff: reliability is high, learning is low.
2. Workflow memory.
   - Immediate: repeated schedules and pipelines reuse prior validated context.
   - Long term: improves recurring work, but knowledge remains trapped inside each definition.
   - Tradeoff: limited cross-workflow reuse.
3. Project memory.
   - Immediate: Chat, Coding, Research, and Workflows share project knowledge.
   - Long term: creates useful continuity, but stale or incorrect material can contaminate many runs.
   - Tradeoff: requires provenance and cleanup controls.
4. Layered run, workflow, and project memory with gated promotion.
   - Immediate: runs stay isolated while approved knowledge can move into reusable scopes.
   - Long term: supports reliable learning without allowing one weak LLM answer to become global truth.
   - Tradeoff: requires explicit provenance, promotion, supersession, and retention rules.
   - Recommendation: best fit for crash-safe context and unreliable workers.
