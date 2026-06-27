# Desktop (Avalonia) Module

## Purpose

The Desktop project is the primary user interface and orchestration hub for NexusCentral. It hosts UI, workflow management, runtime coordination, integration server endpoints, automation execution, and WebLab facilitation.

## Key entry points

- `Program.cs`:
  - `Main(string[] args)` calls `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`.
- `App.axaml.cs`:
  - `OnFrameworkInitializationCompleted()` instantiates `AppServices`, `MainViewModel`, `MainWindow` and starts `IntegrationServer`, `NexusWebLabService`, and automation loop services.

## Core services

- `AppServices` (factory container)
  - `AppPathsService`, `LogService`, `AppStateService`
  - `ModelCatalogService`, `ModelLibraryService`, `AgentConfigService`, `WorkflowCatalogService`, `WrapperCatalogService`, `TemplateCatalogService`, `WorkspaceService`
  - `RuntimeService`, `WrapperRunService`, `WorkflowRunService`, `WorkflowAutomationService`, `AutomationExecutionService`
  - `IntegrationRegistryService`, `IntegrationServer`, `NexusWebLabService`
  - `UiAutomationService`, `ChatMemoryService`, `ScreenshotService`
  - `LanRoutingSettingsService`, `LanPeerRegistryService`, `LanDiscoveryAdvertisementService`, `LanDiscoveryBrowserService`, `RoutingRequestTelemetryService`

## UI architecture

- MVVM pattern.
- `ViewLocator` maps view model class names to view XAML controls.
- `MainViewModel` controls page navigation, UI state, notifications.
- Feature-specific view models in `Desktop/ViewModels` (e.g., `ChatViewModel`, `AutomationsViewModel`, `ModelsViewModel`).

## Configuration directories

- `Desktop/config/templates`: Default automation templates.
- `Desktop/config/workflows`: Default workflow definitions.
- `Desktop/config/wrappers`: Default wrapper scripts or containers.

## Runtime integration

- `RuntimeService` launches `runtime/NexusCentral-BitNet-Runtime` and interacts with it for BitNet model inference.
- `Launcher` bundling makes sure runtime binary for current platform is included.

## Interface/Web integration

- `IntegrationServer` starts HTTP API on `http://127.0.0.1:3005`.
- `NexusWebLabService` starts the web host frontend/backend and points them to the integration API.
- `Interface` domain API and `Web` front-end use JSON endpoints from `IntegrationServer`.

## Notes

- Dontomt-level data (models, logs, workspaces) are defined by `AppPathsService` and persist in a platform-state directory.
- When modifying Desktop, keep integration API contract stable since Interface/Web rely on fixed endpoints.
- Router now includes a split LAN peer workspace backed by canonical peer records and imported remote route targets rather than one manual remote placeholder.
- Discovery is advisory only. Trust and runtime readiness still live in services and runtime admission remains stricter than UI projection.

## Planning Docs

- `DESIGN.md`: central Desktop UI design authority covering color/token roles, semantic style vocabulary, structural UI DNA, tab-by-tab pattern inventory, drift, and review rules.
- `router-seamless-lan/`: master plan pack for a Models-style split Router UI, automatic LAN discovery, trusted peer import, and MVVM-safe implementation slices, including an execution-ordered master implementation sequence.
- Automations now owns the workflow ladder UI; the standalone Workflow top-level tab is no longer part of the Desktop shell.
