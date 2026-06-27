# Routing Master Implementation Sequence

## Current Status

As of 2026-03-29, the Desktop codebase implements the core code slices in this sequence:

- canonical peer convergence,
- LAN advertisement and auto-discovery,
- trust and auth repair flows,
- imported remote inventory as peer-owned route targets,
- role binding and runtime dispatch against imported targets,
- split Router workspace projection,
- peer-aware runtime blocking and operator-facing degraded-state messaging,
- and restart-safe persistence plus regression coverage for the main LAN routing behaviors.

The remaining open step is the final real-LAN proof on two physical machines. That step cannot be claimed from local unit or integration tests alone and should remain open until that hardware validation is run.

## Goal

Turn Router into a real multi-machine LAN orchestration surface that:

- discovers nearby Nexus Central peers automatically,
- preserves a single canonical peer record per machine,
- imports real remote runtime inventory as stable route targets,
- keeps role assignment and runtime dispatch aligned with peer health,
- and exposes all of that through a split Router workspace that can be extended safely.

This sequence is intentionally ordered so each step hardens the architecture before the next layer is added. The objective is not just to make routing work once, but to make it reliable enough that later features can build on it without reworking core ownership boundaries.

## Final Goal In Plain Terms

When this work is finished, Router should behave like a seamless LAN routing console instead of a manual remote-forwarding tool.

The operator experience should be:

- open Router and immediately see nearby Nexus Central machines,
- trust a peer once instead of retyping its address repeatedly,
- inspect the real remote runtime inventory that peer exposes,
- assign roles to specific imported remote targets just like local targets,
- see clear health and trust state for every peer and target,
- and route work only through healthy, trusted, and admitted targets.

The architecture outcome should be:

- one canonical peer model per remote machine,
- one canonical route-target model per imported remote instance,
- one authoritative runtime admission path that blocks unsafe dispatch,
- and one Router UI that projects those service-owned states without inventing its own parallel state.

## What "Done" Actually Means

Routing is not done when two machines can hit each other over HTTP. Routing is done only when the full product behavior exists end to end.

The feature is only complete when all of the following are true:

- Peer discovery works automatically on a normal LAN.
- Manual peer entry still works as a fallback and converges into the same peer system.
- Each remote machine keeps one stable peer identity across restart, hostname change, and IP change.
- Trust survives restart and token rotation failures can be repaired without deleting the peer.
- Remote inventory imports as distinct route targets rather than one generic peer placeholder.
- Roles can bind to imported remote targets and keep those assignments visible even when degraded.
- Runtime dispatch rejects untrusted, stale, offline, auth-failed, or overloaded remote targets even if the UI gets out of sync.
- Router UI makes every major peer and target state understandable to the operator.
- Cached peer and imported-target state survive restart so offline peers remain inspectable.
- The entire flow is proven on two real LAN machines and the docs match that proven behavior.

## Final Product Shape

### Final Peer Model

The final peer model must represent machines, not ad hoc connection attempts.

Each peer record must contain:

- a stable peer id,
- display name,
- last known reachable control-plane address,
- trust state,
- auth failure state if present,
- last seen timestamp,
- cached inventory summary,
- cached imported targets or enough metadata to rebuild their last known presentation,
- and source information showing whether the peer was first seen manually, discovered automatically, or both.

The peer model must survive:

- restart,
- host or IP changes,
- temporary discovery loss,
- temporary auth failure,
- temporary runtime inventory failure.

### Final Route-Target Model

The final route-target model must represent actual imported runtime instances, not just machines.

Each imported target must contain:

- a stable route-target id,
- owning peer id,
- remote instance id,
- model id,
- mode and capabilities,
- health and readiness state,
- routing eligibility,
- and enough cached metadata to keep degraded assignments visible.

Route targets must remain distinct from peer records because one peer can expose multiple targets and those targets can differ in readiness, mode, and assigned roles.

### Final Router UI

The final Router page must show two related but separate things at once:

