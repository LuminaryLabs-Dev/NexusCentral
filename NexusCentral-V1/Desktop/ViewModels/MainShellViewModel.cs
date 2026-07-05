using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexusCentral.V1.Features.Harnesses;
using NexusCentral.V1.Core.Execution;
using NexusCentral.V1.Core.Logging;
using NexusCentral.V1.Features.Lab;
using NexusCentral.V1.Features.Models;
using NexusCentral.V1.Features.Router;

namespace NexusCentral.V1.Desktop.ViewModels;

public partial class MainShellViewModel : ObservableObject
{
    private readonly LabViewModel _lab;
    private readonly ModelsViewModel _models;
    private readonly RouterViewModel _router;
    private readonly HarnessImporterViewModel _harnesses;
    private readonly IAppLogService _logs;
    private readonly IExecutionEventStream _executionEvents;

    public MainShellViewModel(
        LabViewModel lab,
        ModelsViewModel models,
        RouterViewModel router,
        HarnessImporterViewModel harnesses,
        IAppLogService logs,
        IExecutionEventStream executionEvents,
        SurfaceKind? startupSurface = null)
    {
        _lab = lab;
        _models = models;
        _router = router;
        _harnesses = harnesses;
        _logs = logs;
        _executionEvents = executionEvents;
        _lab.PropertyChanged += OnLabPropertyChanged;
        _models.PropertyChanged += OnModelsPropertyChanged;
        _router.PropertyChanged += OnRouterPropertyChanged;
        _harnesses.PropertyChanged += OnHarnessesPropertyChanged;
        _logs.EntryAdded += OnLogEntryAdded;
        _executionEvents.EventPublished += OnExecutionEventPublished;
        Tools =
        [
            new("List tools", "GET /tools", "read", "planned", "safe"),
            new("Workspace read", "POST /workspace/read", "workspace.read", "planned", "safe"),
            new("Workspace write", "POST /workspace/write", "workspace.write", "planned", "approval"),
            new("Model list", "GET /models", "model.read", "planned", "safe"),
            new("LM Studio chat", "POST /providers/lm-studio/chat", "provider.chat", "planned", "network"),
            new("Job enqueue", "POST /jobs", "job.write", "planned", "safe"),
            new("Device route", "POST /devices/route", "device.route", "planned", "trusted peer"),
            new("Automation run", "POST /automations/run", "automation.run", "planned", "approval")
        ];

        Surfaces =
        [
            new(
                SurfaceKind.Lab,
                "Lab",
                "Conversation and execution workspace for jobs, files, and live output.",
                "LB",
                "Idle",
                "Backed compact chat, workspace selection, folder tree browsing, chat uploads, and inline tool steps.",
                "New run",
                "Open workspace",
                surfaceModel: _lab),
            new(
                SurfaceKind.Automations,
                "Automations",
                "Planned workflow builder shell for ladders, validation, runs, history, and artifacts.",
                "AU",
                "Planned",
                "Read-only planned surface. Automation runner behavior is not active in this slice.",
                "New automation",
                "Run selected"),
            new(
                SurfaceKind.Agents,
                "Agents",
                "Harness importer for wrappers, selected capabilities, and execution policy.",
                "AG",
                "Service",
                "Service-backed harness registry with attach, select, remove, capability gates, and hosted harness test turns.",
                "Discover CLI",
                "Capabilities",
                surfaceModel: _harnesses),
            new(
                SurfaceKind.Router,
                "Router",
                "Planned request routing across local endpoints, provider endpoints, and trusted NexusCentral queues.",
                "RT",
                "Service",
                "Truthful route availability derived from selected model endpoint health and active hosted harness readiness.",
                "Add router",
                "Refresh",
                surfaceModel: _router),
            new(
                SurfaceKind.Models,
                "Models",
                "Routable model endpoint setup and runtime health checks.",
                "MO",
                "Service",
                "Model endpoint registry, selected model status, and LM Studio/OpenAI-compatible probes. No model downloads.",
                "Add endpoint",
                "Probe",
                surfaceModel: _models),
            new(
                SurfaceKind.Settings,
                "Settings",
                "Application configuration, theme preferences, paths, and system defaults.",
                "ST",
                "Local",
                "Config profiles, path service, theme choices, behavior defaults, and local-only preferences.",
                "Open config",
                "Theme")
        ];

        SelectedSurface = startupSurface is null
            ? Surfaces[0]
            : Surfaces.FirstOrDefault(surface => surface.Kind == startupSurface.Value) ?? Surfaces[0];
        foreach (var entry in _logs.GetEntries())
        {
            AppendConsoleLine(FormatLogEntry(entry));
        }
    }

