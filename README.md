# NexusCentral

NexusCentral V2 is the canonical active implementation on `main`. It is a local-first AI control plane built around a durable headless Nexus Node, a shared Next.js interface, and a thin Electron desktop shell that packages the Node runtime with the app.

## Automation Workbench

The primary V2 UI now uses Home, Workflows, Apps, and Activity. The first complete
workflow converts task records into an operations report and a human review step.
Read [current workbench coverage](NexusCentral-V2/docs/17-AUTOMATION-WORKBENCH.md)
before treating the full integration roadmap as delivered. Linux built-in workers
are validated; external agents, live Luminary connectors, and cross-platform
workflow isolation remain pending.

## Active Product

```text
NexusCentral-V2
|-- Nexus Node          durable local service and source of truth
|-- Next.js Web         shared desktop/LAN browser interface
|-- Electron            packaged desktop host
|-- CentralClient       direct / IPC / HTTP + WebSocket boundary
|-- SQLite WAL          local state, jobs, events, recovery
|-- Router + Models     local/cloud provider and model lifecycle
|-- Harnesses           bounded coding/review execution
|-- libp2p + PeerJS     trusted LAN and remote model routing
`-- Packaging           macOS, Windows, Linux targets
```

Run locally with Node.js 24 or newer:

```bash
cd NexusCentral-V2
npm ci
npm run build
npm run dev
```

Open `http://127.0.0.1:47800`, or launch the Electron shell with:

```bash
npm run start:electron
```

## Desktop Packaging

V2 desktop packaging takes an explicit Electron platform and architecture:

```bash
npm run package:desktop -- darwin arm64
npm run package:desktop -- darwin x64
npm run package:desktop -- win32 x64
npm run package:desktop -- linux x64
```

The packaged Electron executable starts the embedded Nexus Node when required. Users do not need a separate Node.js installation for the packaged app.

Cross-platform targets and native CI are defined in `updates/nexuscentral-v2-targets.json` and `.github/workflows/nexuscentral-v2-build.yml`. Tagged V2 releases are handled by `.github/workflows/nexuscentral-v2-release.yml`.

## Repository Lanes

- `NexusCentral-V2/` — active product and deployment target.
- `NexusCentral-V1/` — stable Avalonia rollback/reference lane; no new product development.
- `NexusCentral-Electron/` — earlier Electron proof retained for feature/reference comparison.
- `NexusCentral-V0/` — historical implementation reference.
- `gh-pages` branch — generated download-site output only; never an application-development branch.

Legacy V1 build/release configuration remains available for rollback validation but does not own V2 releases.

## Validation

From `NexusCentral-V2`:

```bash
npm run check
npm run check:integration
npm run check:scale
npm run build
npm run simulate:contracts
npm run simulate:browser
```

Native CI additionally packages each supported desktop target, starts the embedded Nexus Node from the packaged Electron executable, probes `/api/v1/capabilities`, and validates Windows DPAPI secret storage on Windows.

See `NexusCentral-V2/docs/00-START-HERE.md`, `12-IMPLEMENTATION-STATUS.md`, and `15-DESKTOP-DEPLOYMENT.md` for the current implementation contract and remaining promotion gates.
