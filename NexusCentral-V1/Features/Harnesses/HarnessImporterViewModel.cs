using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NexusCentral.V1.Features.Harnesses;

public partial class HarnessImporterViewModel : ObservableObject
{
    private readonly IHarnessImporterService _service;
    private readonly IHostedHarnessTurnRunner? _turnRunner;
    private readonly IHarnessOrchestrationSmokeService? _orchestrationSmoke;
    private bool _isRendering;
    private HarnessImporterSnapshot _snapshot;

    public HarnessImporterViewModel(
        IHarnessImporterService service,
        IHostedHarnessTurnRunner? turnRunner = null,
        IHarnessOrchestrationSmokeService? orchestrationSmoke = null)
    {
        _service = service;
        _turnRunner = turnRunner;
        _orchestrationSmoke = orchestrationSmoke;
        _snapshot = _service.GetSnapshot();
        _service.StateChanged += Render;
        Render(_snapshot);
    }

    public ObservableCollection<HarnessItemViewModel> Harnesses { get; } = new();

    public ObservableCollection<HarnessCapabilityItemViewModel> Capabilities { get; } = new();

    public ObservableCollection<HarnessEndpointItemViewModel> ProposedEndpoints { get; } = new();

    public ObservableCollection<HarnessManifestCandidateItemViewModel> ManifestCandidates { get; } = new();

    public ObservableCollection<HarnessOrchestrationStepItemViewModel> OrchestrationSteps { get; } = new();

    [ObservableProperty]
    private HarnessItemViewModel? _selectedHarness;

    [ObservableProperty]
    private HarnessManifestCandidateItemViewModel? _selectedManifestCandidate;

    [ObservableProperty]
    private string _statusLabel = "Inactive";

    [ObservableProperty]
    private string _newHarnessName = "Conversation Harness";

    [ObservableProperty]
    private string _newHarnessCommand = "cd /Users/crimsonwheeler/Documents/GitHub/NexusAgent/Conversation-Harness && CONVERSATION_HARNESS_BASE_URL=http://10.0.0.38:1234 CONVERSATION_HARNESS_MODEL=lfm2.5-8b-a1b CONVERSATION_HARNESS_MAX_TOKENS=16000 PYTHONPATH=src python3 -m ConversationHarness.cli chat-turn";

    [ObservableProperty]
    private string _newHarnessRoute = "/harnesses/conversation-harness/chat-turn";

    [ObservableProperty]
    private string _newHarnessStatusText = "Attach a hosted harness command. The harness owns its adapter.";

    [ObservableProperty]
    private string _newHarnessManifestPath = "/Users/crimsonwheeler/Documents/GitHub/NexusAgent/Conversation-Harness/harness.manifest.json";

    [ObservableProperty]
    private string _manifestSearchRoot = "/Users/crimsonwheeler/Documents/GitHub/NexusAgent";

    [ObservableProperty]
    private string _manifestDiscoveryStatusText = "Scan a harness folder to import manifests.";

    [ObservableProperty]
    private string _harnessTestPrompt = "Reply with exactly: harness ready";

    [ObservableProperty]
    private string _harnessTestStatusText = "No harness test run.";

    [ObservableProperty]
    private string _orchestrationSmokePrompt = "Reply with exactly: orchestration ready";

    [ObservableProperty]
    private string _orchestrationSmokeStatusText = "No orchestration smoke run.";

    public string ShellStatusText => _snapshot.HarnessStatusText;

    public string CapabilitySummary => Harnesses.Count == 0
        ? "Import a harness before it can own any work."
        : _snapshot.CapabilitySummaryText;

    public string WrapperSummaryText => _snapshot.WrapperSummaryText;

    public string OrchestrationSummaryText => _snapshot.OrchestrationSummaryText;

    public string LastOrchestrationSmokeText => FormatSmokeResult(_orchestrationSmoke?.LastResult);

    public string ActiveHarnessTitle => SelectedHarness?.Name ?? "No harness";

    public string ActiveHarnessDetail => SelectedHarness?.StatusMessage ?? "Discover or import a harness before enabling capabilities.";

    public string ActiveHarnessCommandText => SelectedHarness?.CommandPathText ?? "Command: none";

    public string ActiveHarnessVersionText => SelectedHarness?.VersionText ?? "Version: unavailable";

    public string ActiveHarnessHealthText => SelectedHarness?.HealthLabel ?? "Inactive";

    public string ActiveHarnessCapabilityText => SelectedHarness?.CapabilityCountText ?? "0 enabled";

    public string ActiveHarnessEndpointText => SelectedHarness?.EndpointCountText ?? "0 endpoints";

