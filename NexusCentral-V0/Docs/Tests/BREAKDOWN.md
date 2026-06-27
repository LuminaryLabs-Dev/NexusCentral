# Tests Module - BREAKDOWN

## Current State

- Single unit test file: `AppPathsServiceTests.cs`.
- Tests project depends on core Desktop logic and AppPathsService.

## Bug / Interaction Audit

- Lack of test coverage for Automation and IntegrationServer.
- No web layer tests for `nexuscentral` API semantics.
- No runtime integration tests for `runtime/NexusCentral-BitNet-Runtime` execution path.

## Cross-module Interaction Matrix

- Tests -> Desktop: validates path resolution and state directories.
- Tests -> Interface: no direct coverage currently.
- Tests -> Web: no coverage.
- Tests -> Runtime: no coverage.

---
