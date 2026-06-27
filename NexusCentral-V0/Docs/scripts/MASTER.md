# scripts Module - MASTER

## Final Target State

- All release and verification scripts are documented and reliable.
- A script exists for clean publish validation, packaging checks, and CI gating.
- `Docs/scripts/README.md` points to the expected CI pipeline usage.

## Key Modules

- `scripts/verify-publish-clean.sh`

## Success Criteria

- Script confirms no models/logs in publish output.
- Script exit code indicates pass/fail for pipeline.

---
