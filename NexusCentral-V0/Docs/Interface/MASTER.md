# Interface Module - MASTER

## Final Target State

- Full-featured CLI for NexusCentral operations.
- Domain-driven commands: models, ui, workflows, nexuscentral.
- Output is machine-readable and script-friendly (JSON optional).
- Well-documented commands and options.
- Robust error codes for automation wrappers.

## Key Modules

- setup.py / pyproject.toml: package metadata.
- src/nexuscentral_interface/cli.py: entrypoint and command dispatcher.
- src/nexuscentral_interface/domains/models: model operations.
- src/nexuscentral_interface/domains/ui: integration with running desktop.
- src/nexuscentral_interface/domains/workflows: run/list workflow APIs.

## Integration Contracts

- API requests to Desktop IntegrationServer (http://127.0.0.1:3005).
- Works with local runtime and workflow state stores.
- Works as a local automation surface for CI.

## Success Criteria

- `nci models download` stores model in correct path.
- `nci ui change-tab` toggles Desktop tab reliably.
- CLI docs are included in `Docs/Interface/README.md`.

---
