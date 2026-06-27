# Target Split UI

## Goal

Rework the Router tab so it follows the same structural idea as the Models tab:

- left pane = browsable list and intake surface,
- right pane = detail and management surface for the selected item.

The queue and top-level routing summary can stay as a header strip above the split body, but peer discovery and peer management should move into a true split workspace.

## Final UX Goal

The final Router should feel like a live peer and target operations console.

When a user opens the page, they should be able to understand four things immediately:

- what peer machines exist,
- which ones are trusted and usable,
- what runtime targets the selected peer exposes,
- and whether routing will work right now.

The page should no longer feel like a place where the operator types an IP address and hopes the route works. It should feel like a stable workspace that reflects the actual LAN state, actual trust state, and actual runtime eligibility.

## Final Page Contract

When the feature is complete, the Router page must provide all of the following without forcing the user into logs or hidden dialogs:

- peer discovery state,
- peer trust state,
- peer auth repair actions,
- imported remote target inspection,
- role assignment context,
- route readiness and blocked reasons,
- queue visibility,
- and last-known cached state when the LAN is degraded.

## Target Layout

- [ ] Keep the existing top Router status strip
  - [ ] Keep queue summary and recent request summary at the top.
  - [ ] Keep request queue slide panel as a secondary surface, not the primary layout model.
  - [ ] Keep route load telemetry visible at the top so the page still feels operational.

- [ ] Convert the main body to a two-column split similar to `ModelsView.axaml`
  - [ ] Left column should be a Peer Directory pane.
  - [ ] Right column should be a Peer Detail and Routing pane.
  - [ ] Use stable column ratios rather than stacked sections so the page remains scannable on desktop.

- [ ] Define first-load and empty-state behavior
  - [ ] If discovery has not yet returned anything, show a scanning state instead of an empty error state.
  - [ ] If no peers are found, explain that discovery may be blocked and manual add is still available.
  - [ ] If LAN control is disabled locally, explain why discovery and advertisement are limited.

## Left Pane: Peer Directory

- [ ] Add a discovery toolbar
  - [ ] Discovery status text.
  - [ ] Search/filter box.
  - [ ] Toggle for `Discovered`, `Trusted`, `Connected`, `Stale`, and `Manual` peer filters.
  - [ ] Button for `Add Manual Peer` as the fallback path.

- [ ] Add a discovered peer list
  - [ ] Each row should show display name, host, trust state, health state, and imported target count.
  - [ ] Each row should show whether LAN control is enabled and whether auth has succeeded.
  - [ ] Each row should show stale or auth-failed state without requiring the detail pane to explain it.
  - [ ] Each row should show whether the peer is discovered, trusted-only, connected, stale, or offline.
  - [ ] Each row should preserve a stable sort order so peers do not jump around during refresh unless their operator relevance changes.
  - [ ] Each row should show whether data is live or cached-only.
  - [ ] Each row should not disappear just because a previously trusted peer is currently offline.

- [ ] Add a lightweight intake drawer inside the left pane
  - [ ] Manual host entry remains available.
  - [ ] Manual token entry remains available.
  - [ ] Manual peers should land in the same directory list as discovered peers, not in a separate disconnected section.
  - [ ] If a manual peer is later discovered automatically, the UI should merge them into one peer row instead of duplicating them.

## Right Pane: Selected Peer Workspace

- [ ] Show selected peer summary
  - [ ] Display name, host, advertised control plane URL, trust state, and health state.
  - [ ] Display last seen / stale timing.
  - [ ] Display imported runtime inventory counts.
  - [ ] Display inspection completeness, for example full inventory loaded versus status-only.
  - [ ] Display whether the current view is live data or cached fallback data.

- [ ] Add peer trust and connection controls
  - [ ] Pair / trust action.
  - [ ] Rotate token guidance for the local machine.
  - [ ] Remove peer / forget trust action.
  - [ ] Refresh peer action.
  - [ ] Re-enter token action when a trusted peer becomes auth-failed after token rotation.

- [ ] Add imported target list
  - [ ] Show each imported remote target as its own routeable row.
  - [ ] Include mode, health, role assignments, active requests, and allow-routing state.
  - [ ] Do not collapse an entire machine into a single generic route target.
  - [ ] If import is partial, show any usable targets while making the degraded state explicit.
  - [ ] Show blocked reason when a target is visible but not currently routable.

