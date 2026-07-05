# NexusCentral V1 Architecture

NexusCentral V1 is a rebuild, not a port of NexusCentral V2. V2 is only a behavior reference for product capabilities, workflows, and user expectations.

## Boundaries

- `Desktop/` contains the runnable Avalonia desktop host.
- `Core/` contains shared platform services and contracts used across features.
- `Features/` contains user-facing product modules.
- `Integrations/Rpc/` contains infrastructure APIs and must not appear as a primary navigation tab.
- `Integrations/Providers/` contains future provider adapters for external model endpoints.

## Current Slice

The first production-capable V1 slice is `Lab + Workspaces`. It is implemented with V1-native contracts and services, not copied V2 service or view-model shapes.

- `Core/Config`, `Core/Logging`, and `Core/State` provide local file-backed foundations.
- `Core/Execution` provides the minimal event model needed for inline Lab tool steps.
- `Features/Workspaces` owns workspace definitions, sessions, bounded file mutation, and browse trees.
- `Features/Lab` owns compact chat transcript state, chat-owned uploads, and placeholder execution step rendering.
- Files can be browsed from the Lab left rail, but file additions enter through the chat upload flow.

## User Surfaces

The V1 shell exposes these primary surfaces:

- Lab
- Automations
- Agents
- Router
- Models
- Settings

V1 intentionally does not expose a standalone Dashboard tab. Shared status belongs in the shell dock; each tab owns its own content.

`Router` is the request-routing surface for local endpoints, provider endpoints, trusted NexusCentral queues, and visible peer/device route availability. `Devices` remains a feature-module boundary, but it is not a primary navigation tab unless explicitly restored. `Web Lab` is not a primary tab; Lab owns the action that opens the Web Lab app.

## Exclusions

- V2 source files are not copied into V1.
- V2 view-model shapes are not ported directly.
- Generated artifacts, duplicate V2 Web lanes, build outputs, and old temp files are excluded.
- The old standalone Workflow page is not restored.
- Web Lab is not restored as a primary tab.
- Ticketing is not part of the current V1 scope.
- `Integrations/Rpc` is infrastructure only, not a primary navigation surface.

## Future Runtime Boundary

Runtime execution is a future boundary outside this first scaffold. The intended shape is a local `Core/Execution` job queue that can use local provider endpoints first and route overflow work to trusted NexusCentral peer queues. Remote NexusCentral peers host temporary execution sessions, while the local NexusCentral remains the source of truth for the user conversation, job state, history, and cancellation.
