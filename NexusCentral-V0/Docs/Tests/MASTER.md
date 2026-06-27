# Tests Module - MASTER

## Final Target State

- Complete coverage for AppServices, Workflow execution, Integration API, and Runtime integration.
- Clear test conventions and tooling in Docs.
- Regression tests for bug-fix scenarios in Desktop, Interface, Web, Runtime.
- CI passes with `dotnet test` and optional path coverage.

## Key Modules

- `Tests/NexusCentral.Triceratops.Tests.csproj`.
- `Tests/AppPathsServiceTests.cs`.

## Success Criteria

- 100% path coverage in core services is targeted.
- Integration test for Desktop->Runtime workflow exists.
- `verify-publish-clean.sh` executed from CI.

---
