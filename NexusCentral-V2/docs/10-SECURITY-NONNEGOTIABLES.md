# Security Non-Negotiables

## Local Sovereignty

The receiving node is always the final execution authority. Membership,
routing, relay, provider availability, or another peer's approval cannot
override local policy.

## Secrets

- Raw LAN codes, provider keys, private identity keys, and decrypted secrets remain in secure adapters.
- DSK state stores opaque secret references only.
- Snapshots, logs, events, UI state, diagnostics, and proof artifacts must redact secrets.
- Secret migration requires the original secure storage context.

## Remote Actions

- Default policy denies all work actions.
- Ping and public capability discovery may be explicitly enabled.
- Shell, filesystem, harness, model, inference, relay, and workspace transfer require separate grants.
- Grants include peer, action, resource scope, budget, expiry, and revocation.
- No wildcard remote shell capability is permitted.

## Workspace Safety

- Remote peers receive content-addressed snapshots, not arbitrary local paths.
- Workspace writes are scoped, auditable, conflict-aware, and approval-controlled.
- Symlink escapes, traversal, hidden credential files, and oversized transfers fail closed.
- Commit, push, deploy, credential use, and destructive operations require explicit approval.

## Transport

- Authenticate identity and LAN membership before decrypting or accepting work.
- Encrypt discovery details and request/response bodies.
- Reject replay, stale clocks, malformed signatures, unknown protocol versions, loops, and excessive payloads.
- Rotate compromised node identities and revoke peers locally.
- Enterprise deployment should support managed certificates and discovery alternatives when multicast is blocked.

## Runtime Isolation

- Run ONNX, CLI, provider, and native jobs outside the UI process.
- Apply CPU, GPU, memory, disk, network, token, concurrency, and timeout budgets.
- Cancellation must terminate adapters and release resources.
- Untrusted model files and generated code never load into the Nexus Node process directly.

## Audit

Record request identity, policy decision, route, job, budgets, adapter outcome,
artifact hashes, proof references, and timestamps. Peers cannot erase or rewrite
another node's local audit history.
