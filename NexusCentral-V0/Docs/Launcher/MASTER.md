# Launcher Module - MASTER

## Final Target State

- Canonical build-and-bundle orchestrator.
- Supports all production platforms (osx-arm64, osx-x64, win-x64, linux x64/aarch64).
- Ensures BitNet runtime adapter is included in publish output.
- Enforces artifact hygiene: no model/state artifacts in package.

## Key Modules

- NexusCentral-Launcher.csproj: publish bundle target.
- Build logic: dotnet publish desktop + copy runtime adapter.
- Copy runtime adapter into published tree.

## Integration Contracts

- Template: Desktop runtime discovery path (`./runtime/NexusCentral-BitNet-Runtime`).
- Err on unsupported RID.
- Validate `PublishDir` contains no state artifacts.

## Success Criteria

- `dotnet msbuild /t:BundleProduct` works for all supported platforms.
- No `models` or `logs` dirs in output.
- Ensure `@PublishedStateArtifacts` assertion is correct.

---
