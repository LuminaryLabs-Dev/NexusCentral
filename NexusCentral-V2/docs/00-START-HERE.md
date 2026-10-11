# Start Here

## Current UI and workflow contract

The October 11 workbench decision supersedes earlier Lab-first navigation and
visual rules. Read `17-AUTOMATION-WORKBENCH.md` first for implemented scope and
remaining roadmap, then retain the underlying ownership/security contracts below.

## Authority

These documents are the V2 implementation contract. When chat history, old screenshots, stale build output, or older plans conflict with this folder, this folder wins unless the user explicitly changes the goal.

NexusEngine itself remains an external read-only dependency. V2 follows the current NexusEngine rules:

- bootstrap remains small;
- DSKs own reusable meaning, state, services, snapshots, and reset behavior;
- hosts own platform lifecycle and I/O;
- UI presents domain state without owning it;
- domain APIs are addressable through `engine.n.*`.

Primary upstream references:

- https://github.com/LuminaryLabs-Dev/NexusEngine/blob/main/docs/CURRENT-ARCHITECTURE.md
- https://github.com/LuminaryLabs-Dev/NexusEngine/blob/main/docs/ideal/dsk.md
- https://github.com/LuminaryLabs-Dev/NexusEngine/blob/main/docs/DOMAIN-ADDRESSABILITY.md
- https://github.com/LuminaryLabs-Dev/NexusEngine/blob/main/docs/protokit-boundaries.md

Pin an exact compatible NexusEngine package version or commit before runtime implementation begins. Do not float against `main` during a proof phase.

## Reading Order

1. `01-PRODUCT-INTENT.md`
2. `02-TARGET-ARCHITECTURE.md`
3. `03-DOMAIN-SERVICE-KIT-GRAPH.md`
4. `04-ADAPTER-HOST-BOUNDARIES.md`
5. `05-CENTRAL-CLIENT-AND-UI.md`
6. `06-MESH-LAN-WEB-ONNX.md`
7. `07-MIGRATION-COPY-MAP.md`
8. `08-IMPLEMENTATION-SEQUENCE.md`
9. `09-VALIDATION-PROMOTION-GATES.md`
10. `10-SECURITY-NONNEGOTIABLES.md`
11. `11-SIMULATOR-PROOF.md`
12. `12-IMPLEMENTATION-STATUS.md`
13. `13-HARNESS-EXPERIMENTS.md`
14. `14-PROVIDERS-AND-PEERJS-ROOMS.md`
15. `15-DESKTOP-DEPLOYMENT.md`
16. `16-LEGACY-LANE-AUDIT.md`

## Source Hierarchy

```text
V2 docs and goal
-> live NexusEngine DSK contract
-> current NexusCentral source behavior
-> V1 tests and proof artifacts
-> older plans and screenshots
```

## Completion Rule

V2 is complete only when the shared UI, Electron shell, headless node, LAN host, mesh, provider/model routing, harness execution, proof review, persistence migration, and packaging gates all have executable evidence.

For desktop deployment specifically, a generated package is not enough: each supported operating-system/architecture target must launch the embedded Nexus Node and pass the packaged `/api/v1/capabilities` smoke gate on its native CI runner.
