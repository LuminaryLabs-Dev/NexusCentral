# NexusCentral V2

The active interface is the visual automation workbench: Home, Workflows, Apps,
and Activity. Drag or click to add an action, configure it, connect its result,
and run a saved workflow. Start with the Operations review template.

See [Automation Workbench](docs/17-AUTOMATION-WORKBENCH.md) for exact feature
coverage, sandbox requirements, validation, and remaining integration work.
Built-in workflow execution currently requires Linux, Python 3, libseccomp, and
Node 24+. Other platforms can edit/save workflows; external agent apps are not
enabled for workflow execution yet.

### Managed llama.cpp (Linux x64)

Run `npm run setup:llama-cpp` to build the pinned official llama.cpp source
commit for this machine. The result is staged outside Git and included by
`npm run package:desktop -- linux x64` when present; packaging without this
optional runtime remains supported. The setup needs Git, CMake, and a C++
toolchain. GGUF model files are not downloaded or packaged. In Models, enter a
host-local GGUF path and start the runtime. NexusCentral launches the staged
server on loopback only, checks readiness and model identity, and keeps it alive
only while Nexus Node runs. Choose “Use for inference” to route new jobs there.

Run a real CPU proof with `NEXUS_LLAMA_MODEL_PATH=/path/to/model.gguf`
`NEXUS_LLAMA_SERVER_PATH=/path/to/llama-server npm run check:llama-cpp-live`.
The check uses the app's 16,000-token context and verifies that Nexus Node
terminates the owned server process on shutdown.


NexusCentral V2 is a service-operated coding workbench. A headless Nexus Node
owns workspaces, jobs, routing, policy, mesh membership, browser compute leases,
and audit state. The shared Next.js interface runs in Electron or over the LAN.

```text
Electron or Nexus Central Web
            |
       CentralClient
  direct | IPC | HTTP + WS
            |
       Nexus Node
  19 ESM DSKs + SQLite WAL
     /       |        \
providers  mesh peers  browser ONNX
```

V1 and the prior Electron app remain untouched rollback references.

## Run

Requires Node.js 24 or newer.

```bash
npm install
npm run build
npm run dev
```

Open [http://127.0.0.1:47800](http://127.0.0.1:47800), or launch the thin desktop
shell with `npm run start:electron`. Electron starts Nexus Node when needed; the
node remains alive when the window closes.

LAN mode generates a local TLS certificate unless certificate paths are set:

```bash
npm run dev -- --lan
```

Pair browsers from `Settings -> Nexus Central Web`. The mesh LAN code and the
browser pairing code are separate credentials.

For remote PeerJS model rooms, run the self-hosted signaling broker and connect
from `Settings -> Remote model room`:

```bash
npm run dev:peer-broker
```

The broker sees transport IDs, not room codes or provider credentials. Peers
with the same room code discover each other, but providers remain private until
the owner enables sharing and explicitly allows the requesting peer.

## Prove

```bash
npm run check
npm run check:integration
npm run check:scale
npm run simulate:contracts
npm run simulate:browser
npm run check:lm-studio
npm run check:harness-live
```

The live LM Studio check defaults to `http://10.0.0.137:1234/v1`,
`qwen3.5-4b`, and a 16,000-token context. Override it with `LM_STUDIO_URL` and
`LM_STUDIO_MODEL`.

Migration is dry-run by default:

```bash
npm run migrate:electron
npm run migrate:electron -- --apply
```

Desktop bundles can be assembled with `npm run package:desktop -- darwin`,
`linux`, or `win32`.

Start with [docs/00-START-HERE.md](docs/00-START-HERE.md). Current proof and
remaining promotion blockers are in
[docs/12-IMPLEMENTATION-STATUS.md](docs/12-IMPLEMENTATION-STATUS.md).
