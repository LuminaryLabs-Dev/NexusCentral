# Implementation Standards

## MVVM Standards

- [ ] Keep all UI logic in view models and all discovery/runtime logic in services
  - [ ] No socket, HTTP, timer, or file operations in `RouterView.axaml.cs`.
  - [ ] No direct service mutation from XAML bindings beyond commands.
  - [ ] No discovery polling from the view layer.

- [ ] Use snapshot projection for Router UI state
  - [ ] Services should publish immutable or snapshot-friendly data.
  - [ ] `RouterViewModel` should translate snapshots into observable collections.
  - [ ] Child view models should stay shallow and disposable.

- [ ] Keep command boundaries explicit
  - [ ] `PairPeerCommand`
  - [ ] `ForgetPeerCommand`
  - [ ] `RefreshPeerCommand`
  - [ ] `ToggleDiscoveryCommand`
  - [ ] `ImportPeerInventoryCommand`
  - [ ] `RetryPeerAuthCommand`

## Networking Standards

- [ ] Keep the existing authenticated HTTP control plane as the only management API
  - [ ] Discovery finds peers.
  - [ ] Authenticated HTTP inspects and controls peers.
  - [ ] Runtime forwarding continues through the existing control plane contract.

- [ ] Prefer stable local-network standards over custom UDP formats
  - [ ] mDNS/DNS-SD is preferred over ad hoc broadcast JSON.
  - [ ] If a fallback discovery path is ever added, keep it behind the same peer registry abstraction.

- [ ] Fail closed on auth
  - [ ] Discovery alone never grants routing rights.
  - [ ] A discovered peer without trust should be visible but non-routable.
  - [ ] Auth failures should degrade the peer to `AuthFailed`, not silently disappear.

- [ ] Keep discovery and import resilient under partial failure
  - [ ] Status, inventory, and role fetches should fail independently.
  - [ ] A peer should not vanish just because one inspection endpoint failed.
  - [ ] Backoff repeated failures instead of retrying aggressively on every timer tick.

## State Standards

- [ ] Separate persistent trust state from ephemeral discovery state
  - [ ] Persist trust, manual peers, and user choices.
  - [ ] Do not persist volatile active-request counters.
  - [ ] Do not persist stale discovery records as authoritative truth.

- [ ] Keep state under `AppPathsService`
  - [ ] No hardcoded files outside the state root.
  - [ ] New peer registry documents must be versionable.
  - [ ] Migration should be possible if the peer schema changes later.
  - [ ] Stored secrets should be isolated to the peer registry layer rather than copied into multiple documents.

## UX Standards

- [ ] Discovery should feel seamless but not magical
  - [ ] Show discovered machines immediately.
  - [ ] Require explicit trust before routing through them.
  - [ ] Preserve a clear manual fallback path.
  - [ ] Keep trusted offline peers visible so the user does not feel like devices are randomly disappearing.

- [ ] Status should be legible at a glance
  - [ ] Online, stale, auth failed, and offline states should render differently.
  - [ ] Imported target counts should be visible in the list.
  - [ ] Role assignments should show whether they point local or remote.
  - [ ] Cached or partial data should be visually distinguished from live complete data.

- [ ] Avoid hidden state transitions
  - [ ] If a peer moves from connected to stale, the UI should explain why.
  - [ ] If routing is blocked because a peer is stale or busy, surface that reason in Router and runtime error text.
  - [ ] If a token rotates remotely, show an auth repair path instead of forcing the operator to recreate the peer.

## Runtime And Routing Standards

- [ ] Preserve the existing routing invariants
  - [ ] Busy-state stays driven by `RoutingRequestTelemetryService`.
  - [ ] Role-based `MaxParallel` enforcement stays in runtime admission.
  - [ ] Queue ownership stays in `ChatViewModel`.
  - [ ] Imported offline targets can remain visible in assignments, but runtime admission must still block them.

- [ ] Do not bypass `ModelLibraryService` for route selection
  - [ ] Imported peer targets should become regular route targets.
  - [ ] Role assignment should continue to resolve through `ModelLibraryService`.
  - [ ] Runtime should remain ignorant of discovery mechanics.

## Testing Standards

- [ ] Add unit tests around new persistent state services
  - [ ] Peer registry serialization.
  - [ ] Trust-state updates.
  - [ ] Discovery merge behavior.
  - [ ] Manual-plus-discovered peer merge behavior.
  - [ ] Token rotation and auth-failed persistence behavior.

- [ ] Add integration-style tests around route resolution
  - [ ] Imported remote target ids.
  - [ ] Role assignment to remote imported targets.
  - [ ] Stale peer blocking.
  - [ ] Partial import handling.

- [ ] Add manual validation checklist for the UI
  - [ ] Two machines on the same LAN discover each other.
  - [ ] A tokenless discovered peer cannot be routed.
  - [ ] A trusted peer imports inventory.
  - [ ] Role assignment can target imported instances.
  - [ ] A stale peer becomes unavailable without crashing the UI.
  - [ ] A trusted peer remains visible across restart while offline.
  - [ ] A token-rotated peer can be repaired without deleting the peer entry.