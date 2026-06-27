# Discovery Architecture

## Standard Selection

- [ ] Use mDNS / DNS-SD as the primary LAN discovery standard
  - [ ] Service name should be human-readable, for example `Nexus Central - <MachineName>`.
  - [ ] Service type should be `_nexuscentral._tcp.local`.
  - [ ] Port should be the existing control plane port from `LanRoutingSettingsService`.
  - [ ] TXT records should carry lightweight metadata only.

- [ ] Keep manual peer entry as a fallback standard
  - [ ] Discovery can fail on segmented networks, guest Wi-Fi, or locked-down enterprise LANs.
  - [ ] Manual pairing must remain a first-class fallback path.

## Required Edge-Case Handling

- [ ] Filter self-discovery reliably
  - [ ] A machine must not show its own advertisement as a routable peer.
  - [ ] Self-filtering should use a stable machine id, not only host comparison.

- [ ] Handle multiple network interfaces
  - [ ] A peer may advertise on Ethernet and Wi-Fi at the same time.
  - [ ] The browser should merge these into one canonical peer identity.

- [ ] Handle renamed hosts and changed IPs
  - [ ] Trust should follow the stable peer id.
  - [ ] Last known host and discovered host must be reconciled without creating duplicate peers.

- [ ] Handle missing mDNS support gracefully
  - [ ] Discovery browser failure should not break Router startup.
  - [ ] Manual peers and trusted peers should still load and route normally.

## Discovery Record Contract

- [ ] Publish the following TXT record fields
  - [ ] `api=v1`
  - [ ] `display_name=<machine display name>`
  - [ ] `instance_id=<stable machine id>`
  - [ ] `auth=token`
  - [ ] `router=1`
  - [ ] `control_port=<port>`
  - [ ] `version=<desktop app version>`

- [ ] Do not publish secrets in discovery
  - [ ] Never publish the shared token via mDNS.
  - [ ] Never publish detailed runtime inventory in TXT records.
  - [ ] Use discovery to find peers, then authenticated HTTP to inspect them.

- [ ] Publish a stable machine identity
  - [ ] Add a generated stable peer id stored locally under app state.
  - [ ] Do not rely on machine name alone because names can collide or change.

## Core Services To Add

- [ ] Add `LanDiscoveryAdvertisementService`
  - [ ] Own local mDNS advertisement.
  - [ ] Start only when LAN control is enabled.
  - [ ] Refresh advertisement when display name, control port, or LAN setting changes.

- [ ] Add `LanDiscoveryBrowserService`
  - [ ] Browse `_nexuscentral._tcp.local` continuously.
  - [ ] Raise snapshot events for discovered, updated, and expired peers.
  - [ ] Normalize duplicate service announcements into one peer identity.
  - [ ] Keep browse failures isolated so the rest of Desktop remains operational.

- [ ] Add `LanPeerRegistryService`
  - [ ] Merge manual peers, discovered peers, trusted peers, and paired peers into one canonical registry.
  - [ ] Persist trusted/manual peer decisions.
  - [ ] Surface a read-only snapshot model for the Router view model.
  - [ ] Keep cached imported inventory summaries available for offline display.

- [ ] Extend `ModelLibraryService`
  - [ ] Import authenticated remote inventory from trusted peers.
  - [ ] Represent imported remote targets as distinct route targets.
  - [ ] Keep role assignment and runtime route resolution centered here.
  - [ ] Preserve degraded imported targets when a peer goes offline so existing assignments stay visible.

## Persistence Structures

- [ ] Extend `AppPathsService`
  - [ ] Add `LanPeerRegistryPath`.
  - [ ] Add `LanDiscoveryCachePath` if a cache is desired for fast startup.

- [ ] Persist a trusted peer registry document
  - [ ] Stable peer id.
  - [ ] Last known host and control plane port.
  - [ ] Display name.
  - [ ] Trust state.
  - [ ] Shared token or token reference.
  - [ ] Last successful import time.
  - [ ] Last seen discovery timestamp.
  - [ ] Last auth failure reason if present.
  - [ ] Last imported inventory summary for offline UI continuity.

