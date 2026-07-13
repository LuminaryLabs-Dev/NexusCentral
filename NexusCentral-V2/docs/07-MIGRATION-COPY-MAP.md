# Migration Copy Map

## Rule

Copy proven behavior and contracts. Do not copy current coupling. Every moved
module must receive one V2 owner: DSK, adapter, host, shared UI, fixture, or
legacy-only.

## Electron Source Map

| Current source | V2 destination | Treatment |
|---|---|---|
| `src/index.html` | `apps/web` | Port layout and semantics; do not retain direct IPC assumptions |
| `src/styles.css` | `apps/web` | Copy current visual tokens and calm IDE styling |
| `src/renderer.mjs` | `apps/web` + `central-client` | Rewrite orchestration against CentralClient; retain interaction behavior |
| `src/workflow-contract.js` | `packages/contracts` | Convert to ESM typed workflow contract |
| `src/domain-kits/identity-kit.mjs` | `apps/web/ui` | Keep as UI identity module, not a DSK |
| `src/domain-kits/particle-field-kit.mjs` | `apps/web/ui` | Optional visual module; preserve reduced density |
| `src/domain-kits/surface-copy-kit.mjs` | `apps/web/ui` | Keep outcome copy outside domain state |
| `src/harnesses/coding-harness.mjs` | harness DSK + process adapter | Preserve stage prompts; extract manifest and state |
| `src/services/router-service.js` | providers DSK + routing DSK + HTTP adapter | Split profile/selection/health from transport and secrets |
| `src/services/models-service.js` | model catalog/runtime DSKs + adapters | Split manifests/lifecycle from LM Studio and Hugging Face I/O |
| `src/services/mesh-routing-service.js` | membership/policy/routing DSKs + network/crypto adapters | Preserve protocol proof; remove host and policy coupling |
| `src/main.js` | composition + host adapters | Mine behavior; never copy wholesale |
| `src/preload.js` | Electron CentralClient transport | Replace feature channels with client contract |
| `sidecars/rust-proof` | `adapters/native` | Keep only as native boundary proof until a real worker exists |
| `scripts/check.js` | workspace checks | Preserve useful invariants and add graph validation |

## Persistent State Migration

Migration must detect and preserve:

```text
router/registry.json
router encrypted secrets
mesh/config.json
mesh/identity.json
mesh encrypted LAN code
lab-loop/latest.json
lab-loop run artifacts
selected workspace preference when available
```

Private keys and encrypted secrets must be migrated through the original secure
storage context. Never copy decrypted values into JSON, logs, tests, or DSK
snapshots.

## V1 Map

Use `NexusCentral-V1` as behavioral reference for service truth, workflow
semantics, tests, and proof expectations. Do not copy Avalonia views or
view-model architecture into the shared web UI.

## Copy First

- workflow names and user outcomes;
- provider types and normalized profile fields;
- 16K model loading requirement;
- Review -> Act -> Audit stage semantics;
- mesh identity and encrypted-request proof behavior;
- workspace safety boundaries;
- existing validation fixtures and expected states;
- visual tokens and simplified interaction layout.

## Leave Behind

```text
node_modules/
dist/ and stale staged builds
generated screenshots
placeholder readiness copy
static fake service state
duplicated Electron-only renderer orchestration
Avalonia UI code
deprecated V0 structures
unscoped remote execution surfaces
```

## Compatibility Strategy

Use a strangler migration:

```text
current Electron call
-> compatibility adapter
-> new DSK API
-> existing host implementation
```

Remove a legacy path only after the corresponding promotion gate passes.
