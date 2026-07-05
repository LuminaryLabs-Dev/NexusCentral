using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NexusCentral.V1.Features.Router;

public partial class RouterViewModel : ObservableObject
{
    private readonly IRouterService _service;
    private bool _isRendering;
    private RouterSnapshot _snapshot;

    public RouterViewModel(IRouterService service)
    {
        _service = service;
        _snapshot = _service.GetSnapshot();
        _service.StateChanged += Render;
        Render(_snapshot);
    }

    public ObservableCollection<RouterRouteItemViewModel> Routes { get; } = new();

    [ObservableProperty]
    private RouterRouteItemViewModel? _selectedRoute;

    [ObservableProperty]
    private string _statusLabel = "Inactive";

    [ObservableProperty]
    private string _routeSmokePrompt = "Return exactly four short labels: Plan, Evidence, Risk, Project.";

    [ObservableProperty]
    private string _routeSmokeStatusText = "No route smoke run.";

    public string CapabilitySummary => "Routes are derived from backed service state: local model endpoints plus the active hosted harness. Peer routing is not active in this slice.";

    public string ShellStatusText => _snapshot.RouteStatusText;

    public string QueueSummaryText => _snapshot.QueueSummaryText;

    public string EndpointStatusText => _snapshot.EndpointStatusText;

    public string HealthStatusText => _snapshot.HealthStatusText;

    public string QueueRunningText => SelectedRoute?.RunningText ?? "0 running";

    public string QueueQueuedText => SelectedRoute?.QueueText ?? "0 queued";

    public string ActiveRouteTitle => SelectedRoute?.Name ?? "No route";

    public string ActiveRouteDetail => SelectedRoute?.StatusMessage ?? "No backed route is available.";

    public string ActiveRouteTargetText => SelectedRoute?.TargetText ?? "Target: none";

    public string ActiveRouteHealthText => SelectedRoute?.AvailabilityLabel ?? "Inactive";

    public string ActiveRouteQueueText => SelectedRoute?.QueueText ?? "Queue inactive";

    public string ActiveRouteCapacityText => SelectedRoute?.CapacityText ?? "0";

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await _service.RefreshAsync();
    }

    [RelayCommand]
    public async Task SelectBestRouteAsync()
    {
        StatusLabel = "Selecting";
        var snapshot = await _service.SelectBestAvailableRouteAsync();
        StatusLabel = snapshot.ActiveRoute?.Availability.ToString() ?? "Inactive";
        RouteSmokeStatusText = snapshot.ActiveRoute is null
            ? "No route could be selected."
            : $"Selected {snapshot.ActiveRoute.Name}: {snapshot.ActiveRoute.StatusMessage}";
    }

    [RelayCommand]
    public async Task SmokeSelectedRouteAsync()
    {
        StatusLabel = "Testing";
        var result = await _service.SmokeSelectedRouteAsync(RouteSmokePrompt);
        RouteSmokeStatusText = result.Ok
            ? $"{result.Message} Answer: {result.Answer}"
            : result.Message;
        StatusLabel = result.Ok ? "Available" : "Inactive";
    }

    partial void OnSelectedRouteChanged(RouterRouteItemViewModel? value)
    {
        OnPropertyChanged(nameof(QueueRunningText));
        OnPropertyChanged(nameof(QueueQueuedText));
        OnPropertyChanged(nameof(ActiveRouteTitle));
        OnPropertyChanged(nameof(ActiveRouteDetail));
        OnPropertyChanged(nameof(ActiveRouteTargetText));
        OnPropertyChanged(nameof(ActiveRouteHealthText));
        OnPropertyChanged(nameof(ActiveRouteQueueText));
        OnPropertyChanged(nameof(ActiveRouteCapacityText));

        if (!_isRendering && value is not null)
        {
            _ = _service.SelectRouteAsync(value.Id);
        }
    }

    private void Render(RouterSnapshot snapshot)
    {
        _snapshot = snapshot;
        _isRendering = true;
        Routes.Clear();

        foreach (var route in snapshot.Routes.OrderBy(route => route.Name))
        {
            Routes.Add(RouterRouteItemViewModel.FromModel(route, snapshot.ActiveRoute?.Id));
        }

        SelectedRoute = Routes.FirstOrDefault(route => route.IsActive)
            ?? Routes.FirstOrDefault();
        StatusLabel = snapshot.ActiveRoute?.Availability.ToString() ?? "Inactive";
        _isRendering = false;

        OnPropertyChanged(nameof(ShellStatusText));
        OnPropertyChanged(nameof(QueueSummaryText));
        OnPropertyChanged(nameof(EndpointStatusText));
        OnPropertyChanged(nameof(HealthStatusText));
        OnPropertyChanged(nameof(QueueRunningText));
        OnPropertyChanged(nameof(QueueQueuedText));
        OnPropertyChanged(nameof(ActiveRouteTitle));
        OnPropertyChanged(nameof(ActiveRouteDetail));
        OnPropertyChanged(nameof(ActiveRouteTargetText));
        OnPropertyChanged(nameof(ActiveRouteHealthText));
        OnPropertyChanged(nameof(ActiveRouteQueueText));
        OnPropertyChanged(nameof(ActiveRouteCapacityText));
    }
}

public sealed class RouterRouteItemViewModel
{
    private RouterRouteItemViewModel(RouterRouteState route, string? activeRouteId)
    {
        Id = route.Id;
        Name = route.Name;
        DisplayName = string.Equals(route.Id, activeRouteId, StringComparison.OrdinalIgnoreCase)
            ? $"{route.Name}  active"
            : route.Name;
        TargetText = string.IsNullOrWhiteSpace(route.EndpointName)
            ? "Target: none"
            : $"Target: {route.EndpointName} / {route.TargetModelId}";
        TargetKind = route.TargetKind;
        AvailabilityLabel = route.Availability.ToString();
        QueueText = route.Availability == RouterAvailability.Available
            ? $"{route.QueueDepth} queued"
            : "Queue inactive";
        RunningText = "0 running";
        CapacityText = route.Capacity > 0 ? $"{route.Capacity} open" : "0";
        StatusMessage = route.StatusMessage;
        LastCheckedText = route.LastCheckedUtc is null
            ? "Never checked"
            : $"Last check: {route.LastCheckedUtc.Value.LocalDateTime:g}";
        IsActive = string.Equals(route.Id, activeRouteId, StringComparison.OrdinalIgnoreCase);
    }

    public string Id { get; }

    public string Name { get; }

    public string DisplayName { get; }

    public string TargetText { get; }

    public string TargetKind { get; }

    public string AvailabilityLabel { get; }

    public string QueueText { get; }

    public string RunningText { get; }

    public string CapacityText { get; }

    public string StatusMessage { get; }

    public string LastCheckedText { get; }

    public bool IsActive { get; }

    public static RouterRouteItemViewModel FromModel(RouterRouteState route, string? activeRouteId) => new(route, activeRouteId);
}
