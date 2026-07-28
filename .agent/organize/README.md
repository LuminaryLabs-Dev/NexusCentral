# Organized Legacy Builder Library

Status: imported; content fully validated
Workflow classification: legacy and unvalidated

## Purpose

Preserve the former NexusCentral-Builder planning and workflow-authoring workspace inside Nexus Central's existing `.agent/` control center without treating the imported material as app runtime state or active workflow policy.

## Contents

- [`builder/README.md`](builder/README.md): entry point to the complete sanitized 244-file legacy snapshot
- [`import-manifest.json`](import-manifest.json): provenance, exclusions, classification, and validation results
- [`checksums.sha256`](checksums.sha256): SHA-256 inventory for the files under `builder/`

## Rules

- Treat lifecycle labels inside `builder/` as historical classifications only.
- Do not activate, promote, or execute an imported workflow without a separate current review.
- Keep Nexus Central application state and implementation work outside this library.
- Preserve the imported tree as read-only provenance; add current decisions to the root `.agent/` workspace.
