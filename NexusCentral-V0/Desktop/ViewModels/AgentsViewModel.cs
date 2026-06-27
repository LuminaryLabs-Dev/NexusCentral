using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexusCentral_Avalonia.Models;
using NexusCentral_Avalonia.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace NexusCentral_Avalonia.ViewModels;

public partial class AgentsViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private bool _loadingConfig;

    public ObservableCollection<WrapperDefinition> Wrappers { get; } = new();
    public ObservableCollection<WrapperRunState> RecentRuns { get; } = new();
    public ObservableCollection<AgentChoiceOptionViewModel> AgentModelOptions { get; } = new();
    public ObservableCollection<AgentChoiceOptionViewModel> AgentWorkflowOptions { get; } = new();

    [ObservableProperty]
    private WrapperDefinition? _selectedWrapper;

    [ObservableProperty]
    private WrapperRunState? _selectedRun;

    [ObservableProperty]
    private string _importManifestPath = string.Empty;

    [ObservableProperty]
    private string _statusText = "Wrapper registry ready.";

    [ObservableProperty]
    private string _hostedWebStatusText = "Nexus Web Lab is launched from Lab.";

    [ObservableProperty]
    private string _selectedWrapperValidationStatusText = "Validation: not run in this session.";

    [ObservableProperty]
    private AgentChoiceOptionViewModel? _selectedModelOption;

    [ObservableProperty]
    private AgentChoiceOptionViewModel? _selectedWorkflowOption;

    [ObservableProperty]
    private string _agentConfigStatusText = "Agent config: wrapper defaults.";

    public AgentsViewModel(AppServices services)
    {
        _services = services;
        _services.Wrappers.WrappersChanged += Refresh;
        _services.Templates.TemplatesChanged += () => Dispatcher.UIThread.Post(RefreshSelectedWrapperDetails);
        _services.Workflows.WorkflowsChanged += () => Dispatcher.UIThread.Post(RefreshSelectedWrapperDetails);
        _services.Library.ChatInstancesChanged += () => Dispatcher.UIThread.Post(RefreshSelectedWrapperDetails);
        _services.AgentConfigs.ConfigurationsChanged += () => Dispatcher.UIThread.Post(RefreshSelectedWrapperDetails);
        _services.WrapperRuns.RunUpdated += _ => Dispatcher.UIThread.Post(RefreshRuns);
        _services.WebLab.StatusChanged += () => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    public string SelectedWrapperDescription =>
        string.IsNullOrWhiteSpace(SelectedWrapper?.Description)
            ? "Select an agent to inspect its behavior policy."
            : SelectedWrapper.Description;

    public string SelectedWrapperBehaviorSummary
    {
        get
        {
            if (SelectedWrapper is null)
            {
                return "No agent selected.";
            }

            var parts = new[]
            {
                SelectedWrapper.SupportsArtifacts ? "Artifacts on" : "Artifacts off",
                "Nexus Web Lab from Lab",
                SelectedWrapper.SupportsTemplates ? "Template-aware" : "Fixed behavior"
            };
            return string.Join(" • ", parts);
        }
    }

    public string SelectedWrapperContractSummary =>
        SelectedWrapper is null
            ? "Contract: none"
            : $"Contract: {SelectedWrapper.IoContractId}";

    public string SelectedWrapperToolSummary =>
        SelectedWrapper is null
            ? "Tools: none"
            : SelectedWrapper.AllowedTools.Count == 0
                ? "Tools: none declared"
                : $"Tools: {string.Join(", ", SelectedWrapper.AllowedTools)}";

    public string SelectedWrapperRoleSummary =>
        SelectedWrapper is null
            ? "Text roles: none"
            : $"Text in: {(SelectedWrapper.TextInputRoles.Count == 0 ? "prompt" : string.Join(", ", SelectedWrapper.TextInputRoles))} • Text out: {(SelectedWrapper.TextOutputRoles.Count == 0 ? "final_text" : string.Join(", ", SelectedWrapper.TextOutputRoles))}";

    public string SelectedWrapperInputSummary =>
        SelectedWrapper is null || SelectedWrapper.InputContract.Count == 0
            ? "Inputs: prompt"
            : $"Inputs: {string.Join(", ", SelectedWrapper.InputContract.Select(pair => $"{pair.Key}"))}";

    public string SelectedWrapperOutputSummary =>
        SelectedWrapper is null || SelectedWrapper.OutputContract.Count == 0
            ? "Outputs: final"
            : $"Outputs: {string.Join(", ", SelectedWrapper.OutputContract.Select(pair => $"{pair.Key}"))}";

    public string SelectedWrapperSourceSummary =>
        SelectedWrapper is null
            ? string.Empty
            : $"{SelectedWrapper.DisplaySource} • timeout {SelectedWrapper.DefaultTimeoutSec}s • bundle {(string.IsNullOrWhiteSpace(SelectedWrapper.SourceBundlePath) ? "n/a" : SelectedWrapper.SourceBundlePath)}";

    public string SelectedWrapperImportSummary
    {
        get
        {
            if (SelectedWrapper is null)
            {
                return "Import: none";
            }

            if (string.IsNullOrWhiteSpace(SelectedWrapper.ImportMetadata.ManifestPath))
            {
                return "Import: built-in or legacy local wrapper";
            }

            var importedAt = SelectedWrapper.ImportMetadata.ImportedAtUtc?.ToString("u") ?? "unknown time";
            return $"Manifest: {SelectedWrapper.ImportMetadata.ManifestPath}{Environment.NewLine}Build: {SelectedWrapper.ImportMetadata.BuildFolderPath}{Environment.NewLine}Executable: {SelectedWrapper.ImportMetadata.ExecutableRelativePath}{Environment.NewLine}Imported from: {SelectedWrapper.ImportMetadata.ImportedFrom} at {importedAt}";
        }
    }

    public string SelectedWrapperConfigSummary
    {
        get
        {
            if (SelectedWrapper is null)
            {
                return "Config defaults: none";
            }

            var modelLabel = SelectedModelOption?.Label ?? "Auto";
            var workflowLabel = SelectedWorkflowOption?.Label ?? "none";
            return $"Config defaults: model {modelLabel} • workflow {workflowLabel}";
        }
    }

    partial void OnSelectedWrapperChanged(WrapperDefinition? value)
    {
        SelectedWrapperValidationStatusText = BuildWrapperValidationSummary(value);
        RefreshSelectedWrapperDetails();
    }

    [RelayCommand]
    private void Refresh()
    {
        var selectedWrapperId = SelectedWrapper?.Id ?? string.Empty;
        Wrappers.Clear();
        foreach (var wrapper in _services.Wrappers.GetWrappers().OrderBy(wrapper => wrapper.Name, StringComparer.OrdinalIgnoreCase))
        {
            Wrappers.Add(wrapper);
        }

        SelectedWrapper = Wrappers.FirstOrDefault(wrapper => string.Equals(wrapper.Id, selectedWrapperId, StringComparison.OrdinalIgnoreCase))
            ?? Wrappers.FirstOrDefault();
        RefreshRuns();
        RefreshSelectedWrapperDetails();
        HostedWebStatusText = _services.WebLab.StatusText;
        StatusText = $"Loaded {Wrappers.Count} wrappers and {RecentRuns.Count} runs.";
    }

    [RelayCommand]
    private async Task ImportWrapper()
    {
        if (string.IsNullOrWhiteSpace(ImportManifestPath))
        {
            StatusText = "Enter a wrapper manifest path to import.";
            return;
        }

        var result = await _services.Wrappers.ImportWrapperAsync(ImportManifestPath);
        if (!result.Ok)
        {
            StatusText = $"Import failed: {string.Join(" ", result.Errors.Concat(result.Warnings))}";
            if (result.Wrapper is not null)
            {
                SelectedWrapperValidationStatusText = BuildWrapperValidationSummary(result.Wrapper);
            }
            return;
        }

        Refresh();
        SelectedWrapper = Wrappers.FirstOrDefault(wrapper => string.Equals(wrapper.Id, result.WrapperId, StringComparison.OrdinalIgnoreCase))
            ?? SelectedWrapper;
        if (result.Wrapper?.ImportMetadata.ManifestPath is { Length: > 0 } importedManifestPath)
        {
            ImportManifestPath = importedManifestPath;
        }

        StatusText = $"Imported wrapper '{result.Wrapper?.Name ?? result.WrapperId}' into the local wrapper store.";
    }

    [RelayCommand]
    private void SaveSelectedAgentConfig()
    {
        if (SelectedWrapper is null)
        {
            return;
        }

        var config = new AgentConfigOverride
        {
            AgentId = SelectedWrapper.Id,
            TemplateId = string.Empty,
            DefaultModelId = SelectedModelOption?.Id ?? string.Empty,
            DefaultWorkflowId = SelectedWorkflowOption?.Id ?? string.Empty
        };

        _services.AgentConfigs.Save(config);
        AgentConfigStatusText = $"Agent config saved for {SelectedWrapper.Name}.";
        StatusText = AgentConfigStatusText;
        RefreshSelectedWrapperDetails();
    }

    [RelayCommand]
    private void ResetSelectedAgentConfig()
    {
        if (SelectedWrapper is null)
        {
            return;
        }

        _services.AgentConfigs.Reset(SelectedWrapper.Id);
        AgentConfigStatusText = $"Agent config reset for {SelectedWrapper.Name}.";
        StatusText = AgentConfigStatusText;
        RefreshSelectedWrapperDetails();
    }

    [RelayCommand]
    private async Task ValidateSelectedWrapper()
    {
        if (SelectedWrapper is null)
        {
            return;
        }

        var result = await _services.Wrappers.ValidateWrapperAsync(SelectedWrapper.Id);
        SelectedWrapperValidationStatusText = result.Ok
            ? $"Validation: ready • command {result.ResolvedCommand}{(result.ResolvedArgs.Count == 0 ? string.Empty : $" • args {string.Join(" ", result.ResolvedArgs)}")}"
            : $"Validation: {string.Join(" ", result.Errors.Concat(result.Warnings))}";
        StatusText = result.Ok
            ? $"Wrapper '{SelectedWrapper.Name}' validated."
            : $"Wrapper '{SelectedWrapper.Name}' invalid: {string.Join(" ", result.Errors.Concat(result.Warnings))}";
    }

    [RelayCommand]
    private void CancelSelectedRun()
    {
        if (SelectedRun is null)
        {
            return;
        }

        var cancelled = _services.WrapperRuns.CancelRun(SelectedRun.RunId);
        StatusText = cancelled
            ? $"Cancel requested for run {SelectedRun.RunId}."
            : $"Could not cancel run {SelectedRun.RunId}.";
        RefreshRuns();
    }

    [RelayCommand]
    private void CloseOverview()
    {
        SelectedWrapper = null;
        SelectedRun = null;
    }

    private void RefreshRuns()
    {
        var selectedRunId = SelectedRun?.RunId ?? string.Empty;
        RecentRuns.Clear();
        foreach (var run in _services.WrapperRuns.GetRecentRuns(20))
        {
            RecentRuns.Add(run);
        }

        SelectedRun = RecentRuns.FirstOrDefault(run => string.Equals(run.RunId, selectedRunId, StringComparison.OrdinalIgnoreCase))
            ?? RecentRuns.FirstOrDefault();
    }

    private void RefreshSelectedWrapperDetails()
    {
        RefreshOptionSources();
        LoadSelectedWrapperConfig();

        OnPropertyChanged(nameof(SelectedWrapperDescription));
        OnPropertyChanged(nameof(SelectedWrapperBehaviorSummary));
        OnPropertyChanged(nameof(SelectedWrapperContractSummary));
        OnPropertyChanged(nameof(SelectedWrapperToolSummary));
        OnPropertyChanged(nameof(SelectedWrapperRoleSummary));
        OnPropertyChanged(nameof(SelectedWrapperInputSummary));
        OnPropertyChanged(nameof(SelectedWrapperOutputSummary));
        OnPropertyChanged(nameof(SelectedWrapperSourceSummary));
        OnPropertyChanged(nameof(SelectedWrapperImportSummary));
        OnPropertyChanged(nameof(SelectedWrapperConfigSummary));
    }

    private static string BuildWrapperValidationSummary(WrapperDefinition? wrapper)
    {
        if (wrapper is null)
        {
            return "Validation: no wrapper selected.";
        }

        var validation = string.IsNullOrWhiteSpace(wrapper.Validation) ? "n/a" : wrapper.Validation;
        var availability = string.IsNullOrWhiteSpace(wrapper.AvailabilityMessage) ? "unknown" : wrapper.AvailabilityMessage;
        return $"Validation: {validation} • Availability: {availability}";
    }

    partial void OnSelectedModelOptionChanged(AgentChoiceOptionViewModel? value)
    {
        if (_loadingConfig)
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedWrapperConfigSummary));
    }

    partial void OnSelectedWorkflowOptionChanged(AgentChoiceOptionViewModel? value)
    {
        if (_loadingConfig)
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedWrapperConfigSummary));
    }

    private void RefreshOptionSources()
    {
        var selectedModelId = SelectedModelOption?.Id ?? string.Empty;
        var selectedWorkflowId = SelectedWorkflowOption?.Id ?? string.Empty;

        AgentModelOptions.Clear();
        AgentModelOptions.Add(new AgentChoiceOptionViewModel(string.Empty, "Auto"));
        foreach (var model in _services.Library.GetChatAvailableInstances()
                     .OrderByDescending(instance => instance.IsPinned)
                     .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            AgentModelOptions.Add(new AgentChoiceOptionViewModel(model.ModelId, model.DisplayName));
        }

        AgentWorkflowOptions.Clear();
        AgentWorkflowOptions.Add(new AgentChoiceOptionViewModel(string.Empty, "None"));
        foreach (var workflow in _services.Workflows.GetWorkflows().OrderBy(workflow => workflow.Name, StringComparer.OrdinalIgnoreCase))
        {
            AgentWorkflowOptions.Add(new AgentChoiceOptionViewModel(workflow.Id, workflow.Name));
        }

        SelectedModelOption = AgentModelOptions.FirstOrDefault(option => string.Equals(option.Id, selectedModelId, StringComparison.OrdinalIgnoreCase))
            ?? SelectedModelOption
            ?? AgentModelOptions.FirstOrDefault();
        SelectedWorkflowOption = AgentWorkflowOptions.FirstOrDefault(option => string.Equals(option.Id, selectedWorkflowId, StringComparison.OrdinalIgnoreCase))
            ?? SelectedWorkflowOption
            ?? AgentWorkflowOptions.FirstOrDefault();
    }

    private void LoadSelectedWrapperConfig()
    {
        _loadingConfig = true;
        try
        {
            if (SelectedWrapper is null)
            {
                SelectedModelOption = AgentModelOptions.FirstOrDefault();
                SelectedWorkflowOption = AgentWorkflowOptions.FirstOrDefault();
                AgentConfigStatusText = "Agent config: no agent selected.";
                return;
            }

            var effective = _services.AgentConfigs.ResolveEffective(SelectedWrapper);
            var overrideConfig = _services.AgentConfigs.GetOverride(SelectedWrapper.Id);

            SelectedModelOption = AgentModelOptions.FirstOrDefault(option => string.Equals(option.Id, effective.DefaultModelId, StringComparison.OrdinalIgnoreCase))
                ?? AgentModelOptions.FirstOrDefault();
            SelectedWorkflowOption = AgentWorkflowOptions.FirstOrDefault(option => string.Equals(option.Id, effective.DefaultWorkflowId, StringComparison.OrdinalIgnoreCase))
                ?? AgentWorkflowOptions.FirstOrDefault();

            AgentConfigStatusText = string.IsNullOrWhiteSpace(overrideConfig.AgentId)
                ? "Agent config: wrapper defaults."
                : $"Agent config: local override saved {overrideConfig.UpdatedAtUtc:u}.";
        }
        finally
        {
            _loadingConfig = false;
        }

        OnPropertyChanged(nameof(SelectedWrapperConfigSummary));
    }
}

public sealed class AgentChoiceOptionViewModel
{
    public AgentChoiceOptionViewModel(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }
}
