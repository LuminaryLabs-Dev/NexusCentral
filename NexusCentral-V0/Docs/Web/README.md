# Web Module

## Purpose

`Web` is the hosted web UI layer and APIs for NexusCentral. It provides a browser-based experience on top of the desktop/integration backend.

## Structure

- `.env.example`: environment variables template.
- `backend/`: Ruby on Rails backend app
  - typical Rails layout (`app`, `config`, `bin`, `config.ru`, `Gemfile`)
  - likely offers REST API endpoints for a web dashboard and integration to Desktop through local proxy.
- `frontend/`: JS/TS frontend app
  - likely uses Next.js or another framework to render UI and call backend.

## Integration

- `NexusWebLabService` in Desktop starts frontend and backend servers when requested.
- Web backend uses Desktop `IntegrationServer` API at `127.0.0.1:3005` as source of truth for app state, workflow status, and runtime control.

## How to run

From repo root:

```bash
cd Web
# backend: rails server
bundle install
bundle exec rails server -p 3016

# frontend: next/webpack dev server
cd frontend
npm install
npm run dev
```

## Notes

- The web module is an optional UI and should be consistent with Desktop's business logic and APIs.
- Keep versioning and dependencies synced between Web and Desktop API contract when endpoints change.