    [RelayCommand]
    public async Task DiscoverCodexCliAsync()
    {
        StatusLabel = "Discovering";
        await _service.DiscoverCodexCliAsync();
    }

    [RelayCommand]
    public async Task AttachManualHarnessAsync()
    {
        try
        {
            StatusLabel = "Attaching";
            var id = CreateStableHarnessId(NewHarnessName);
            var harness = await _service.ImportHarnessAsync(new HarnessImportRequest(
                id,
                NewHarnessName,
                HarnessKind.Manual,
                NewHarnessCommand,
                "manual",
                new[]
                {
                    new HarnessCapabilityDefinition(
                        $"{id}.chat",
                        "Chat turn",
                        "Run one hosted harness turn through the attached command.",
                        true,
                        false)
                },
                new[]
                {
                    new HarnessEndpointDefinition(
                        "POST",
                        NewHarnessRoute,
                        $"{id}.chat",
                        "safe",
                        "Send one message to the attached harness.")
                }));

            await _service.SelectHarnessAsync(harness.Id);
            NewHarnessStatusText = $"Attached {harness.Name}.";
        }
        catch (Exception exc)
        {
            StatusLabel = "Invalid";
            NewHarnessStatusText = exc.Message;
        }
    }

    [RelayCommand]
    public async Task ImportManifestAsync()
    {
        try
        {
            StatusLabel = "Importing";
            var harness = await _service.ImportManifestAsync(NewHarnessManifestPath);
            await _service.SelectHarnessAsync(harness.Id);
            NewHarnessStatusText = $"Imported {harness.Name} manifest.";
        }
        catch (Exception exc)
        {
            StatusLabel = "Invalid";
            NewHarnessStatusText = exc.Message;
        }
    }

    [RelayCommand]
    public async Task DiscoverManifestsAsync()
    {
        try
        {
            StatusLabel = "Scanning";
            var candidates = await _service.DiscoverManifestsAsync(ManifestSearchRoot);
            ManifestCandidates.Clear();
            foreach (var candidate in candidates)
            {
                ManifestCandidates.Add(HarnessManifestCandidateItemViewModel.FromModel(candidate));
            }

            SelectedManifestCandidate = ManifestCandidates.FirstOrDefault();
            ManifestDiscoveryStatusText = candidates.Count == 0
                ? "No harness manifests found."
                : $"Found {candidates.Count} harness manifests.";
            StatusLabel = "Discovered";
        }
        catch (Exception exc)
        {
            StatusLabel = "Invalid";
            ManifestDiscoveryStatusText = exc.Message;
        }
    }

    [RelayCommand]
    public async Task ImportSelectedManifestAsync()
    {
        if (SelectedManifestCandidate is null)
        {
            ManifestDiscoveryStatusText = "Select a manifest before importing.";
            return;
        }

        NewHarnessManifestPath = SelectedManifestCandidate.ManifestPath;
        await ImportManifestAsync();
    }

    [RelayCommand]
    public async Task RemoveSelectedHarnessAsync()
    {
        if (SelectedHarness is null)
        {
            return;
        }

        StatusLabel = "Removing";
        var removedName = SelectedHarness.Name;
        if (await _service.RemoveHarnessAsync(SelectedHarness.Id))
        {
            NewHarnessStatusText = $"Removed {removedName}.";
        }
    }

    [RelayCommand]
    public async Task TestSelectedHarnessAsync()
    {
        var harness = SelectedHarness?.Source;
        if (harness is null)
        {
            HarnessTestStatusText = "Select a harness before testing.";
            return;
        }

        if (_turnRunner is null)
        {
            HarnessTestStatusText = "No hosted harness runner is wired.";
            return;
        }

        if (harness.Kind != HarnessKind.Manual)
        {
            HarnessTestStatusText = "Only manual hosted harnesses can be tested here.";
            return;
        }

        StatusLabel = "Testing";
        var result = await _turnRunner.RunTurnAsync(
            harness,
            new HostedHarnessTurnRequest(
                HarnessTestPrompt,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                TimeSpan.FromMinutes(4)));
        HarnessTestStatusText = result.Ok
            ? $"Passed: {result.Assistant}"
            : $"Failed: {result.Message}";
        StatusLabel = result.Ok ? "Discovered" : "Unavailable";
    }

