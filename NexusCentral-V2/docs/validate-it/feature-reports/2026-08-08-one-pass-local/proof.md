# Proof

## Passed Commands

- `npm run check`: 19 DSK graph checks and workspace builds passed.
- `npm run check:integration`: provider routing, sharing, retry, SimSpace, approval, recovery, LAN, browser runtime, libp2p, and PeerJS checks passed.
- `npm run check:scale`: 50 simulated nodes, 10 churned nodes, and 1,000 queued jobs passed at 76 MB heap.
- NexusAgent `npm run release:check`: 52 release units, 502 Python files, and 90 `.mjs` files checked successfully, with release-hygiene warnings.
- Manual Nexus Node launch: ready at `http://127.0.0.1:47800`.
- Restart: node identity, provider, run, job, and artifact state persisted.
- Artifact commands: list and read passed; browser download produced `request.json`.
- Cancellation: job and run reconciled to `canceled`.

## Failed Or Blocked Commands

- `npm run matrix:validate`: missing script.
- `npm run matrix:queue`: missing script.
- `npm run setup:lfm`: missing script.
- `LM_STUDIO_URL=http://127.0.0.1:1234/v1 LM_STUDIO_MODEL=LFM2-350M-Q4_K_M npm run check:lm-studio`: connection refused.
- NexusAgent Conversation Harness with LFM2-350M: model endpoint unavailable.
- NexusAgent manifest import: HTTP 400, missing NexusCentral stages contract.

## State Conflict

With no healthy route, `inference.submit` accepted the request and retained its input artifact. The job moved to `retry` with `No compatible inference route is available`, while the run remained `queued` with the same error. Restart preserved that contradiction. Explicit cancellation reconciled both records to `canceled`.
