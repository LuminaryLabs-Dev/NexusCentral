# Desktop Module - MASTER

## Final Target State

- Desktop is the primary Avalonia UI and orchestration shell.
- Production-ready integration server API is stable and documented.
- All major workflows are user-driven through the UI and API.
- Complete error handling in automation, workflow, and runtime edge cases.

## Key Modules

- Program.cs: Avalonia app host.
- App.axaml.cs: app lifecycle initialization / shutdown.
- AppServices: service wiring and lifetime management.
- IntegrationServer: local API for external tooling.
- NexusWebLabService: optional web UI host.
- RuntimeService: model runtime sidecar management.

## Integration Contracts

- `IntegrationServer` API at `http://127.0.0.1:3005`.
- `NexusWebLabService` starts React and Rails services, proxies to IntegrationServer.
- `RuntimeService` spawns `runtime/NexusCentral-BitNet-Runtime`.
- `Launcher` bundles runtime and assets for target OS.

## Success Criteria

- `nci ui` operations execute reliably with response times <200ms.
- Integration API discovered by `nci` and web frontend.
- WebLab startup works with health checks for 3015/3016.
- Automation and workflows retriable for transient runtime failures.

---
