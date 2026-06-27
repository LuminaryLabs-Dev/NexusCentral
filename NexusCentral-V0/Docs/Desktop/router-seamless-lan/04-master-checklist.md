# Master Checklist

## Final Outcome This Checklist Must Produce

Completing this checklist should produce one coherent routing system with these properties:

- nearby Nexus Central machines appear automatically,
- each remote machine keeps one canonical peer identity,
- trust and auth repair operate on that canonical peer record,
- each trusted peer exposes imported route targets instead of one generic remote row,
- roles can bind to local and imported targets using stable ids,
- Router shows the intended split peer-directory plus detail workspace,
- and runtime dispatch remains the final authority on what is actually safe to execute.

If a completed item does not move the product toward that end state, it is probably the wrong item.

## Phase 1: Discovery And Peer State Foundation

- [x] Add persistent peer registry paths in `AppPathsService`
  - [x] Add `LanPeerRegistryPath`.
  - [x] Add `LanDiscoveryCachePath` if caching is kept.
  - [x] Add tests for path creation and state-root behavior.

- [x] Add peer registry models
  - [x] Add trusted peer document model.
  - [x] Add discovered peer snapshot model.
  - [x] Add peer trust/status enums.
  - [x] Add stable peer id model separate from machine display name.
  - [x] Add cached import summary fields for offline continuity.

- [x] Add `LanPeerRegistryService`
  - [x] Load and persist trusted peers.
  - [x] Merge manual peers with discovered peers.
  - [x] Publish read-only peer snapshots.
  - [x] Merge manual and discovered peers by stable peer id when available.
  - [x] Keep offline trusted peers visible after restart.
  - [x] Preserve trust, cached import summaries, and auth failure state during peer-id reconciliation.

- [x] Wire the service into `AppServices`
  - [x] Construct it once.
  - [x] Subscribe dependent services to registry change events.
  - [x] Ensure service startup does not fail the app if the registry file is missing or malformed.

Definition of complete for Phase 1:

- The app has one durable machine-peer system and no later phase needs to invent a second one.
- Peer identity can survive restart and future discovery merge work.

## Phase 2: LAN Advertisement And Auto Discovery

- [x] Add `LanDiscoveryAdvertisementService`
  - [x] Advertise `_nexuscentral._tcp.local` when LAN control is enabled.
  - [x] Publish discovery-safe TXT metadata.
  - [x] Re-register when token settings or control-plane settings change.
  - [x] Publish a stable peer id that survives hostname changes.

- [x] Add `LanDiscoveryBrowserService`
  - [x] Browse for peers in the background.
  - [x] Normalize duplicate service announcements.
  - [x] Expire peers after TTL.
  - [x] Raise snapshot updates for the registry service.
  - [x] Filter self-discovery.
  - [x] Tolerate platforms or networks where browse start fails.
  - [x] Preserve enough browse state to explain `Scanning`, `Discovered`, and `DiscoveryUnavailable` in the UI.

- [x] Integrate discovery start/stop with app lifecycle
  - [x] Start discovery services during Desktop startup.
  - [x] Stop discovery services cleanly on app shutdown.
  - [x] Cancel timers and outstanding browse tasks cleanly.

Definition of complete for Phase 2:

- Discovery is real, self-safe, duplicate-safe, and optional rather than fragile.
- Router can become a true peer directory instead of a manual peer form.

## Phase 3: Pairing, Trust, And Authenticated Peer Connect

- [x] Define peer trust states
  - [x] `Discovered`
  - [x] `Trusted`
  - [x] `Connected`
  - [x] `AuthFailed`
  - [x] `Stale`

- [x] Add trust commands and flows
  - [x] Pair discovered peer with token.
  - [x] Promote manual peer into trusted peer registry.
  - [x] Forget trusted peer.
  - [x] Re-auth peer after token rotation.
  - [x] Preserve peer row and cached imported targets while auth is broken.
  - [x] Distinguish `never trusted`, `trusted`, `auth failed`, and `offline trusted` as separate operator-visible states.

- [x] Align existing manual connection flow with the registry
  - [x] Replace one-off manual peer creation with registry-backed peer creation.
  - [x] Keep manual connect as a fallback path inside the same peer system.
  - [x] Merge a previously manual peer with a later discovered peer instead of creating duplicates.

Definition of complete for Phase 3:

- Trust is durable and repairable.
- A peer can fail auth without disappearing from the product.

## Phase 4: Remote Inventory Import

- [x] Extend `ModelLibraryService` to import authenticated remote inventory
  - [x] Query remote status.
  - [x] Query remote runtime inventory.
  - [x] Query remote runtime roles if needed for richer context.
  - [x] Store imported targets under the owning peer.
  - [x] Mark status-only peers as partial import rather than fully connected.
  - [x] Cache enough imported-target metadata to keep degraded peer detail useful after refresh failure.