    public ObservableCollection<NavigationItemViewModel> Surfaces { get; }

    public ObservableCollection<ToolSurfaceItemViewModel> Tools { get; }

    public IEnumerable<NavigationItemViewModel> SecondarySurfaces => Surfaces.Where(surface => surface.Kind != SurfaceKind.Lab);

    [ObservableProperty]
    private NavigationItemViewModel? _selectedSurface;

    [ObservableProperty]
    private bool _isConsoleOpen;

    [ObservableProperty]
    private string _shortRunId = "RID inactive";

    public string AppStatusText => "Online";

    public string ModeLabel => "Local First";

    public string AgentLabel => _harnesses.ShellStatusText;

    public string VersionLabel => "v1.0.0-alpha.1";

    public string BranchLabel => "main";

    public string WorkspaceStatusText => $"Workspace: {_lab.ActiveWorkspaceName}";

    public string FooterWorkspaceText => _lab.ActiveWorkspaceName;

    public string ModelStatusText => _models.ShellStatusText;

    public string RouteStatusText => _router.ShellStatusText;

    public string QueueSummaryText => _router.QueueSummaryText;

    public string QueueRunningText => _router.QueueRunningText;

    public string QueueQueuedText => _router.QueueQueuedText;

    public string RouteEndpointStatusText => _router.EndpointStatusText;

    public string RouteHealthStatusText => _router.HealthStatusText;

    public string ModelRuntimeStatusText => _models.RuntimeStatusText;

    public string ModelContextStatusText => _models.ContextStatusText;

    public string ToolSurfaceSummaryText => $"{Tools.Count} tool contracts staged";

    public string HarnessStatusText => _harnesses.ShellStatusText;

    public string SetupReadinessText => BuildSetupReadinessText();

    public string SetupActionText => BuildSetupActionText();

    public string ShortSessionId => "SID v1-local";

    public string SessionSummaryText => _lab.SessionName;

    public string ConsoleCountText => ConsoleLines.Count.ToString();

    public ObservableCollection<string> ConsoleLines { get; } =
    [
        "V1 shell loaded with Lab + Workspaces first-slice services.",
        "Lab and Workspaces slice is backed by V1-native services.",
        "RPC infrastructure is hidden from primary navigation.",
        "Router is the visible request path surface.",
        "Model endpoints, hosted harnesses, router availability, and validation jobs are service-backed."
    ];

    partial void OnSelectedSurfaceChanged(NavigationItemViewModel? value)
    {
        foreach (var item in Surfaces)
        {
            item.IsSelected = item == value;
        }
    }

    public string SurfaceListText => string.Join(" / ", Surfaces.Select(surface => surface.Name));

    [RelayCommand]
    private void SelectLab()
    {
        SelectedSurface = Surfaces.First(surface => surface.Kind == SurfaceKind.Lab);
    }

    [RelayCommand]
    public async Task ResolveSetupAsync()
    {
        if (ModelStatusText.Contains("inactive", System.StringComparison.OrdinalIgnoreCase))
        {
            SelectSurface(SurfaceKind.Models);
            return;
        }

        if (HarnessStatusText.Contains("inactive", System.StringComparison.OrdinalIgnoreCase))
        {
            SelectSurface(SurfaceKind.Agents);
            return;
        }

        if (RouteStatusText.Contains("inactive", System.StringComparison.OrdinalIgnoreCase)
            || (HarnessStatusText.Contains("Conversation Harness", System.StringComparison.OrdinalIgnoreCase)
                && !RouteStatusText.Contains("Conversation Harness", System.StringComparison.OrdinalIgnoreCase)))
        {
            await _router.SelectBestRouteAsync();
            return;
        }

        SelectSurface(SurfaceKind.Lab);
    }

    private void SelectSurface(SurfaceKind kind)
    {
        SelectedSurface = Surfaces.First(surface => surface.Kind == kind);
    }

