# Questions

## Product Versioning

- Should the desktop project keep the current `NexusCentral-V3.csproj` name or move to a neutral `NexusCentral.csproj` before release packaging?
- Should public update channels start with `stable` and `beta`, or only `stable` until the first release is proven?

## Update Pipeline

- Should package manifests live at repo root under `packages/`, under `NexusCentral-V1/packages/`, or in a separate public update repository?
- Should the first updater be check-only/manual install, or should it download and stage updates automatically after user approval?

## Release

- What is the first public version target: `v0.1.0`, `v0.2.0`, or another label?
