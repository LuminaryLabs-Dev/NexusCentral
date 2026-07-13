# Goal

Status: active

## Purpose

Build sovereign LAN mesh routing while preserving the completed separation between provider routing and local model lifecycle.

## Success Criteria

- Complete: Router configures OpenRouter, OpenAI, Google Gemini, NVIDIA NIM, LM Studio, and custom OpenAI-compatible providers.
- Complete: Provider API keys use Electron secure storage and are not persisted in Router JSON.
- Complete: Models has a separate page and service for search, download, load, unload, and local state.
- Complete: A local LM Studio model can be published as a Router provider profile.
- Complete: Coding harness execution remains separate from provider and model lifecycle ownership.
- Complete: Deferred validation instructions exist under `docs/`.
- Complete: Computers with the same LAN code automatically discover each other without a central controller.
- Complete: Mesh announcements and requests authenticate persistent node identities without broadcasting the LAN code.
- Complete: Peer request payloads are encrypted and local action policy denies unapproved work.
- Complete: Settings can join, disconnect, refresh, and ping discovered mesh peers.
- Pending: Add explicitly approved remote harness/job actions after workspace transfer, audit, and resource-limit contracts exist.
