# NexusCentral Electron

## Purpose

`NexusCentral-Electron` is the cutover proof lane for a web-first NexusCentral shell that can also package as desktop on macOS, Windows, and Linux.

The current workflow is:

```text
Lab -> Router -> Models -> Review -> Settings
```

Avalonia V1 remains the stable checkpoint until this lane proves local model calls, Conversation Harness execution, workspace browsing, validation proof review, Linux packaging, and Rust sidecar execution.

## Commands

```text
npm run check
npm run start
npm run build
npm run sidecar:check
```

`npm run check` uses only Node.js and does not require Electron to be installed. `npm run start` requires dependencies from `npm install`.

## Desktop-Only Capabilities

The browser-safe UI lives in `src/index.html`, `src/styles.css`, `src/renderer.mjs`, and small MJS domain kits under `src/domain-kits/`.

Electron-only powers are exposed through `src/preload.js` and handled in `src/main.js`:

```text
renderer -> preload bridge -> Electron main -> local adapters
```

Current adapter proofs:

- app status
- LM Studio `/v1/models` probe
- Conversation Harness command metadata
- workspace root listing
- Rust sidecar hello-world

The Lab now has a small coding harness loop. An explicit `Run Lab` action runs
Review in read-only mode, Act with workspace-scoped write access, and Audit in
read-only mode. Runs are persisted under the Electron user-data directory and
never commit, push, deploy, or write outside the selected workspace.

Router owns persisted provider profiles for OpenRouter, OpenAI, Google Gemini,
NVIDIA NIM, LM Studio, and custom OpenAI-compatible endpoints. Provider API
keys are encrypted through Electron secure storage. Models separately owns LM
Studio search, download, load, unload, and local-provider publication.

Settings also owns the LAN mesh connection. Computers using the same LAN code
discover each other automatically over local multicast. The code is encrypted
at rest and used to authenticate and encrypt peer requests; it is not announced
on the network. Every receiving computer retains a local action allow-list, so
joining a mesh does not grant remote shell, workspace, model, or harness access.

The first callable mesh actions are intentionally limited to authenticated ping
and capability discovery. See `docs/lan-mesh-routing.md` before exposing work
execution.

## Cutover Gate

Do not retire `NexusCentral-V1` until this lane proves:

- macOS startup
- Linux package target
- LM Studio call
- Conversation Harness turn
- workspace file browsing
- validation proof review
- Rust sidecar execution
