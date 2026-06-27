# Desktop Module - BREAKDOWN

## Current State

- Completed: UI bootstraps via Program.cs and App.axaml.cs.
- AppServices provides singletons for all core services.
- IntegrationServer handles routes for status, workflows, runtime, model, and chat.
- NexusWebLabService manages local frontend/backend processes, health checks, and open-browser.
- RuntimeService spawns runtime binary; dependencies on Runtime folder.
- LAN routing now persists a stable local peer id in `LanRoutingSettingsService` and a canonical machine-peer registry via `LanPeerRegistryService` under the app state root.
- LAN discovery is implemented through `LanDiscoveryAdvertisementService` and `LanDiscoveryBrowserService`, including discovery cache recovery, self-suppression, duplicate merge, and graceful failure handling.
- Manual and discovered peers now converge into the same peer system, preserve trust and cached import metadata, and expose degraded states such as `AuthFailed`, `PartialImport`, `Stale`, `Offline`, and `CachedOnly`.
- `ModelLibraryService` now imports remote runtime inventory and roles as peer-owned imported route targets with stable ids, cached fallback, and periodic background refresh.
- Router has been refactored into a split peer-directory plus selected-peer workspace with trust actions, imported target detail, and role assignment against local plus imported targets.
- Runtime admission still blocks degraded imported targets and enforces `MaxParallel` through `RoutingRequestTelemetryService`.
- Test coverage now includes discovery merge, discovery-cache recovery, imported target persistence, auth-failure repair retention, self-suppression, and offline imported-target blocking.

## Bug / Interaction Audit

- IntegrationServer still exposes broad local API surface area; non-loopback LAN control-plane access now requires `X-NexusCentral-Token`, but wider hardening should still tighten exposed cross-origin and endpoint behavior over time.
- Lack of API rate limit or per-tool guard; internal endpoints are unprotected.
- NexusWebLabService may fail in containerized headless environments (`xhost`, browser open etc.).
- Launcher platform coverage is currently macOS/Windows only (no Linux target in csproj).
- RuntimeService error handling may swallow process stderr details in some branches.

## Routing Master Plan

- Canonical execution sequence: [router-seamless-lan/05-master-implementation-sequence.md](router-seamless-lan/05-master-implementation-sequence.md)
- Core objective: maintain the move from manual peer forwarding to a real LAN routing system with discovery, canonical peer identity, imported remote targets, role-safe dispatch, and a split Router workspace.
- Most code slices in that plan are now implemented locally; the remaining open closure item is the two-machine LAN proof on real hardware.
- Architectural boundary to preserve: peer identity lives in `LanPeerRegistryService`, route targets live in `ModelLibraryService`, dispatch safety lives in `RuntimeService`, and `RouterViewModel` only projects the resulting state.
- The feature is only considered operational after a full two-machine proof passes and the Router docs match the implementation exactly.

## Cross-module Interaction Matrix

- Desktop->Runtime: RuntimeService (process + lifecycle), model load commands.
- Desktop->Interface: IntegrationServer provides the service API. CLI `nci` uses this.
- Desktop->Web: NexusWebLabService starts Web backend and frontend with IntegrationServer link.
- Desktop->Tests: AppPathsService and AppStateService are unit-tested and represented.
- Desktop->Scripts: publish path and workspace cleanup checks.

---
