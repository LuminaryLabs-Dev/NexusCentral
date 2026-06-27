using System;
using NexusCentral_Avalonia.ViewModels;

namespace NexusCentral_Avalonia.Services;

public class AppServices : IDisposable
{
    public AppServices()
    {
        Paths = new AppPathsService();
        Logs = new LogService(Paths.LogsDirectory);
        AppState = new AppStateService();
        UiStyleSettings = new UiStyleSettingsService(Paths, Logs);
        SplitWorkspaceLayout = new SplitWorkspaceLayoutService(Paths, Logs);
        Templates = new TemplateCatalogService(Paths, Logs);
        Workflows = new WorkflowCatalogService(Paths, Logs);
        Wrappers = new WrapperCatalogService(Paths, Logs);
        AgentConfigs = new AgentConfigService(Paths, Logs);
        Workspaces = new WorkspaceService(Paths, Logs);
        LanRouting = new LanRoutingSettingsService(Paths, Logs);
        PeerRegistry = new LanPeerRegistryService(Paths, LanRouting, Logs);
        LanDiscoveryAdvertisement = new LanDiscoveryAdvertisementService(LanRouting, Logs);
        LanDiscoveryBrowser = new LanDiscoveryBrowserService(Paths, LanRouting, Logs);
        Catalog = new ModelCatalogService(Paths, Logs);
        Library = new ModelLibraryService(Paths, Catalog, PeerRegistry, LanRouting, Logs);
        DirectChatExecutionPolicy = new DirectChatExecutionPolicy(Library);
        DirectChatQualityGuard = new DirectChatQualityGuard();
        LanDiscoveryBrowser.Changed += () =>
        {
            PeerRegistry.ApplyDiscoveryObservations(LanDiscoveryBrowser.GetSnapshot().Peers);
        };
        RoutingRequests = new RoutingRequestTelemetryService();
        UiAutomation = new UiAutomationService();
        Integrations = new IntegrationRegistryService();
        Downloads = new DownloadService(Paths, Catalog, Logs);
        Runtime = new RuntimeService(Paths, Catalog, Library, RoutingRequests, Logs);
        Screenshots = new ScreenshotService(AppState);
        WebLab = new NexusWebLabService(Paths, Logs);
        WrapperRuns = new WrapperRunService(Paths, Templates, Wrappers, Library, Runtime, Logs);
        WorkflowRuns = new WorkflowRunService(Paths, Workflows, Integrations, WrapperRuns, Logs);
        WorkflowValidation = new WorkflowValidationService(Wrappers, Integrations);
        WorkflowDirectoryScanner = new WorkflowDirectoryScannerService();
        WorkflowClassifier = new WorkflowClassifierService(Paths, WorkflowValidation);
        WorkflowImports = new WorkflowImportService(Paths);
        WorkflowAutomationHistory = new WorkflowAutomationRunStateService();
        WorkflowAutomation = new WorkflowAutomationService(WorkflowDirectoryScanner, WorkflowClassifier, WorkflowImports, WorkflowAutomationHistory, WorkflowRuns);
        AutomationExecution = new AutomationExecutionService(Paths, Runtime, Logs);
    }

    public AppPathsService Paths { get; }
    public LogService Logs { get; }
    public AppStateService AppState { get; }
    public UiStyleSettingsService UiStyleSettings { get; }
    public SplitWorkspaceLayoutService SplitWorkspaceLayout { get; }
    public TemplateCatalogService Templates { get; }
    public WorkflowCatalogService Workflows { get; }
    public WrapperCatalogService Wrappers { get; }
    public AgentConfigService AgentConfigs { get; }
    public WorkspaceService Workspaces { get; }
    public LanRoutingSettingsService LanRouting { get; }
    public LanPeerRegistryService PeerRegistry { get; }
    public LanDiscoveryAdvertisementService LanDiscoveryAdvertisement { get; }
    public LanDiscoveryBrowserService LanDiscoveryBrowser { get; }
    public ModelCatalogService Catalog { get; }
    public ModelLibraryService Library { get; }
    public DirectChatExecutionPolicy DirectChatExecutionPolicy { get; }
    public DirectChatQualityGuard DirectChatQualityGuard { get; }
    public RoutingRequestTelemetryService RoutingRequests { get; }
    public UiAutomationService UiAutomation { get; }
    public IntegrationRegistryService Integrations { get; }
    public DownloadService Downloads { get; }
    public RuntimeService Runtime { get; }
    public ScreenshotService Screenshots { get; }
    public NexusWebLabService WebLab { get; }
    public WrapperRunService WrapperRuns { get; }
    public WorkflowValidationService WorkflowValidation { get; }
    public WorkflowDirectoryScannerService WorkflowDirectoryScanner { get; }
    public WorkflowClassifierService WorkflowClassifier { get; }
    public WorkflowImportService WorkflowImports { get; }
    public WorkflowAutomationRunStateService WorkflowAutomationHistory { get; }
    public WorkflowAutomationService WorkflowAutomation { get; }
    public AutomationExecutionService AutomationExecution { get; }
    public ChatViewModel? Chat { get; private set; }
    public WorkflowViewModel? WorkflowViewModel { get; private set; }
    public AutomationsViewModel? AutomationsViewModel { get; private set; }

    public void RegisterChat(ChatViewModel chatViewModel)
    {
        Chat = chatViewModel;
    }

    public WorkflowRunService WorkflowRuns { get; }

    public void RegisterWorkflowViewModel(WorkflowViewModel workflowViewModel)
    {
        WorkflowViewModel = workflowViewModel;
    }

    public void RegisterAutomationsViewModel(AutomationsViewModel automationsViewModel)
    {
        AutomationsViewModel = automationsViewModel;
    }

    public void StartLanServices()
    {
        LanDiscoveryAdvertisement.Start();
        LanDiscoveryBrowser.Start();
    }

    public void StopLanServices()
    {
        LanDiscoveryBrowser.Stop();
        LanDiscoveryAdvertisement.Stop();
        Library.Dispose();
    }

    public void Dispose()
    {
        StopLanServices();
        LanDiscoveryBrowser.Dispose();
        LanDiscoveryAdvertisement.Dispose();
        WorkflowAutomation.Dispose();
    }
}