- peer-level state: discovery, trust, auth, stale or offline condition,
- target-level state: imported runtime instances, role assignments, health, and routing eligibility.

The page should answer these operator questions without making the user inspect logs:

- What machines are available on the LAN?
- Which of them are trusted?
- Which of them are degraded and why?
- What runtime instances does the selected peer expose?
- Which roles currently point at those instances?
- What would happen if I routed a request right now?

## What Must Happen Technically

The remaining work is not just a UI rewrite. The following technical outcomes must all land for the feature to be solid.

### 1. Discovery Must Become Real

The app must advertise itself on the LAN and browse for other peers continuously.

That means:

- adding a discovery advertisement service,
- adding a discovery browser service,
- publishing a stable peer id in discovery-safe metadata,
- suppressing self-discovery by peer id,
- merging duplicate multi-interface announcements into one discovered peer,
- and degrading cleanly if the local platform or network does not support browsing.

### 2. Manual And Discovered Peers Must Collapse Into One System

The app cannot keep a manual lane and a discovery lane forever.

That means:

- a manual connect must create or repair a canonical peer record,
- a later discovery event for the same machine must merge into that same peer record,
- trust and cached inventory must stay attached to that one peer record,
- and Router rows must remain stable across refreshes instead of jumping between identities.

### 3. Trust Must Be A Real Lifecycle

The token cannot remain just a request header detail.

That means:

- untrusted peers are visible but not routable,
- trusted peers persist across restart,
- auth failures are preserved as a state on the peer instead of silently dropping it,
- repair updates the peer in place,
- and peer discovery never carries the secret itself.

### 4. Inventory Import Must Become First-Class

The app must stop pretending a remote machine is one synthetic endpoint.

That means:

- authenticated inspection must fetch actual remote runtime inventory,
- imported targets must be peer-owned but separately modeled,
- route-target ids must be stable across refresh,
- partial import must be surfaced as partial,
- and cached imported targets must remain visible when live refresh later fails.

### 5. Runtime Must Remain The Final Safety Gate

The UI cannot be the last line of defense.

That means:

- runtime selection must reject untrusted peers,
- runtime selection must reject offline, stale, auth-failed, and unhealthy targets,
- `MaxParallel` admission must still be enforced for imported targets,
- and fallback behavior must be explicit instead of silently routing somewhere else.

### 6. Router UI Must Project The Real Model

The final UI should not invent alternate routing state just to make rendering easier.

That means:

- the peer directory must be driven from canonical peer snapshots,
- the selected peer workspace must project trust state and imported targets from services,
- role assignment UI must bind against real local plus imported targets,
- and every degraded state must be explicit in the view model instead of inferred heuristically in XAML.

## Current Floor

The following pieces are already real and should now be treated as the architectural floor rather than reimplemented:

- LAN control-plane exposure is optional and authenticated for non-loopback requests.
- A stable local machine identity persists through `LanRoutingSettingsService`.
- A durable peer registry persists peer records, cached summaries, auth failure state, and offline-visible metadata.
- Manual remote connect and remote refresh now sync through `LanPeerRegistryService`.
- Remote probes now carry stable peer identity and block accidental self-routing back into the same machine.
- Remote heartbeat refresh, stale classification, and `MaxParallel` runtime admission already exist.

## Remaining Gaps Blocking Intended Routing

- Discovery is still manual. There is no LAN advertisement or peer browsing yet.
- The product still does not have one canonical peer system that merges manual and discovered identities automatically.
- Trust exists at the token level, but the operator pairing and repair flow is incomplete.
- Remote peers still model mostly as one peer row rather than imported first-class route targets.
- Role assignment is not yet built on imported remote targets that survive degraded states.
- Router UI is still not the intended split peer-directory plus detail workspace.
- Runtime target selection is still limited and not yet peer-aware beyond explicit remote targeting.
- Reliability around retry, duplicate suppression, partial import, cached restart continuity, and shutdown lifecycle still needs a full hardening pass.

## Ownership Model That Must Not Drift