- [ ] Keep ephemeral discovery separate from trust state
  - [ ] Discovery events are transient.
  - [ ] Pairing and trust decisions are durable.
  - [ ] The service layer should merge both into one presentation snapshot.
  - [ ] Cached imported targets should be clearly marked as cached when the peer is offline.

## Canonical Peer Model

- [ ] Distinguish machine peers from route targets
  - [ ] One machine peer can expose many imported route targets.
  - [ ] A peer is the trust and control entity.
  - [ ] A route target is the runnable endpoint for routing.

- [ ] Add explicit peer states
  - [ ] `Discovered`
  - [ ] `Trusted`
  - [ ] `Connecting`
  - [ ] `Connected`
  - [ ] `AuthFailed`
  - [ ] `Stale`
  - [ ] `Offline`
  - [ ] `PartialImport`

- [ ] Add explicit imported target states
  - [ ] `Ready`
  - [ ] `Busy`
  - [ ] `Unavailable`
  - [ ] `Stale`
  - [ ] `Cached`

## Remote Inventory Import

- [ ] Reuse the authenticated control plane rather than inventing a second protocol
  - [ ] Query `/nexuscentral/status` first.
  - [ ] Query runtime inventory and runtime role endpoints only after auth succeeds.
  - [ ] Cache imported targets per peer.
  - [ ] Treat status success plus inventory failure as partial import, not total peer failure.

- [ ] Model imported targets as first-class router entries
  - [ ] Use a stable compound id, for example `<peer-id>/<remote-instance-id>`.
  - [ ] Preserve the remote instance id from the peer machine.
  - [ ] Preserve capability, mode, health, and role relationship metadata.
  - [ ] Preserve the owning peer id on every imported target so UI and route diagnostics can explain provenance.

- [ ] Refresh imported inventory on a background schedule
  - [ ] Fast refresh on first pairing.
  - [ ] Moderate refresh interval while connected.
  - [ ] Degrade to stale if control plane or inventory stops responding.
  - [ ] Add backoff after repeated network failures so the app does not spam the LAN.
  - [ ] Cancel outstanding refresh work cleanly during app shutdown.

## Hardening Decisions

- [ ] Token rotation handling
  - [ ] If a previously trusted peer starts failing auth, move it to `AuthFailed` instead of deleting it.
  - [ ] Preserve cached targets and assignments while blocking new routing through that peer.

- [ ] Duplicate peer resolution
  - [ ] If the same stable peer id appears from discovery and manual entry, merge records into one canonical peer.
  - [ ] Prefer live discovered endpoint information over stale manual host data, but preserve manual trust metadata.

- [ ] Offline startup behavior
  - [ ] Trusted peers should still populate the Router list from local state on app launch.
  - [ ] Discovery and import refresh should progressively upgrade cached state to live state when the network becomes available.

## How This Fits The Existing Code

- [ ] `LanRoutingSettingsService` remains the owner of local LAN exposure and shared token.
- [ ] `IntegrationServer` remains the authenticated control-plane server.
- [ ] `ModelLibraryService` remains the owner of routeable targets and role resolution.
- [ ] `RouterViewModel` remains a projection/orchestration view model, not a networking layer.

## File Impact

- [ ] Add new services under `Desktop/Services`
  - [ ] `LanDiscoveryAdvertisementService.cs`
  - [ ] `LanDiscoveryBrowserService.cs`
  - [ ] `LanPeerRegistryService.cs`

- [ ] Extend `Desktop/Services/AppServices.cs`
  - [ ] Register discovery and peer registry services.

- [ ] Extend `Desktop/Services/AppPathsService.cs`
  - [ ] Add new state file paths.

- [ ] Extend `Desktop/Services/IntegrationServer.cs`
  - [ ] Expose versioned discovery-safe peer metadata if needed.

- [ ] Extend `Desktop/Services/ModelLibraryService.cs`
  - [ ] Import and manage remote machine inventory.