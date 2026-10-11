# Workbench validation — October 11, 2026

These screenshots and reports were captured from the real production V2 web
build served by Nexus Node, using a fresh temporary database and real isolated
workers. They cover the modified working tree based on
`eac58b1ecd0b7ae069115831a4fe9d5256a855ae`; they do not describe that base commit alone.
The JSON retains original capture paths for provenance. Images are colocated here
for durable review. No real credentials or business records were used.

## Results

- `npm run build`: shared Next.js static interface and Electron shell passed.
- `npm run check`: 20 DSKs, renderer/host boundaries passed.
- `npm run check:workflows`: real worker/report/review, graph validation,
  revisions, permissions, idempotency, app dependencies, restart persistence,
  pause/resume, cancellation, conditional/parallel steps, retry, cleanup passed.
- Worker probes denied external file reads/writes, child processes, and sockets;
  linked sandbox directories were rejected.
- Browser proof: build from empty canvas, drag/drop and accessible Add,
  configure/connect, move, undo/redo, duplicate/delete, missing-field errors,
  save/run, download report, approve, reopen, Apps, mobile configuration passed.
  Zero browser errors and failed network requests were recorded.
- Evidence schema validation: passed.
- NexusSimulator contract proof: 20/20 accepted, zero rejected.
- `npm run check:scale`: 50 simulated nodes / 1,000 jobs passed.
- `npm audit --audit-level=high`: zero vulnerabilities after compatible updates.
- Bounded integration check: **partial**, with provider/approval/recovery/auth
  regressions passing and live libp2p / PeerJS checks explicitly skipped because
  this execution environment prohibits network-interface enumeration.

Browser capture used Chromium 143 with Playwright 1.57 and system fonts. CI
installs the Playwright browser normally. The default CI network checks remain
mandatory; no native package, physical network, external agent, or live Luminary
connector readiness is claimed. CI now installs a pinned NexusSimulator checkout
instead of assuming it is already beside the repository.

## Screenshots

[Home](before.png) · [Builder](canvas.png) · [Review](review.png) ·
[Mobile Home](mobile.png) · [Mobile builder](mobile-builder.png)

See [delivery scope and remaining work](../../17-AUTOMATION-WORKBENCH.md).
