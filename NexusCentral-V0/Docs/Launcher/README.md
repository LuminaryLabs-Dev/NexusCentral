# Launcher Module

## Purpose

`Launcher` is a .NET build/publish orchestrator that packages the Desktop Avalonia app with the BitNet runtime adapter for deployment.

## Key configuration

- `Launcher/NexusCentral-Launcher.csproj` controls:
  - target framework: `net10.0`
  - output type: `Library` (custom publish target)
  - bundle RID values: `osx-arm64`, `osx-x64`, `win-x64`
  - runtime adapter paths for `Desktop` and `Runtime`.

## Publish flow (`BundleProduct` target)

1. Validates supported `BundleRid`.
2. Clean `$(PublishDir)` and prepare output directory.
3. `dotnet publish` Desktop app as self-contained.
4. Let the Desktop publish graph copy the BitNet runtime adapter into `$(PublishDir)/runtime`.
5. Validates artifact hygiene:
   - not include writable `models` or `logs` directories in publish output
   - not include `*.gguf`, `*.safetensors`, `catalog.json`, `library-state.json` state artifacts.

## Integration with Desktop

- Launcher relies on Desktop's standardized sidecar packaging so local build output and published bundles use the same runtime packaging path.
- Desktop finds the runtime under relative `runtime/NexusCentral-BitNet-Runtime` at runtime.
- supports multi-platform publishing with platform launchers and a shared Python adapter.

## Notes

- Keep this manifest in sync if the runtime path changes or Windows binary name changes.
