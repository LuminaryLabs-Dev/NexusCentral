using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexusCentral.V1.Core.Execution;

namespace NexusCentral.V1.Features.Models;

public partial class ModelsViewModel : ObservableObject
{
    private readonly IModelEndpointService _service;
    private bool _isRendering;
    private ModelEndpointSnapshot _snapshot;

    public ModelsViewModel(IModelEndpointService service, IHuggingFaceModelSearchService searchService)
    {
        _service = service;
        _snapshot = _service.GetSnapshot();
        HubSearch = new HuggingFaceSearchViewModel(searchService);
        _service.StateChanged += Render;
        Render(_snapshot);
        _ = ProbeActiveEndpointIfUnprobedAsync();
    }

    public ObservableCollection<ModelEndpointItemViewModel> Endpoints { get; } = new();

    public HuggingFaceSearchViewModel HubSearch { get; }

    [ObservableProperty]
    private ModelEndpointItemViewModel? _selectedEndpoint;

    [ObservableProperty]
    private string _statusLabel = "Inactive";

    [ObservableProperty]
    private string _newEndpointName = "LM Studio LAN";

    [ObservableProperty]
    private string _newEndpointBaseUrl = "http://10.0.0.38:1234/v1";

    [ObservableProperty]
    private string _newEndpointModelId = "lfm2.5-8b-a1b";

    [ObservableProperty]
    private string _newEndpointStatusText = "Add an endpoint, then probe it. No models are downloaded.";

    public string CapabilitySummary => Endpoints.Count == 0
        ? "No model endpoints are registered. Add endpoints through the model service or test fixtures."
        : $"{Endpoints.Count} model endpoint records";

    public string ShellStatusText => _snapshot.ModelStatusText;

    public string RuntimeStatusText => _snapshot.RuntimeStatusText;

    public string ContextStatusText => _snapshot.ContextStatusText;

    public string ActiveEndpointTitle => SelectedEndpoint?.Name ?? "No model endpoint";

    public string ActiveEndpointDetail => SelectedEndpoint?.StatusMessage ?? "Model routing is inactive until an endpoint is registered and probed.";

    public string ActiveEndpointBaseUrl => SelectedEndpoint?.BaseUrl ?? "Base URL: none";

    public string ActiveEndpointModelText => SelectedEndpoint?.SelectedModelIdText ?? "Selected model: none";

    public string ActiveEndpointHealthText => SelectedEndpoint?.HealthLabel ?? "Inactive";

    public string ActiveEndpointModelCountText => SelectedEndpoint?.ModelCountText ?? "0 models";

    public string ActiveEndpointLastProbeText => SelectedEndpoint?.LastProbeText ?? "Never probed";

    [RelayCommand]
    public async Task ProbeSelectedEndpointAsync()
    {
        if (SelectedEndpoint is null)
        {
            return;
        }

        StatusLabel = "Probing";
        await _service.ProbeEndpointAsync(SelectedEndpoint.Id);
    }

    private async Task ProbeActiveEndpointIfUnprobedAsync()
    {
        var active = _service.GetSnapshot().ActiveEndpoint;
        if (active is null || active.Health != ModelEndpointHealth.Unknown)
        {
            return;
        }

        StatusLabel = "Probing";
        await _service.ProbeEndpointAsync(active.Id);
    }

    [RelayCommand]
    public async Task AddEndpointAsync()
    {
        try
        {
            StatusLabel = "Adding";
            var endpoint = await _service.AddOrUpdateEndpointAsync(new ModelEndpointCreateRequest(
                null,
                NewEndpointName,
                ProviderKind.LmStudio,
                NewEndpointBaseUrl,
                NewEndpointModelId));

            await _service.SelectEndpointAsync(endpoint.Id);
            NewEndpointStatusText = $"Added {endpoint.Name}. Probe to verify availability.";
        }
        catch (Exception exc)
        {
            StatusLabel = "Invalid";
            NewEndpointStatusText = exc.Message;
        }
    }

    [RelayCommand]
    public async Task RemoveSelectedEndpointAsync()
    {
        if (SelectedEndpoint is null)
        {
            return;
        }

        StatusLabel = "Removing";
        var removedName = SelectedEndpoint.Name;
        if (await _service.RemoveEndpointAsync(SelectedEndpoint.Id))
        {
            NewEndpointStatusText = $"Removed {removedName}.";
        }
    }

