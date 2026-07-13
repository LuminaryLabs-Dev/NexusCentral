# LAN Mesh Routing

## Outcome

Computers using the same LAN code automatically form a direct peer mesh. There
is no controller node. Every computer retains authority over its local actions,
models, harnesses, files, secrets, and resource limits.

## Join Flow

```text
Settings -> LAN mesh
-> enter computer name and shared LAN code
-> Join mesh
-> signed multicast announcements discover matching nodes
-> peers appear in Settings
-> Ping proves an authenticated encrypted request
```

The LAN code is encrypted at rest through Electron `safeStorage`. It is used to
derive a network identifier, HMAC key, and AES-256-GCM key. The code itself is
never included in an announcement or request.

## Security Boundary

Each node has a persistent Ed25519 identity. Announcements and request envelopes
are signed and authenticated. Discovery details plus request and response bodies
are encrypted. Peer records expire when announcements stop.

Joining establishes mesh membership, not execution permission. The receiving
node applies its own allow-list after authenticating and decrypting a request.
The initial allow-list contains only:

- `mesh.ping`
- `mesh.capabilities`

Arbitrary shell commands, Lab runs, workspace access, model execution, and
harness execution are denied.

## Service Contract

`src/services/mesh-routing-service.js` owns:

- persistent node identity
- LAN-code-derived mesh membership
- UDP multicast discovery
- peer leases and capability advertisements
- encrypted signed HTTP request envelopes
- local action registration and admission
- direct peer routing

Electron owns secure code storage and exposes minimal Settings IPC. The renderer
never receives the saved LAN code, private node key, or encryption key.

## Next Gate

Before enabling remote work, add a scoped job envelope containing workspace
snapshot identity, requested harness, tool permissions, CPU/GPU/token budgets,
timeout, cancellation, artifact manifest, and audit receipt. The receiving node
must display and enforce those limits locally.
