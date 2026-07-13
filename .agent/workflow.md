# Workflow

Status: active

## Purpose

Keep broad validation research, catalogs, profile packs, and agent coordination artifacts in `.agent/` instead of `NexusCentral-V1` app state.

## Notes

- Generate catalog artifacts under `.agent/use-case-catalog/`.
- Generate full profile packs from the source catalog with deterministic scripts before using them as NexusCentral build targets.
- Export one-go submission bundles from the profile pack when the user wants the largest practical handoff file.
- Treat the profile pack as the batch submission surface: the sprint plan is the build order and the seed prompt is the master spec.
- For the Electron provider/models split, implement service ownership first and keep deferred validation steps in `docs/provider-router-models-validation.md` until the user requests execution.
