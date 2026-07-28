# NexusSimulator Proof

## What Runs

NexusCentral generates lifecycle descriptors from all 19 live ESM Domain
Service Kits. The sibling NexusSimulator validates metadata, dependencies,
idempotency, invalid input, exact snapshots, reset behavior, outputs, and host
isolation. Its browser interaction proof renders the composed policy scenario in
a disposable SimSpace and captures before/after evidence.

```bash
npm run simulate:contracts
npm run simulate:browser
```

`NEXUS_SIMULATOR_CLI` may point to another NexusSimulator CLI. Otherwise the
runner uses the sibling checkout at `../NexusSimulator`.

## Current Result

- 19 of 19 kit lifecycle contracts pass.
- The browser proof changes visible state and reports no console errors.
- Generated reports and screenshots are retained under ignored `output/` and
  `.simspaces/runs/` paths.

Representative evidence:

```text
output/kit-contract-report.json
.simspaces/runs/<run-id>/report.json
.simspaces/runs/<run-id>/artifacts/interaction-proof-before.png
.simspaces/runs/<run-id>/artifacts/interaction-proof-after.png
```

This proves kit contracts and the deterministic simulator surface. It does not
substitute for real provider, operating-system, LAN, package-signing, or
multi-machine release proof.
