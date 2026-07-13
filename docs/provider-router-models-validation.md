# Provider Router And Models Validation

Status: deferred

## Purpose

Validate the Electron provider Router and the separate local Models service after implementation review is approved. This document describes validation only; it does not claim the checks have run.

## Prerequisites

- Install Electron dependencies with `npm install` in `NexusCentral-Electron`.
- For local-model checks, run LM Studio 0.4 or newer with its server enabled.
- Use test credentials with restricted spend limits for hosted providers.
- Never place API keys in screenshots, logs, fixtures, or repository files.

## Static Checks

```text
cd NexusCentral-Electron
npm run check
npm run build
node --check src/main.js
node --check src/preload.js
node --check src/renderer.mjs
node --check src/services/router-service.js
node --check src/services/models-service.js
```

Expected: every command exits successfully and the build stages both service files.

## Router Validation

1. Start the app with `npm run start` and open Router.
2. Confirm the provider list contains OpenRouter, OpenAI, Google Gemini, NVIDIA NIM, LM Studio, and OpenAI-compatible.
3. Change provider types and confirm the documented default base URL changes without exposing a previously saved API key.
4. Enter a restricted test key, test the provider, and confirm the available model list populates the Model field.
5. Restart the app and confirm the provider profile and selected model persist while the API key remains blank with a `saved securely` indication.
6. Inspect the Router registry under Electron user data and confirm no plaintext API key exists.
7. Enter an invalid URL or key and confirm Router becomes unavailable with an actionable HTTP or credential error.

Expected: Router owns provider profile, model selection, health, and credential reference. It does not own model downloads or coding-harness tools.

## Models Validation

1. Configure and save an LM Studio provider in Router.
2. Open Models and select Refresh.
3. Confirm downloaded LM Studio models appear with their loaded or unloaded state.
4. Search for `qwen gguf` and confirm Hugging Face results appear.
5. Start one small model download and record the returned LM Studio job ID.
6. After LM Studio reports the download complete, refresh and confirm the model appears locally.
7. Load the model and confirm LM Studio reports an active instance using a 16000-token context request.
8. Unload it and confirm its local state changes without deleting the downloaded model.
9. Load it again, select `Use as provider`, and confirm Router activates an LM Studio profile with that model ID.

Expected: Models owns search, download, load, unload, and local model state. Publishing creates or updates a Router provider profile without moving lifecycle ownership into Router.

## Failure Cases

- LM Studio offline: Models shows a connection error and does not report loaded state.
- Download rejected: the LM Studio HTTP status is shown and no completed download is claimed.
- Load rejected or out of memory: the error remains in Models and Router is not silently promoted.
- Hosted provider key missing: required providers remain unavailable.
- Custom provider without `/models`: Router reports the provider response rather than claiming readiness.

## Human View

- Router first view contains only provider, base URL, API key, model, state, and `Test and save`.
- Models first view contains search, local models, and lifecycle actions.
- Lab remains workspace-left and coding-chat-right.
- No API key value is visible after save or restart.
- No Router or Models control overlaps at the minimum supported window size.

## Promotion Gate

Promote the feature only after static checks, one hosted-provider route, the complete LM Studio lifecycle, secure-key inspection, and the human-view checks all pass.
