# Web Module - BREAKDOWN

## Current State

- `Web/backend` is a Rails project with the standard layout and bundle configuration.
- `Web/frontend` is a JS frontend (likely Next.js) with `npm run dev` entry.
- `NexusWebLabService` starts backend and frontend via commands.
- Health checks target `/health` on backend and `/` on frontend.

## Bug / Interaction Audit

- Risk of Ruby/Gem dependencies causing broken startup due to outdated gems.
- Frontend reliance on local WebLab ports means conflicts on 3015/3016 can break startup.
- No integration test in repo to verify Web <-> Desktop contract.

## Cross-module Interaction Matrix

- Web -> Desktop: each request proxies through IntegrationServer.
- Web -> Runtime: indirectly via Desktop runtime routes.
- Web -> Interface: no direct CLI integration; uses same APIs.

---
