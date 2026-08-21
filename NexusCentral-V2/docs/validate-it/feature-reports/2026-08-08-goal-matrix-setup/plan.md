# Goal Matrix Setup Plan

## Scope

Create and validate the complete planning control plane only. Do not change application runtime code, reconcile branches, deploy, use credentials, install harnesses, or run paid/live providers.

## Intent

- One NexusCentral application and source tree.
- Desktop, public web, and internal web are capability profiles, not forks.
- Each node owns local user state; shared infrastructure routes model work only.
- Public reachability, visibility, enablement, authorization, and support are separate states.
- NexusCentral retains run, route, sandbox, tool, artifact, approval, and cancellation authority for every harness.
- Plans and simulations never count as product-runtime proof.

## Matrix Shape

| Order | Domain | Required outcome |
|---|---|---|
| 1 | Goal control | Durable matrix, ledger, queue, validation, and simulation control. |
| 2 | Source authority | Preserve durable work and gate branch integration. |
| 3 | Shared application | Keep one source, contract, component, and command system. |
| 4 | Runtime profiles | Resolve desktop, public web, and internal web capabilities. |
| 5 | Local node state | Keep user work and recovery state on its owning computer or browser. |
| 6 | Security | Enforce identity, permissions, secrets, origins, quotas, and cost limits. |
| 7 | Models and runtimes | Catalog, start, route, and qualify local, browser, and remote models. |
| 8 | Router server | Share model capacity without taking ownership of requester state. |
| 9 | Runs and proof | Persist lifecycle, context, artifacts, approvals, and recovery evidence. |
| 10 | Harness and sandbox | Adapt harnesses behind NexusCentral authority and SimSpace containment. |
| 11 | Work | Support chat, coding, and research with task-appropriate routes. |
| 12 | Workflows | Support versioned, queued, fail-forward stages with final human gates. |
| 13 | Internal API | Preserve application contracts while enforcing server-side authorization. |
| 14 | UI | Render the same application according to profile capabilities. |
| 15 | Operations | Run desktop services, internal web sessions, and static public web builds. |
| 16 | Release proof | Require automated, human-view, profile-parity, and approval evidence. |

## Validation Chain

1. Audit repository and dirty-work boundaries.
2. Materialize the complete hierarchy and atomic leaves.
3. Simulate one injected failure and one review pass for every domain.
4. Simulate supported, conditional, rejected, and held harness/model routes.
5. Validate JSON, node contracts, ownership, parents, dependencies, DAG, revisions, statuses, ledger, and queue.
6. Repair only observed structural or semantic defects.
7. Re-run simulations after repairs.
8. Promote matrix-control setup only.
9. Leave product and release work planned, with one unclaimed ready leaf.

## Pass Gate

The matrix is usable when every node has one owner and gate, the DAG is valid, every subgoal has an atomic action, every major domain has a reviewed simulation, speculative routes remain held, the ledger reconstructs state, and the queue derives exactly one safe next leaf.
