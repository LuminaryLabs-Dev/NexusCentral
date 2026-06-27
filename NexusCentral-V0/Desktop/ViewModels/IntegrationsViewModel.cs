using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using Avalonia.Threading;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.ViewModels;

public partial class IntegrationEntryViewModel : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _status = "IDLE"; // IDLE, SETUP, NO

    public IntegrationEntryViewModel(string id, string name, string description, bool enabled, string status)
    {
        Id = id;
        Name = name;
        Description = description;
        IsEnabled = enabled;
        Status = status;
    }
}

public partial class IntegrationToolViewModel : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string AuthScope { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private int _usageCount;

    public IntegrationToolViewModel(string name, string description, string route, string method, string authScope, bool enabled, int usageCount = 0)
    {
        Name = name;
        Description = description;
        Route = route;
        Method = method;
        AuthScope = authScope;
        IsEnabled = enabled;
        UsageCount = usageCount;
    }
}

public partial class IntegrationsViewModel : ViewModelBase
{
    private readonly AppServices _services;

    [ObservableProperty]
    private string _host = "127.0.0.1";

    [ObservableProperty]
    private int _port = 3002;

    [ObservableProperty]
    private bool _isLan;

    [ObservableProperty]
    private string _statusText = "● Integrations are managed by the desktop app lifecycle.";

    [ObservableProperty]
    private bool _isRunning = true;

    [ObservableProperty]
    private int _activeSessions = 0;

    [ObservableProperty]
    private string _backendUrl = "http://127.0.0.1:8000";

    public ObservableCollection<IntegrationEntryViewModel> Integrations { get; } = new();
    public ObservableCollection<IntegrationToolViewModel> Tools { get; } = new();
    public ObservableCollection<string> RecentLogs { get; } = new();

    public IntegrationsViewModel(AppServices services)
    {
        _services = services;
        RefreshRegistry();
        foreach (var logEvent in _services.Logs.GetEvents().TakeLast(15))
        {
            RecentLogs.Add(FormatLog(logEvent.Source, logEvent.Message));
        }
        _services.Logs.LogAppended += logEvent =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                RecentLogs.Add(FormatLog(logEvent.Source, logEvent.Message));
                while (RecentLogs.Count > 50)
                {
                    RecentLogs.RemoveAt(0);
                }
            });
        };
    }

    private string GetWsUrl() => $"ws://{Host}:{Port}/ws";

    [RelayCommand]
    private void Start()
    {
        IsRunning = true;
        StatusText = "● Integrations are managed by the desktop app lifecycle.";
    }

    [RelayCommand]
    private void Stop()
    {
        IsRunning = true;
        StatusText = "● Integrations remain active while the desktop app is running.";
    }

    private void UpdateStatusText()
    {
        if (IsRunning)
        {
            StatusText = $"● Running (pid 1337) | {GetWsUrl()} | backend {BackendUrl} | sessions {ActiveSessions}";
        }
        else
        {
            StatusText = $"● Stopped | backend {BackendUrl}";
        }
    }

    [RelayCommand]
    private void Check()
    {
        RefreshRegistry();
        StatusText = $"● Registered {_services.Integrations.GetTools("nexuscentral").Count} RPC tools.";
    }

    private void RefreshRegistry()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Integrations.Clear();
            Tools.Clear();

            foreach (var integration in _services.Integrations.GetIntegrations())
            {
                Integrations.Add(new IntegrationEntryViewModel(
                    integration.Id,
                    integration.Name,
                    integration.Description,
                    integration.Enabled,
                    integration.Status));

                foreach (var tool in integration.Tools)
                {
                    Tools.Add(new IntegrationToolViewModel(
                        tool.Name,
                        tool.Description,
                        tool.Route,
                        tool.Method,
                        tool.AuthScope,
                        tool.Enabled,
                        0));
                }
            }
        });
    }

    private static string FormatLog(string source, string message)
    {
        return $"[{source}] {message}";
    }
}
