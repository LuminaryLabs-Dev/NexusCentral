# Web Module - MASTER

## Final Target State

- Full web frontend + backend integration with Desktop.
- Backend API should mirror integration endpoints and provide user-friendly dashboard.
- Frontend should support all workspace and workflow operations.
- Deploy docs specifying local and cloud modes.

## Key Modules

- Web/backend: Rails app with API endpoints and a health endpoint.
- Web/frontend: JS/Next.js app with auth and UI pages.

## Integration Contracts

- `NexusWebLabService` provides environment `NEXUSCENTRAL_INTEGRATION_BASE_URL=http://127.0.0.1:3005`.
- Frontend and backend must use the same API spec.
- Fallback when Desktop offline gracefully.

## Success Criteria

- WebLab spin-up from Desktop starts both processes and passes health checks.
- User can control workflows via Web UI.
- API surfaces stable, documented in Docs/ Web API file.

---
