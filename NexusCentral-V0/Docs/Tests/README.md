# Tests Module

## Purpose

Contains automated tests for NexusCentral core services and functionality.

## Structure

- `NexusCentral.Triceratops.Tests.csproj`: test project with references.
- `AppPathsServiceTests.cs`: tests for path resolve behavior and runtime state directories.

## How to run

From repository root:

```bash
dotnet test Tests/NexusCentral.Triceratops.Tests.csproj
```

## Notes

- Add tests for service-level behavior in `Tests/` when adding features in `Desktop/Services` or integration contracts.
