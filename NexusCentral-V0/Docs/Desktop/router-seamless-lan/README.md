# Router Seamless LAN Plan

## Current Implementation Status

As of 2026-03-29, the Desktop app now implements the core seamless-LAN architecture described in this folder:

- `LanRoutingSettingsService` persists stable local peer identity, LAN exposure state, and the shared secret.
- `LanPeerRegistryService` owns canonical machine peers, trust state, discovery merge, cached import summaries, and degraded peer presentation states.
- `LanDiscoveryAdvertisementService` and `LanDiscoveryBrowserService` provide LAN advertisement, peer browse, duplicate suppression, cache recovery, and graceful discovery failure handling.
- `ModelLibraryService` imports remote runtime inventory and roles as peer-owned imported route targets, preserves cached targets on partial failure, and resolves routing through stable imported target ids.
- `RouterViewModel` and `RouterView.axaml` now present Router as a split peer directory plus selected-peer workspace with imported targets, trust actions, and route-role editing.
- `RuntimeService` and `RoutingRequestTelemetryService` continue to own runtime admission and `MaxParallel` enforcement for imported targets.

The remaining closure item from this plan folder is not local code implementation. It is the final two-machine LAN proof plus any doc touch-ups needed after that proof.

## Purpose

This folder defines the master implementation plan for turning the Router tab into a seamless multi-PC LAN surface with:

- a split-pane UI patterned after the Models tab,
- automatic Nexus Central discovery on the local network,
- trusted peer pairing and imported remote inventory,
- MVVM-safe service and state boundaries,
- phased implementation checklists that can be completed one slice at a time.

## Final Goal

The final goal is a Router that behaves like a real LAN routing workspace rather than a manual remote-connect helper.

That means:

- peers appear automatically on the LAN,
- trust is explicit and durable,
- imported remote runtime instances become real route targets,
- degraded peers remain visible with repairable state,
- and runtime dispatch only uses targets that are trusted, healthy, and admitted.

## What Readers Should Use In This Folder

- Use [05-master-implementation-sequence.md](./05-master-implementation-sequence.md) for the exact end-state, architectural rules, and execution order.
- Use [04-master-checklist.md](./04-master-checklist.md) for concrete deliverables and closure checks per phase.
- Use [06-two-machine-proof-runbook.md](./06-two-machine-proof-runbook.md) for the final real-LAN validation pass on two physical desktops.
- Use [01-target-split-ui.md](./01-target-split-ui.md) for the final Router page contract and operator-facing UX outcome.
- Use [02-discovery-architecture.md](./02-discovery-architecture.md) and [03-implementation-standards.md](./03-implementation-standards.md) for service-boundary and implementation constraints.

## Target Outcome

- The Router tab should feel like a peer directory and orchestration console, not a manual host-and-port form.
- Nearby Nexus Central machines should appear automatically when LAN control is enabled.
- Operators should be able to trust, inspect, assign, and route through discovered peers without retyping connection details.
- Remote machines should expose imported runtime inventory so routing decisions operate on real remote targets instead of a single placeholder peer row.
- The experience should remain understandable when discovery fails, auth breaks, a peer goes stale, or a remote inventory import is only partially available.

## Standards Chosen

- Discovery standard: mDNS/DNS-SD on the local network.
- Discovery service type: `_nexuscentral._tcp.local`.
- Control plane: authenticated HTTP on the existing Nexus Central control plane port.
- Trust model: explicit token-based pairing, never automatic trust.
- MVVM structure: Views bind to view models only; network, timers, persistence, and pairing logic live in services.
- State location: all mutable discovery and trust state must live under `AppPathsService.StateRootDirectory`.

## Hardened Experience Rules

- Discovery is advisory, not authoritative.
	- A discovered peer can be shown immediately.
	- A discovered peer cannot be routed until trust and authenticated inspection succeed.

- Trust is durable, network presence is not.
	- Trusted peers should remain visible across restarts.
	- Offline peers should remain visible with explicit offline or stale state rather than disappearing.

- Imported inventory is allowed to be partial.
	- If status succeeds but runtime inventory fails, the peer remains visible.
	- The Router should explain that inspection is incomplete instead of treating the peer as fully healthy.

- Secrets never ride the discovery layer.
	- Tokens stay in local trusted state only.
	- Discovery records publish only enough metadata to find the control plane and identify the machine.

## Recommended Delivery Order

- [x] Phase 1: Discovery foundation and persistence
- [x] Phase 2: Remote inventory import and canonical peer model
- [x] Phase 3: Split Router UI patterned after Models
- [x] Phase 4: Seamless pairing, health, and role assignment flows
- [ ] Phase 5: Routing automation, validation, and operator polish

## Implemented Foundation Slice

- Stable local peer identity now persists through `LanRoutingSettingsService`.
- App-state paths now include dedicated LAN peer registry and discovery-cache locations.
- `LanPeerRegistryService` now persists trusted or manual peer records, cached import summary data, auth-failure state, and offline-visible peer metadata.
- Manual remote connect and refresh now flow through the peer registry, adopt observed stable peer ids, and block self-routing when the remote probe resolves back to the local machine.
- Startup continuity is covered by tests for stable local peer identity, malformed peer-registry recovery, and peer-registry reload behavior.

## Current Limits

- The final two-machine LAN proof is still pending and must be run on real desktops before the feature is considered operationally frozen.
- Discovery and remote refresh now back off after repeated failure, but the exact operator polish around that behavior should still be verified on a noisy real LAN.
- The docs in this folder now describe the implemented architecture, but should be refreshed one more time after the real-LAN proof so screenshots and operator messaging stay exact.

## Document Map

- [01-target-split-ui.md](./01-target-split-ui.md): target UX, split layout, and view model composition.
- [02-discovery-architecture.md](./02-discovery-architecture.md): discovery protocol, service boundaries, persistence, and remote inventory import.
- [03-implementation-standards.md](./03-implementation-standards.md): MVVM, state, networking, status, and testing standards.
- [04-master-checklist.md](./04-master-checklist.md): step-by-step implementation checklist with file-level impact notes.
- [05-master-implementation-sequence.md](./05-master-implementation-sequence.md): execution-ordered master plan that closes the remaining routing gaps without breaking ownership boundaries.
- [06-two-machine-proof-runbook.md](./06-two-machine-proof-runbook.md): hardware validation runbook for discovery, trust, imported targets, runtime blocking, repair, and restart continuity.