    [RelayCommand]
    public void Refresh()
    {
        Render(_service.GetSnapshot());
    }

    partial void OnSelectedEndpointChanged(ModelEndpointItemViewModel? value)
    {
        OnPropertyChanged(nameof(ActiveEndpointTitle));
        OnPropertyChanged(nameof(ActiveEndpointDetail));
        OnPropertyChanged(nameof(ActiveEndpointBaseUrl));
        OnPropertyChanged(nameof(ActiveEndpointModelText));
        OnPropertyChanged(nameof(ActiveEndpointHealthText));
        OnPropertyChanged(nameof(ActiveEndpointModelCountText));
        OnPropertyChanged(nameof(ActiveEndpointLastProbeText));

        if (!_isRendering && value is not null)
        {
            _ = _service.SelectEndpointAsync(value.Id);
        }
    }

    private void Render(ModelEndpointSnapshot snapshot)
    {
        _snapshot = snapshot;
        _isRendering = true;
        Endpoints.Clear();

        foreach (var endpoint in snapshot.Endpoints.OrderBy(endpoint => endpoint.Name))
        {
            Endpoints.Add(ModelEndpointItemViewModel.FromModel(endpoint, snapshot.ActiveEndpoint?.Id));
        }

        SelectedEndpoint = Endpoints.FirstOrDefault(endpoint => endpoint.IsActive)
            ?? Endpoints.FirstOrDefault();
        StatusLabel = snapshot.ActiveEndpoint?.Health.ToString() ?? "Inactive";
        _isRendering = false;

        OnPropertyChanged(nameof(CapabilitySummary));
        OnPropertyChanged(nameof(ShellStatusText));
        OnPropertyChanged(nameof(RuntimeStatusText));
        OnPropertyChanged(nameof(ContextStatusText));
        OnPropertyChanged(nameof(ActiveEndpointTitle));
        OnPropertyChanged(nameof(ActiveEndpointDetail));
        OnPropertyChanged(nameof(ActiveEndpointBaseUrl));
        OnPropertyChanged(nameof(ActiveEndpointModelText));
        OnPropertyChanged(nameof(ActiveEndpointHealthText));
        OnPropertyChanged(nameof(ActiveEndpointModelCountText));
        OnPropertyChanged(nameof(ActiveEndpointLastProbeText));
    }
}

public sealed class ModelEndpointItemViewModel
{
    private ModelEndpointItemViewModel(ModelEndpointDefinition endpoint, string? activeEndpointId)
    {
        Id = endpoint.Id;
        Name = endpoint.Name;
        ProviderLabel = endpoint.ProviderKind.ToString();
        BaseUrl = $"Base URL: {endpoint.BaseUrl}";
        SelectedModelIdText = string.IsNullOrWhiteSpace(endpoint.SelectedModelId)
            ? "Selected model: none"
            : $"Selected model: {endpoint.SelectedModelId}";
        HealthLabel = endpoint.Health.ToString();
        StatusMessage = endpoint.StatusMessage;
        ModelCountText = $"{endpoint.AvailableModelIds.Count} models";
        LastProbeText = endpoint.LastProbeUtc is null
            ? "Never probed"
            : $"Last probe: {endpoint.LastProbeUtc.Value.LocalDateTime:g}";
        DisplayName = string.Equals(endpoint.Id, activeEndpointId, StringComparison.OrdinalIgnoreCase)
            ? $"{endpoint.Name}  active"
            : endpoint.Name;
        IsActive = string.Equals(endpoint.Id, activeEndpointId, StringComparison.OrdinalIgnoreCase);
    }

    public string Id { get; }

    public string Name { get; }

    public string DisplayName { get; }

    public string ProviderLabel { get; }

    public string BaseUrl { get; }

    public string SelectedModelIdText { get; }

    public string HealthLabel { get; }

    public string StatusMessage { get; }

    public string ModelCountText { get; }

    public string LastProbeText { get; }

    public bool IsActive { get; }

    public static ModelEndpointItemViewModel FromModel(ModelEndpointDefinition endpoint, string? activeEndpointId) => new(endpoint, activeEndpointId);
}