- [ ] Add role assignment workspace
  - [ ] Route roles should bind against the combined local and imported target catalog.
  - [ ] Role cards can stay horizontally scrollable or move to a vertical stack in the right pane.
  - [ ] The selected peer detail area should make it obvious which roles point at that peer's imported targets.
  - [ ] If a role points at a stale or offline imported target, show the assignment but mark it degraded rather than silently clearing it.

## Required User Flows

- [ ] First discovery flow
  - [ ] User opens Router and immediately sees that scanning is active.
  - [ ] User sees a discovered machine row with no trust yet.
  - [ ] User can trust it without leaving the selected peer workspace.
  - [ ] After trust succeeds, the same peer row transitions into inspected imported-target state rather than being replaced by a second row.

- [ ] Failure recovery flow
  - [ ] User sees `AuthFailed` and is given a direct token re-entry action.
  - [ ] User sees `Offline` or `Stale` and can still inspect cached peer details.
  - [ ] User can forget a peer even while it is offline.
  - [ ] User can tell whether routing is blocked by trust, health, stale state, or target admission.

- [ ] Restart continuity flow
  - [ ] Trusted peers remain listed on next app launch.
  - [ ] Cached last-seen and imported-target summary are visible until live refresh arrives.
  - [ ] The UI distinguishes cached data from live connected data.

## Final States The UI Must Render Explicitly

The final Router UI should not compress these into vague generic badges. Each state changes what the operator can do and what the page should say.

- `Scanning`: discovery is still running and nothing usable has been found yet.
- `DiscoveredUntrusted`: the machine is visible but cannot be used until trust succeeds.
- `TrustedInspecting`: trust exists and the app is loading or refreshing remote inventory.
- `Connected`: peer and imported targets are live and routable subject to target health and admission.
- `PartialImport`: peer status is known but imported target data is incomplete.
- `Stale`: last live contact is old enough that routing should be blocked until refresh succeeds.
- `Offline`: the peer is not reachable right now but cached details still exist.
- `AuthFailed`: the stored token is wrong or outdated and must be repaired.
- `CachedOnly`: the view is showing last known peer or target data because live inspection is not currently available.

## Final Operator Questions The Page Must Answer

By the time this work is complete, a user should be able to answer these questions directly from Router:

- Which machines on my LAN can potentially help route work?
- Which of those machines have I explicitly trusted?
- Which of those machines are healthy enough to use right now?
- Which imported runtime targets exist on the selected machine?
- Which roles point at those targets?
- If routing would fail right now, why exactly would it fail?

## View Model Structure

- [ ] Keep `RouterViewModel` as the shell view model
  - [ ] Own selected peer state.
  - [ ] Own top-level discovery filters and commands.
  - [ ] Own high-level dashboard status strings.

- [ ] Introduce list and detail child view models
  - [ ] `RouterPeerListItemViewModel`
  - [ ] `RouterPeerDetailViewModel`
  - [ ] `RouterImportedTargetViewModel`
  - [ ] `RouterDiscoveryFilterOptionViewModel`

- [ ] Keep service calls out of XAML code-behind
  - [ ] Discovery browsing belongs in services.
  - [ ] Pairing and trust persistence belongs in services.
  - [ ] Imported inventory refresh belongs in services or `ModelLibraryService` helpers.

## File Impact

- [ ] Update `Desktop/Views/RouterView.axaml`
  - [ ] Replace the vertical stack body with a split-pane layout.
  - [ ] Retain the queue summary strip and role section behavior where still useful.

- [ ] Update `Desktop/ViewModels/RouterViewModel.cs`
  - [ ] Add selected-peer state.
  - [ ] Add discovery filter state.
  - [ ] Add left-pane list projections and right-pane detail projections.

- [ ] Add new peer-focused view model types
  - [ ] Keep them simple projection models backed by service snapshots.
  - [ ] Avoid putting HTTP or file IO into any of them.
  - [ ] Include explicit flags for cached data, partial import, auth-failed state, and stale state so the view does not infer these heuristically.