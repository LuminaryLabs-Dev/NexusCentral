# Step 04: Router Truth

## One Sentence

Replace fake Router readiness with real inactive/available route state from a minimal router service.

## State Observes

- Router is the highest fake-readiness risk.
- Router UI claims static `Available`, `Ready`, queue counts, endpoint counts, capacity, and next route.
- No Router service currently owns route state.
- `Core/Execution` has route/provider record shapes but no queue, router, provider registry, cancellation path, or health checks.
- Shell status says route/model/queue are inactive while Router tab copy implies readiness.

## Exact Changes To Plan

- Add minimal Router state model:
  - router id
  - router type
  - enabled/disabled
  - target kind
  - health
  - queue depth
  - capacity
  - last check
- Add minimal Router service:
  - list routers
  - select router
  - add local router profile
  - remove router profile
  - report inactive when no backed endpoints exist
- Bind Router UI to service state.
- Remove static `Available` and `Ready` text unless produced by service.
- Show planned provider/peer rows as inactive until backed.
- Keep routing algorithm simple:
  - local queue first
  - provider endpoints later
  - trusted NexusCentral peer queue later

## Hero Controls / User-Facing Impact

- Add router profile.
- Select router profile.
- View actual inactive/available state.
- See queue availability only when backed.

## Advanced Controls / Deferred

- No broad peer bidding.
- No multi-reply routing.
- No provider endpoint secrets in route targets.
- No remote NexusCentral handoff until local queue ownership exists.

## Validation

- Router tab with no service data shows inactive/planned, not ready.
- Adding local router profile changes UI through service state.
- Shell route status matches selected router state.
- Build and tests pass.

## Done Criteria

- Router never claims ready without service state.
- Static fake capacity/provider counts are gone.
- Router can be trusted as a truthful UI surface.