The Router will only stay maintainable if these ownership rules remain fixed:

| Concern | Service Owner | Notes |
| --- | --- | --- |
| Local machine identity, exposure flag, shared secret | `LanRoutingSettingsService` | Never derive identity from hostnames. |
| Canonical peer records, trust state, cached peer metadata | `LanPeerRegistryService` | One machine equals one peer record. |
| Discovery advertisement and browse snapshots | discovery services | Discovery is advisory only and never stores secrets. |
| Authenticated peer inspection and imported route-target shaping | `ModelLibraryService` | Peers and route targets are related, but not the same entity. |
| Runtime readiness, admission, and remote dispatch blocking | `RuntimeService` + `RoutingRequestTelemetryService` | Runtime must reject unhealthy or untrusted targets even if UI is wrong. |
| Operator-visible peer and target projection | `RouterViewModel` | UI projects state; it does not own discovery, trust, or routing logic. |
| Mutable state location | `AppPathsService` | All routing state remains under the app state root. |

## Implementation Sequence

### Step 1: Finish Canonical Peer Identity Convergence

Purpose:
Make the peer registry the one durable source of truth for machine-level identity regardless of whether a peer is first seen manually or via discovery.

Implement:

- Add the missing merge path between discovered peers and already-known manual peers.
- Ensure peer rows converge by stable peer id, not by current IP or hostname alone.
- Preserve trust, cached import data, and operator-visible peer continuity when host or IP changes.
- Define the exact peer merge precedence rules for display name, last known host, last seen time, trust state, cached import summary, and auth failure reason.
- Ensure route references can survive peer identity reconciliation without creating duplicate target ownership.

Success gate:

- A peer discovered after manual entry merges into the same canonical record.
- A machine changing IP does not create a second trusted peer row.
- Removing one route reference does not discard peer state that is still referenced elsewhere.

Why first:

Discovery, trust, import, and UI all become unstable if peer identity is not canonical before those layers expand.

What this unlocks next:

- Discovery can safely merge into existing peers.
- Trust and repair flows can update durable peer records instead of temporary entries.
- Imported targets can be attached to a stable machine owner.

### Step 2: Add LAN Advertisement And Auto-Discovery

Purpose:
Replace host-and-port-only discovery with actual LAN presence while keeping discovery optional and non-authoritative.

Implement:

- Add `_nexuscentral._tcp.local` advertisement for the authenticated control plane.
- Publish only discovery-safe metadata such as stable peer id, display name, and control-plane address.
- Add a discovery browser that merges duplicate announcements, filters the local peer id, and degrades safely when discovery cannot start.
- Cache enough discovery metadata to preserve peer visibility when the LAN disappears between sessions.
- Define the transition rules from `Discovered` to `Offline`, `Stale`, and `Hidden` if announcements expire.

Success gate:

- A second Nexus Central instance appears automatically on the same LAN.
- The local machine never shows itself as a remote peer.
- Multi-interface or duplicate service announcements collapse into one peer row.
- Discovery failure does not break manual peer repair or existing trusted peers.

Why second:

Until discovery is real, the intended seamless experience cannot exist and the split Router UI would still be built around a placeholder manual workflow.

What this unlocks next:

- Router can become a real peer directory instead of a manual host form.
- Trust can start from a discovered peer row rather than from handwritten connection data.

### Step 3: Complete Trust, Auth Failure, And Repair Flows

Purpose:
Turn token-based access into a full operator-visible pairing lifecycle rather than a hidden transport detail.

Implement:

- Add explicit trust and re-trust commands for discovered and manual peers.
- Promote bad or rotated-token peers into `AuthFailed` rather than removing them.
- Add repair flows that update the stored secret without deleting cached peer or imported-target state.
- Make the selected-peer workspace show the exact action needed for each trust state.
- Persist the difference between `never trusted`, `trusted but auth failed`, and `trusted but currently offline`.

Success gate:

- Untrusted discovered peers are visible but not routable.
- Trusting a peer succeeds and persists across restart.
- Token rotation moves the peer into a repairable auth-failed state.
- Repair restores the same peer record instead of creating a replacement.