- [x] Replace one-peer-equals-one-target modeling
  - [x] Introduce imported remote route target ids.
  - [x] Preserve remote instance ids.
  - [x] Keep role assignment compatible with local and remote targets.
  - [x] Preserve existing role assignments when a peer goes offline or stale.
  - [x] Preserve imported target ownership by peer id so target identity can survive host and display-name changes.

- [x] Add periodic import refresh
  - [x] Fast refresh after successful pairing.
  - [x] Background refresh while peer is trusted.
  - [x] Stale imported targets when peer health degrades.
  - [x] Add retry backoff for repeated network failures.

Definition of complete for Phase 4:

- Remote inventory is modeled as actual imported route targets, not as one machine-wide placeholder target.
- Cached and partial import behavior is explicit.

## Phase 5: Split Router UI Shell

- [x] Refactor `RouterView.axaml` into a split-pane body patterned after `ModelsView.axaml`
  - [x] Keep header strip.
  - [x] Add left discovery/directory pane.
  - [x] Add right selected-peer detail pane.
  - [x] Preserve compact route-role editing where it still fits.
  - [x] Add explicit scanning, empty, auth-failed, stale, and offline states.

- [x] Extend `RouterViewModel`
  - [x] Add selected peer state.
  - [x] Add filtered peer collections.
  - [x] Add imported target projections.
  - [x] Add pair / forget / refresh commands.
  - [x] Add retry-auth and clear-error commands where needed.
  - [x] Add explicit projection flags for scanning, cached-only, partial-import, auth-failed, stale, and offline states.

- [x] Add child view model types
  - [x] `RouterPeerListItemViewModel`
  - [x] Selected-peer detail projection type or equivalent
  - [x] `RouterImportedTargetViewModel`
  - [x] Peer-filter projection or equivalent
  - [x] Include cached-data and partial-import projection flags.

Definition of complete for Phase 5:

- Router visually reflects the real service-owned peer and imported-target model.
- The operator can understand peer state and act on it without leaving the page.

## Phase 6: Role Assignment Against Imported Targets

- [x] Expand Router target selection to include imported remote targets
  - [x] Group by local versus peer.
  - [x] Include health and busy state in selection detail.
  - [x] Keep target ids stable across refreshes.
  - [x] Keep degraded assignments visible even when the target is currently unavailable.

- [x] Update runtime role snapshots if necessary
  - [x] Ensure the assigned target summary makes peer origin clear.
  - [x] Ensure remote assignments survive app restart through persisted registry/library state.
  - [x] Ensure unavailable imported targets stay blocked at runtime admission.

Definition of complete for Phase 6:

- Roles speak the same target language as runtime and Router.
- Degraded remote assignments remain visible but cannot execute unsafely.

## Phase 7: Seamless Routing Behavior

- [x] Add peer-aware auto-selection rules
  - [x] Prefer ready targets.
  - [x] Skip stale and auth-failed peers.
  - [x] Respect `MaxParallel` admission.
  - [x] Keep local-only fallback explicit if no peer is eligible.
  - [x] Never auto-route to untrusted discovered peers.

- [x] Add operator-facing status messages
  - [x] Explain why a discovered peer is not routable.
  - [x] Explain why a role points at a stale peer.
  - [x] Explain when imported inventory is outdated.
  - [x] Explain when the UI is showing cached peer data only.

Definition of complete for Phase 7:

- Router messaging and runtime behavior match.
- Operators can tell the difference between visible, trusted, healthy, and routable.

## Phase 8: Validation And Release Hardening

- [x] Add tests
  - [x] Peer registry serialization tests.
  - [x] Discovery merge tests.
  - [x] Imported target route resolution tests.
  - [x] Stale peer blocking tests.
  - [x] Self-discovery suppression tests.
  - [x] Partial-import and auth-failed retention tests.
  - [x] Restart continuity tests for trusted offline peers and cached imported targets.

- [ ] Run manual LAN validation
  - [ ] Two desktops discover each other automatically.
  - [ ] Manual peer fallback still works.
  - [ ] Token trust is required before remote routing.
  - [ ] Imported targets appear in the split UI.
  - [ ] Role assignments can target imported instances.
  - [ ] Stale peers become unavailable gracefully.
  - [ ] A previously trusted offline peer still renders from cached state.
  - [ ] A hostname or IP change does not duplicate the same peer.
  - [ ] A rotated token moves the peer into repairable auth-failed state.

- [x] Update top-level Desktop docs
  - [x] Link this plan folder from `Docs/Desktop/README.md`.
  - [x] Refresh `BREAKDOWN.md` after implementation lands.

Definition of complete for Phase 8:

- The feature survives restart, LAN churn, token rotation, stale transitions, and partial import.
- The docs describe the real behavior rather than the intended behavior.

Remaining hold:

- The real two-machine LAN validation checklist is still intentionally open until that proof is run on actual hardware.
