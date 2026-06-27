using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const string LabTabName = "Lab";
    private const string SettingsTabName = "Settings";
    private readonly AppServices _services;
    private readonly ChatViewModel _chat;
    private readonly NavButtonViewModel _settingsNavButton;

    [ObservableProperty]
    private string _statusText = "● Disconnected";

    [ObservableProperty]
    private string _summaryText = "Supervisor v0.0.0 | Uptime: 0s";

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private MonitorViewModel _monitor;

    private readonly System.Timers.Timer _pollTimer;
    private int _connectStep;
    private DateTimeOffset? _connectedAtUtc;

    public ObservableCollection<NavButtonViewModel> NavButtons { get; } = new();
    public ChatViewModel Chat => _chat;
    public SettingsViewModel Settings { get; }

    public MainViewModel(AppServices services)
    {
        _services = services;
        _monitor = new MonitorViewModel(_services);
        var chatViewModel = new ChatViewModel(_services);
        _chat = chatViewModel;
        var workflowViewModel = new WorkflowViewModel(_services);
        var automationsViewModel = new AutomationsViewModel(_services);
        var modelsViewModel = new ModelsViewModel(_services);
        var settingsViewModel = new SettingsViewModel(_services);
        _services.RegisterChat(chatViewModel);
        _services.RegisterWorkflowViewModel(workflowViewModel);
        _services.RegisterAutomationsViewModel(automationsViewModel);
        Settings = settingsViewModel;

        NavButtons.Add(new NavButtonViewModel(LabTabName, chatViewModel));
        NavButtons.Add(new NavButtonViewModel("Automations", automationsViewModel));
        NavButtons.Add(new NavButtonViewModel("Models", modelsViewModel));
        NavButtons.Add(new NavButtonViewModel("Router", new RouterViewModel(_services)));
        NavButtons.Add(new NavButtonViewModel("Agents", new AgentsViewModel(_services)));
        NavButtons.Add(new NavButtonViewModel("Integrations", new IntegrationsViewModel(_services)));
        _settingsNavButton = new NavButtonViewModel(SettingsTabName, settingsViewModel);
        NavButtons.Add(_settingsNavButton);

        _services.AppState.SelectTabHandler = SelectTabByName;
        SetPage(NavButtons[0]);
        _services.AppState.RegisterTabs(NavButtons.Select(x => x.Name), NavButtons[0].Name);

        _pollTimer = new System.Timers.Timer(1000);
        _pollTimer.Elapsed += OnPollTick;
        _pollTimer.Start();
    }

    [RelayCommand]
    private void OpenSettingsPage()
    {
        SetPage(_settingsNavButton);
    }

    private void OnPollTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_connectStep < 2)
        {
            _connectStep++;
            StatusText = _connectStep == 1 ? "● Connecting" : "● Online";
            if (_connectStep == 2)
            {
                _connectedAtUtc = DateTimeOffset.UtcNow;
                _pollTimer.Interval = 5000;
            }
        }

        if (StatusText == "● Online" && _connectedAtUtc is not null)
        {
            var uptimeSeconds = Math.Max(0, (int)Math.Floor((DateTimeOffset.UtcNow - _connectedAtUtc.Value).TotalSeconds));
            var nextSummary = $"Supervisor v1.2.4 | Uptime: {uptimeSeconds}s";
            if (!string.Equals(SummaryText, nextSummary, System.StringComparison.Ordinal))
            {
                SummaryText = nextSummary;
            }
        }

        Monitor.UpdateStatus(StatusText, SummaryText, string.Empty);
        _services.AppState.UpdateStatus(StatusText, SummaryText);
    }

    public void SetPage(NavButtonViewModel btn)
    {
        foreach (var navBtn in NavButtons)
        {
            navBtn.IsSelected = (navBtn == btn);
        }
        CurrentPage = btn.ViewModel;
        _services.AppState.SetCurrentTab(btn.Name);
    }

    private bool SelectTabByName(string tabName)
    {
        var candidate = NavButtons.FirstOrDefault(x =>
            (string.Equals(tabName, "Chat", StringComparison.OrdinalIgnoreCase) && string.Equals(x.Name, LabTabName, StringComparison.OrdinalIgnoreCase)) ||
            string.Equals(x.Name, tabName, System.StringComparison.OrdinalIgnoreCase) ||
            x.Name.Contains(tabName, System.StringComparison.OrdinalIgnoreCase));

        if (candidate is null)
        {
            return false;
        }

        SetPage(candidate);
        return true;
    }
}

public partial class NavButtonViewModel : ObservableObject
{
    public string Name { get; }
    public ViewModelBase ViewModel { get; }

    [ObservableProperty]
    private bool _isSelected;

    public NavButtonViewModel(string name, ViewModelBase viewModel, bool isSelected = false)
    {
        Name = name;
        ViewModel = viewModel;
        IsSelected = isSelected;
    }
}