    [RelayCommand]
    public async Task SmokeOrchestrationAsync()
    {
        if (_orchestrationSmoke is null)
        {
            OrchestrationSmokeStatusText = "No orchestration smoke service is wired.";
            return;
        }

        if (_snapshot.OrchestrationSteps.Count == 0)
        {
            OrchestrationSmokeStatusText = "No orchestration steps to smoke.";
            return;
        }

        StatusLabel = "Testing";
        var result = await _orchestrationSmoke.SmokeAsync(new HarnessOrchestrationSmokeRequest(
            OrchestrationSmokePrompt,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            TimeSpan.FromMinutes(4)));
        var skipped = result.Steps.Count(step => !step.Executed);
        OrchestrationSmokeStatusText = $"{result.Message} Skipped: {skipped}.";
        StatusLabel = result.Ok ? "Discovered" : "Unavailable";
        OnPropertyChanged(nameof(LastOrchestrationSmokeText));
    }

    [RelayCommand]
    public void Refresh()
    {
        Render(_service.GetSnapshot());
    }

    partial void OnSelectedHarnessChanged(HarnessItemViewModel? value)
    {
        RenderSelectedHarness(value);

        if (!_isRendering && value is not null)
        {
            _ = _service.SelectHarnessAsync(value.Id);
        }
    }

    private void Render(HarnessImporterSnapshot snapshot)
    {
        _snapshot = snapshot;
        _isRendering = true;
        Harnesses.Clear();
        OrchestrationSteps.Clear();

        foreach (var harness in snapshot.Harnesses.OrderBy(harness => harness.Name))
        {
            Harnesses.Add(HarnessItemViewModel.FromModel(harness, snapshot.ActiveHarness?.Id));
        }

        foreach (var step in snapshot.OrchestrationSteps)
        {
            OrchestrationSteps.Add(HarnessOrchestrationStepItemViewModel.FromModel(step));
        }

        SelectedHarness = Harnesses.FirstOrDefault(harness => harness.IsActive)
            ?? Harnesses.FirstOrDefault();
        StatusLabel = snapshot.ActiveHarness?.Health.ToString() ?? "Inactive";
        _isRendering = false;

        OnPropertyChanged(nameof(ShellStatusText));
        OnPropertyChanged(nameof(CapabilitySummary));
        OnPropertyChanged(nameof(WrapperSummaryText));
        OnPropertyChanged(nameof(OrchestrationSummaryText));
        OnPropertyChanged(nameof(LastOrchestrationSmokeText));
        RenderSelectedHarness(SelectedHarness);
    }

    private static string FormatSmokeResult(HarnessOrchestrationSmokeResult? result)
    {
        if (result is null)
        {
            return "Last smoke: none";
        }

        return $"Last smoke: {result.PassedCount}/{result.ExecutedCount} passed, {result.PlannedCount} planned, {result.CompletedAtUtc.LocalDateTime:g}";
    }

    private static string CreateStableHarnessId(string name)
    {
        var cleaned = new string(name
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());

        cleaned = string.Join("-", cleaned.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(cleaned) ? "manual-harness" : cleaned;
    }

    private void RenderSelectedHarness(HarnessItemViewModel? value)
    {
        Capabilities.Clear();
        ProposedEndpoints.Clear();

        if (value is not null)
        {
            foreach (var capability in value.Source.Capabilities)
            {
                Capabilities.Add(HarnessCapabilityItemViewModel.FromModel(
                    value.Id,
                    capability,
                    (harnessId, capabilityId, enabled) => _service.SetCapabilityEnabledAsync(harnessId, capabilityId, enabled)));
            }

            foreach (var endpoint in value.Source.ProposedEndpoints)
            {
                ProposedEndpoints.Add(HarnessEndpointItemViewModel.FromModel(endpoint));
            }
        }

        OnPropertyChanged(nameof(ActiveHarnessTitle));
        OnPropertyChanged(nameof(ActiveHarnessDetail));
        OnPropertyChanged(nameof(ActiveHarnessCommandText));
        OnPropertyChanged(nameof(ActiveHarnessVersionText));
        OnPropertyChanged(nameof(ActiveHarnessHealthText));
        OnPropertyChanged(nameof(ActiveHarnessCapabilityText));
        OnPropertyChanged(nameof(ActiveHarnessEndpointText));
    }
}

public sealed class HarnessItemViewModel
{
    private HarnessItemViewModel(HarnessDefinition harness, string? activeHarnessId)
    {
        Source = harness;
        Id = harness.Id;
        Name = harness.Name;
        DisplayName = string.Equals(harness.Id, activeHarnessId, StringComparison.OrdinalIgnoreCase)
            ? $"{harness.Name}  active"
            : harness.Name;
        KindLabel = harness.Kind.ToString();
        CommandPathText = string.IsNullOrWhiteSpace(harness.CommandPath)
            ? "Command: none"
            : $"Command: {harness.CommandPath}";
        VersionText = string.IsNullOrWhiteSpace(harness.Version)
            ? "Version: unavailable"
            : $"Version: {harness.Version}";
        HealthLabel = harness.Health.ToString();
        StatusMessage = harness.StatusMessage;
        CapabilityCountText = $"{harness.Capabilities.Count(capability => capability.IsEnabled)} enabled";
        EndpointCountText = $"{harness.ProposedEndpoints.Count} endpoints";
        LastDiscoveredText = harness.LastDiscoveredUtc is null
            ? "Never discovered"
            : $"Last discovery: {harness.LastDiscoveredUtc.Value.LocalDateTime:g}";
        IsActive = string.Equals(harness.Id, activeHarnessId, StringComparison.OrdinalIgnoreCase);
    }