    private void OnLabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LabViewModel.ActiveWorkspaceName))
        {
            OnPropertyChanged(nameof(WorkspaceStatusText));
            OnPropertyChanged(nameof(FooterWorkspaceText));
        }

        if (e.PropertyName == nameof(LabViewModel.SessionName))
        {
            OnPropertyChanged(nameof(SessionSummaryText));
        }

        if (e.PropertyName == nameof(LabViewModel.StatusLabel))
        {
            AppendConsoleLine($"lab: {_lab.StatusLabel}");
        }
    }

    private void OnModelsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ModelsViewModel.ShellStatusText) or nameof(ModelsViewModel.RuntimeStatusText) or nameof(ModelsViewModel.ContextStatusText))
        {
            OnPropertyChanged(nameof(ModelStatusText));
            OnPropertyChanged(nameof(ModelRuntimeStatusText));
            OnPropertyChanged(nameof(ModelContextStatusText));
            OnPropertyChanged(nameof(SetupReadinessText));
            OnPropertyChanged(nameof(SetupActionText));
        }
    }

    private void OnRouterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RouterViewModel.ShellStatusText)
            or nameof(RouterViewModel.QueueSummaryText)
            or nameof(RouterViewModel.QueueRunningText)
            or nameof(RouterViewModel.QueueQueuedText)
            or nameof(RouterViewModel.EndpointStatusText)
            or nameof(RouterViewModel.HealthStatusText))
        {
            OnPropertyChanged(nameof(RouteStatusText));
            OnPropertyChanged(nameof(QueueSummaryText));
            OnPropertyChanged(nameof(QueueRunningText));
            OnPropertyChanged(nameof(QueueQueuedText));
            OnPropertyChanged(nameof(RouteEndpointStatusText));
            OnPropertyChanged(nameof(RouteHealthStatusText));
            OnPropertyChanged(nameof(SetupReadinessText));
            OnPropertyChanged(nameof(SetupActionText));
        }
    }

    private void OnHarnessesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HarnessImporterViewModel.ShellStatusText))
        {
            OnPropertyChanged(nameof(AgentLabel));
            OnPropertyChanged(nameof(HarnessStatusText));
            OnPropertyChanged(nameof(SetupReadinessText));
            OnPropertyChanged(nameof(SetupActionText));
        }
    }

    private string BuildSetupReadinessText()
    {
        if (ModelStatusText.Contains("inactive", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Setup: add and probe LM Studio";
        }

        if (HarnessStatusText.Contains("inactive", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Setup: import hosted harness";
        }

        if (RouteStatusText.Contains("inactive", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Setup: select best route";
        }

        if (HarnessStatusText.Contains("Conversation Harness", System.StringComparison.OrdinalIgnoreCase)
            && !RouteStatusText.Contains("Conversation Harness", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Setup: select hosted route";
        }

        return "Ready: hosted route + validation";
    }

    private string BuildSetupActionText()
    {
        if (ModelStatusText.Contains("inactive", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Models";
        }

        if (HarnessStatusText.Contains("inactive", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Agents";
        }

        if (RouteStatusText.Contains("inactive", System.StringComparison.OrdinalIgnoreCase)
            || (HarnessStatusText.Contains("Conversation Harness", System.StringComparison.OrdinalIgnoreCase)
                && !RouteStatusText.Contains("Conversation Harness", System.StringComparison.OrdinalIgnoreCase)))
        {
            return "Use best route";
        }

        return "Lab";
    }

    private void OnLogEntryAdded(AppLogEntry entry)
    {
        AppendConsoleLine(FormatLogEntry(entry));
    }

    private void OnExecutionEventPublished(ExecutionEvent entry)
    {
        ShortRunId = $"RID {entry.EventId[..6]}";
        AppendConsoleLine($"event/{entry.Kind}: Using {entry.ToolName} - {entry.Message}");
    }

    private void AppendConsoleLine(string line)
    {
        void Append()
        {
            ConsoleLines.Add(line);
            while (ConsoleLines.Count > 80)
            {
                ConsoleLines.RemoveAt(0);
            }

            OnPropertyChanged(nameof(ConsoleCountText));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Append();
            return;
        }

        Dispatcher.UIThread.Post(Append);
    }

    private static string FormatLogEntry(AppLogEntry entry)
    {
        return $"{entry.Level}/{entry.Source}: {entry.Message}";
    }
}
