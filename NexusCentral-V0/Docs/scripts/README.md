# scripts Module

## Purpose

Contains utility scripts for quality checks and publish validation.

## Contents

- `verify-publish-clean.sh`: script that validates the publish output does not contain writable state directories or model artifacts.

## Behavior

The script is used before publishing a new release to ensure:
- no unintended `models` or `logs` directories are included in publish artifacts
- no pre-existing GGUF/Safetensors or state files (catalog/library-state) are included

## How to run

```bash
./scripts/verify-publish-clean.sh
```

## Notes

- This is a preflight script; include in CI checks for release packaging.