    public HarnessDefinition Source { get; }

    public string Id { get; }

    public string Name { get; }

    public string DisplayName { get; }

    public string KindLabel { get; }

    public string CommandPathText { get; }

    public string VersionText { get; }

    public string HealthLabel { get; }

    public string StatusMessage { get; }

    public string CapabilityCountText { get; }

    public string EndpointCountText { get; }

    public string LastDiscoveredText { get; }

    public bool IsActive { get; }

    public static HarnessItemViewModel FromModel(HarnessDefinition harness, string? activeHarnessId) => new(harness, activeHarnessId);
}

public sealed class HarnessManifestCandidateItemViewModel
{
    private HarnessManifestCandidateItemViewModel(HarnessManifestCandidate candidate)
    {
        ManifestPath = candidate.ManifestPath;
        Name = candidate.Name;
        DisplayName = string.IsNullOrWhiteSpace(candidate.Id)
            ? candidate.Name
            : $"{candidate.Name}  {candidate.Id}";
        DetailText = $"{candidate.Kind} {candidate.Version}".Trim();
        CommandText = string.IsNullOrWhiteSpace(candidate.Command)
            ? "Command: missing"
            : $"Command: {candidate.Command}";
        StatusMessage = candidate.StatusMessage;
    }

    public string ManifestPath { get; }

    public string Name { get; }

    public string DisplayName { get; }

    public string DetailText { get; }

    public string CommandText { get; }

    public string StatusMessage { get; }

    public static HarnessManifestCandidateItemViewModel FromModel(HarnessManifestCandidate candidate) => new(candidate);
}

public sealed class HarnessOrchestrationStepItemViewModel
{
    private HarnessOrchestrationStepItemViewModel(HarnessOrchestrationStep step)
    {
        OrderText = step.Order.ToString();
        Title = $"{step.HarnessName} - {step.Role}";
        Detail = $"{step.EndpointRoute} / {step.CapabilityId}";
        StatusText = step.StatusText;
    }

    public string OrderText { get; }

    public string Title { get; }

    public string Detail { get; }

    public string StatusText { get; }

    public static HarnessOrchestrationStepItemViewModel FromModel(HarnessOrchestrationStep step) => new(step);
}

public sealed class HarnessCapabilityItemViewModel : ObservableObject
{
    private readonly Func<string, string, bool, Task> _onChanged;
    private bool _isSelected;

    private HarnessCapabilityItemViewModel(
        string harnessId,
        HarnessCapabilityDefinition capability,
        Func<string, string, bool, Task> onChanged)
    {
        _onChanged = onChanged;
        HarnessId = harnessId;
        Id = capability.Id;
        Name = capability.Name;
        Description = capability.Description;
        RequiresApproval = capability.RequiresApproval;
        SafetyText = capability.RequiresApproval ? "approval" : "safe";
        _isSelected = capability.IsEnabled;
    }

    public string HarnessId { get; }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public bool RequiresApproval { get; }

    public string SafetyText { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _ = _onChanged(HarnessId, Id, value);
            }
        }
    }

    public static HarnessCapabilityItemViewModel FromModel(
        string harnessId,
        HarnessCapabilityDefinition capability,
        Func<string, string, bool, Task> onChanged)
    {
        return new HarnessCapabilityItemViewModel(harnessId, capability, onChanged);
    }
}

public sealed class HarnessEndpointItemViewModel
{
    private HarnessEndpointItemViewModel(HarnessEndpointDefinition endpoint)
    {
        Route = $"{endpoint.Method} {endpoint.Route}";
        CapabilityId = endpoint.CapabilityId;
        Safety = endpoint.Safety;
        Description = endpoint.Description;
    }

    public string Route { get; }

    public string CapabilityId { get; }

    public string Safety { get; }

    public string Description { get; }

    public static HarnessEndpointItemViewModel FromModel(HarnessEndpointDefinition endpoint) => new(endpoint);
}
