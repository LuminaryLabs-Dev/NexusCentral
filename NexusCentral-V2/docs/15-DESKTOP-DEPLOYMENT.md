# Desktop Deployment

## Canonical Lane

`NexusCentral-V2` is the active product and deployment lane. V0, V1, and the earlier `NexusCentral-Electron` implementation remain rollback/reference sources only.

The deployable desktop shape is:

```text
Electron shell
  -> loopback Nexus Node
  -> static Next.js UI
  -> SQLite state / providers / models / harnesses / mesh
```

Electron owns the native window and trusted IPC. Nexus Node owns durable state, execution, routing, policy, credentials, networking, and the HTTP/WebSocket API. Closing the desktop window must not be treated as completion of accepted Node work.

## Supported Build Matrix

The source of truth is `updates/nexuscentral-v2-targets.json`.

| Target | GitHub runner | Electron platform | Electron arch |
| --- | --- | --- | --- |
| Windows x64 | `windows-latest` | `win32` | `x64` |
| Linux x64 | `ubuntu-latest` | `linux` | `x64` |
| macOS Intel | `macos-15-intel` | `darwin` | `x64` |
| macOS Apple Silicon | `macos-15` | `darwin` | `arm64` |

Local packaging requires a completed static build:

```bash
npm ci
npm run build
npm run package:desktop -- <platform> <arch>
npm run smoke:packaged -- <platform> <arch>
```

The package script rejects unknown platform/architecture values and no longer infers the requested artifact solely from the build host's `process.arch`.

## Runtime Version Registry

`updates/update-registry.json` is the desktop runtime authority. Electron reads that registry instead of enumerating arbitrary Git branches.

The version contract follows NexusEngine's version-branch pattern:

```text
main   -> moving development runtime
0.1.0  -> frozen usable runtime
```

Only refs present in the registry are offered by the desktop app. Version selection uses the human-readable branch ref directly; commit hashes are not part of the user-facing runtime contract.

A registered runtime must declare `type: esm`, `root: NexusCentral-V2`, supported desktop targets, and the minimum compatible Electron bootstrap version. The Electron bootstrap downloads the selected branch archive and installs only the versioned ESM runtime surfaces (`adapters`, `kits`, `packages`, and `services`). It continues to use the packaged native dependencies and static web output, so a runtime that requires new native dependencies or a new desktop/web bootstrap must raise `minimumBootstrap` instead of mutating those bundled surfaces.

The active selection is stored under Electron user data as `current.json` with the active version and channel. Stable versions remain frozen after publication. The `main` channel is development-only and refreshes its ESM runtime from the moving `main` branch when NexusCentral starts.

## Native Promotion Gate

`.github/workflows/nexuscentral-v2-build.yml` is the V2 build gate. It must:

1. run static/domain checks;
2. run integration checks;
3. run scale checks;
4. build the shared web and Electron assets;
5. run contract and browser simulation;
6. reject high-severity dependency findings;
7. package on the target operating system and architecture;
8. start Nexus Node from the packaged Electron executable using `ELECTRON_RUN_AS_NODE=1`;
9. confirm `/api/v1/capabilities` responds from the packaged payload;
10. validate the Windows DPAPI secret-store round trip on Windows;
11. archive the native bundle and publish a SHA-256 sidecar.

A package is not promoted merely because `@electron/packager` produced a directory.

## Credential Storage

`OsSecretStore` uses one platform contract:

- macOS: Keychain through `security`;
- Linux: Secret Service through `secret-tool`;
- Windows: DPAPI `CurrentUser`, with only the encrypted DPAPI blob persisted under the current user's local application-data directory.

Windows plaintext secrets are passed to PowerShell through stdin, not command-line arguments. Provider state continues to persist only secret references.

## Releases

`.github/workflows/nexuscentral-v2-release.yml` owns automatic `v*` tagged releases and manual V2 prereleases. It reuses the V2 build workflow, downloads only V2 package artifacts, and publishes the platform ZIPs plus SHA-256 files.

The previous Avalonia V1 release workflow is manual-only and is retained strictly for rollback/reference releases.

## Current Distribution Boundary

Portable ZIP builds remain the first distribution format for the Electron bootstrap. Branch-based ESM runtime selection and refresh are now implemented inside the packaged desktop app. Signing, notarization, native installers, and automatic replacement of the Electron/bootstrap bundle remain later promotion gates.

Before a public stable release, additionally require:

- macOS signing and notarization;
- Windows Authenticode signing;
- installer/uninstaller behavior;
- upgrade and rollback proof with existing V2 SQLite state;
- physical multi-device LAN/PeerJS proof;
- trusted LAN certificates for non-development deployments.
