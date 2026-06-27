using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexusCentral.V3.Core.Execution;
using NexusCentral.V3.Core.Logging;
using NexusCentral.V3.Features.Lab;

namespace NexusCentral.V3.Desktop.ViewModels;

public partial class MainShellViewModel : ObservableObject
{
    private readonly LabViewModel _lab;
    private readonly IAppLogService _logs;
    private readonly IExecutionEventStream _executionEvents;

    public MainShellViewModel(LabViewModel lab, IAppLogService logs, IExecutionEventStream executionEvents)
    {
        _lab = lab;
        _logs = logs;
        _executionEvents = executionEvents;
        _lab.PropertyChanged += OnLabPropertyChanged;
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
                "Planned harnesses, wrappers, templates, and policy for how work is executed.",
                "AG",
                "Planned",
                "Read-only planned surface. Wrapper execution and tool permissions are not active in this slice.",
                "Add harness",
                "View templates",
                Tools),
            new(
                SurfaceKind.Router,
                "Router",
                "Planned request routing across local endpoints, provider endpoints, and trusted NexusCentral queues.",
                "RT",
                "Inactive",
                "Read-only planned surface. Router queues and provider fallback are not active in this slice.",
                "Add router",
                "Activate"),
            new(
                SurfaceKind.Models,
                "Models",
                "Planned model download and runtime control.",
                "MO",
                "Inactive",
                "Read-only planned surface. Hugging Face downloads and runtime loading are not active in this slice.",
                "Add model",
                "Download"),
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

        SelectedSurface = Surfaces[0];
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

    public string AgentLabel => "Agent: Orchestrator";

    public string VersionLabel => "v3.0.0-alpha.1";

    public string BranchLabel => "main";

    public string WorkspaceStatusText => $"Workspace: {_lab.ActiveWorkspaceName}";

    public string FooterWorkspaceText => _lab.ActiveWorkspaceName;

    public string ModelStatusText => "Model: inactive";

    public string RouteStatusText => "Route: inactive";

    public string QueueSummaryText => "Queue inactive";

    public string ToolSurfaceSummaryText => $"{Tools.Count} tool contracts staged";

    public string ShortSessionId => "SID v3-local";

    public string SessionSummaryText => _lab.SessionName;

    public string ConsoleCountText => ConsoleLines.Count.ToString();

    public ObservableCollection<string> ConsoleLines { get; } =
    [
        "V3 shell loaded with Lab + Workspaces first-slice services.",
        "Lab and Workspaces slice is backed by V3-native services.",
        "RPC infrastructure is hidden from primary navigation.",
        "Router is the visible request path surface.",
        "No provider, model runtime, automation, or routing behavior is active yet."
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