Why third:

Trusted inspection has to exist before imported targets can be treated as dependable routing inputs.

What this unlocks next:

- Remote inventory can be imported against peers the operator has explicitly trusted.
- Cached imported targets can remain attached to peers even when auth later breaks.

### Step 4: Import Remote Inventory As First-Class Route Targets

Purpose:
Stop modeling a remote machine as one synthetic routing row and start modeling the real remote runtime inventory it exposes.

Implement:

- Extend authenticated inspection to import remote instances, capabilities, health, and routing modes.
- Create stable imported route-target ids that are owned by a peer but distinct from the peer id.
- Preserve cached imported targets for offline and stale rendering.
- Surface partial import explicitly when status works but inventory fetch does not.
- Decide and document exactly where imported target state persists and what subset is safe to cache.
- Ensure imported targets remain traceable back to the owning peer and remote instance id.

Success gate:

- One trusted peer can expose multiple imported route targets.
- Imported target ids remain stable across refreshes and restart.
- Partial import is visible as partial, not healthy and not absent.
- Cached imported targets remain visible after a later offline transition.

Why fourth:

This is the point where routing becomes real rather than symbolic. Everything after this step should build on imported targets, not peer placeholder rows.

What this unlocks next:

- Role assignment can target real remote runtime instances.
- Router can show a selected peer’s imported inventory in the right-hand pane.

### Step 5: Bind Roles And Runtime Dispatch To Imported Targets

Purpose:
Make imported targets first-class citizens in the same routing model as local targets, while keeping degraded assignments visible and unsafe dispatch blocked.

Implement:

- Allow roles to target imported remote instances directly.
- Preserve degraded assignments when a peer becomes stale, offline, cached-only, or auth-failed.
- Keep runtime admission authoritative: unhealthy, stale, untrusted, or busy imported targets must be rejected with explicit reasons.
- Show role-to-peer ownership clearly so operators can see when a role depends on a remote machine.
- Preserve assignment continuity without silently auto-clearing broken remote targets.

Success gate:

- Roles can point to local or imported remote targets with stable ids.
- A degraded remote assignment remains visible to the operator.
- Runtime blocks unhealthy imported targets with explicit error text.
- `MaxParallel` enforcement still holds for remote routing.

Why fifth:

Once roles and runtime depend on imported targets, the feature can be exercised through normal routing paths rather than special-case remote commands.

What this unlocks next:

- The split Router UI can render real role context instead of provisional placeholders.
- Runtime and UI can finally speak the same target language.

### Step 6: Refactor Router Into The Intended Split Workspace

Purpose:
Expose the real peer and imported-target model through a UI that can scale, rather than extending the current compact panel further.

Implement:

- Reshape Router into a Models-style split layout: peer directory on the left, selected peer workspace on the right.
- Keep the request queue and routing ladder header strip, but move peer operations into the split body.
- Render explicit states for scanning, empty, connected, stale, offline, auth-failed, cached-only, and partial-import.
- Make the left pane stable under refresh so peers do not jump unpredictably.
- Make the right pane answer both peer questions and target questions without mixing those concepts.

Success gate:

- The left pane acts as a stable peer directory and filter surface.
- The right pane shows trust actions, repair actions, imported targets, and role context for the selected peer.
- Every degraded state has a distinct operator-facing UI treatment.

Why sixth:

The split UI should land only after the real peer and target model exists, otherwise the UI will have to be rebuilt around different state ownership later.

What this unlocks next:

- Peer-aware routing policies and operator messaging can be shown clearly in the intended workspace.

### Step 7: Add Peer-Aware Routing Policy And Operator Messaging

Purpose:
Close the gap between what Router shows and what runtime actually does.

Implement:

- Add peer-aware target selection rules on top of the real imported-target model.
- Keep local fallback explicit rather than silent.
- Explain why a peer or target is blocked: untrusted, stale, auth-failed, cached-only, partial-import, or route-target-busy.
- Surface the difference between `discoverable`, `trusted`, `inspectable`, and `routable` so the product does not imply readiness too early.
- Ensure status strings in Router use the same reasoning as runtime dispatch decisions.

Success gate:

- Runtime behavior matches the status shown in Router.
- No untrusted discovered peer is ever used automatically.
- When no eligible remote target exists, the fallback path is explicit.
- Operator-visible errors explain exactly why routing is blocked.

Why seventh:

This is the layer that turns the system from a collection of connected services into an understandable routing product.

What this unlocks next:

- Reliability work can harden one consistent operator contract instead of moving targets.

### Step 8: Harden Reliability, Lifecycle, And Restart Continuity

Purpose:
Make the feature resilient under real LAN churn rather than only under ideal conditions.

Implement:

- Add retry and backoff for discovery and inventory refresh.
- Ensure stale and offline transitions are deterministic.
- Stop discovery, timers, and refresh loops cleanly on shutdown.
- Preserve trusted offline peers and cached imported targets across restart.
- Add test coverage for duplicate suppression, partial-import retention, self-suppression, and restart continuity.
- Ensure background services cannot leak state updates into disposed views or shutdown app lifetimes.

Success gate:

- Repeated network failures back off instead of spamming retries.
- Duplicate peers do not reappear after interface churn.
- Trusted offline peers still render after restart.
- Background tasks shut down cleanly without orphaned loops.

Why eighth:

Without lifecycle hardening, the feature may appear complete in a short demo and then fail under normal network changes or restart cycles.

### Step 9: Prove The Feature On Two Real Machines And Freeze The Contracts

Purpose:
Convert the plan into an operational proof and lock the architecture with matching documentation.

Implement:

- Run the full two-machine proof: discovery, trust, import, assignment, dispatch, stale detection, auth repair, restart continuity.
- Update all Router LAN docs and desktop docs to match the actual shipping behavior.
- Hold further Router feature work until the final behavioral contract matches the implementation.

Success gate:

- Two real LAN machines discover and trust each other successfully.
- Imported targets appear and route correctly.
- Auth failure and repair behave without peer duplication or state loss.
- Restart continuity and cached offline rendering work.
- Docs match the implementation exactly.

Why last:

This step is where the architecture stops being aspirational and becomes the stable base for future routing features.

## Execution Rules

Apply the same gate discipline on every step:

1. Implement only one step at a time.
2. Run `dotnet build Desktop/NexusCentral-Avalonia.csproj` after each step.
3. Launch the Desktop app and run only the smoke checks for that step.
4. Update the Router LAN docs pack and Desktop docs immediately after the behavior changes.
5. Do not start the next step until the current step passes its success gate in full.

## Definition Of A Solidified Architecture

Routing should be considered architecturally solid only when all of the following are true:

- The peer registry is the one durable peer source of truth.
- Discovery is advisory and merges into the canonical peer system cleanly.
- Remote inventory is represented as stable imported route targets.
- Runtime dispatch is stricter than the UI and never trusts degraded state by accident.
- Router UI projects the service-owned model rather than inventing parallel state.
- The full two-machine proof passes and the docs describe exactly what the code does.

## Non-Goals And Anti-Patterns

The following shortcuts would make the feature look finished temporarily while weakening the architecture. They should be treated as regressions, not acceptable compromises.

- Do not create a second peer system just for discovered peers.
- Do not model one peer as one route target once remote inventory import exists.
- Do not let XAML infer trust or health state from string parsing.
- Do not silently clear degraded role assignments just because a peer is currently unavailable.
- Do not auto-route to a discovered but untrusted peer.
- Do not store routing state outside `AppPathsService`-owned state paths.
- Do not let runtime trust UI eligibility alone; runtime must recheck dispatch safety.

## Short Final Vision

The end state is a Router where machines appear automatically, trust is explicit, imported remote instances are real routing targets, degraded state stays visible instead of disappearing, and runtime behavior always matches what the UI claims.
