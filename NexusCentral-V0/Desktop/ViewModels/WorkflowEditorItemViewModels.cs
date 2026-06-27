using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Models;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.ViewModels;

public partial class WorkflowLibraryItemViewModel : ObservableObject
{
    public WorkflowLibraryItemViewModel(WorkflowDefinition workflow)
    {
        Id = workflow.Id;
        Name = workflow.Name;
        Description = workflow.Description;
        Source = workflow.Source;
        SourcePath = workflow.SourcePath;
        StepCount = workflow.Steps.Count;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Source { get; }
    public string SourcePath { get; }
    public int StepCount { get; }

    public string DisplayLabel => $"{Name} ({Id})";

    public string SourceLabel => string.Equals(Source, "local", StringComparison.OrdinalIgnoreCase) ? "Local" : "Built-in";
}

public partial class WorkflowDocumentViewModel : ObservableObject
{
    private readonly AppServices _services;

    public WorkflowDocumentViewModel(AppServices services)
    {
        _services = services;
    }

    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private string _source = "local";

    [ObservableProperty]
    private string _sourcePath = string.Empty;

    [ObservableProperty]
    private string _defaultWrapperId = "nexusagent";

    [ObservableProperty]
    private string _defaultModelId = string.Empty;

    [ObservableProperty]
    private string _defaultMaxTokensText = "1200";

    [ObservableProperty]
    private string _defaultTimeoutSecText = "240";

    [ObservableProperty]
    private string _globalFlags = string.Empty;

    [ObservableProperty]
    private string _environmentText = string.Empty;

    [ObservableProperty]
    private bool _stopOnError = true;

    [ObservableProperty]
    private string _runStatus = "idle";

    [ObservableProperty]
    private string _activeRunId = string.Empty;

    [ObservableProperty]
    private string _lastError = string.Empty;

    [ObservableProperty]
    private string _validationStatus = "not_validated";

    [ObservableProperty]
    private string _validationReadiness = "Unknown";

    [ObservableProperty]
    private string _validationSummary = "Workflow has not been validated yet.";

    [ObservableProperty]
    private string _lastValidatedAtText = "Not validated";

    public ObservableCollection<WorkflowInputLabelViewModel> Inputs { get; } = new();
    public ObservableCollection<WorkflowStepViewModel> Steps { get; } = new();
    public ObservableCollection<string> AvailableWrapperIds { get; } = new();

    internal AppServices Services => _services;

    public static WorkflowDocumentViewModel FromModel(WorkflowDefinition workflow, AppServices services)
    {
        var viewModel = new WorkflowDocumentViewModel(services)
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            WorkingDirectory = workflow.WorkingDirectory,
            Source = workflow.Source,
            SourcePath = workflow.SourcePath,
            DefaultWrapperId = workflow.GlobalSettings.DefaultWrapperId,
            DefaultModelId = workflow.GlobalSettings.DefaultModelId,
            DefaultMaxTokensText = workflow.GlobalSettings.DefaultMaxTokens.ToString(),
            DefaultTimeoutSecText = workflow.GlobalSettings.DefaultTimeoutSec.ToString(),
            GlobalFlags = workflow.GlobalSettings.GlobalFlags,
            EnvironmentText = workflow.GlobalSettings.Environment,
            StopOnError = workflow.GlobalSettings.StopOnError
        };

        foreach (var input in workflow.Inputs)
        {
            viewModel.Inputs.Add(WorkflowInputLabelViewModel.FromModel(input, viewModel));
        }

        foreach (var step in workflow.Steps)
        {
            viewModel.Steps.Add(WorkflowStepViewModel.FromModel(step, viewModel));
        }

        viewModel.RefreshReferenceData();
        viewModel.ResetRunState();
        viewModel.ResetValidationState();
        return viewModel;
    }

    public static WorkflowDocumentViewModel FromBundle(WorkflowBundle bundle, AppServices services)
    {
        var viewModel = FromModel(bundle.Workflow, services);
        foreach (var input in viewModel.Inputs)
        {
            if (bundle.LastValues.TryGetValue(input.Key, out var currentValue))
            {
                input.CurrentValue = currentValue;
            }
        }

        var expanded = new HashSet<string>(bundle.EditorState.ExpandedStepIds, StringComparer.OrdinalIgnoreCase);
        foreach (var step in viewModel.Steps)
        {
            step.IsExpanded = expanded.Count == 0 || expanded.Contains(step.Id);
        }

        return viewModel;
    }

    public void RefreshReferenceData()
    {
        ReplaceValues(AvailableWrapperIds, _services.Wrappers.GetWrappers()
            .OrderBy(wrapper => wrapper.Name, StringComparer.OrdinalIgnoreCase)
            .Select(wrapper => wrapper.Id));
        if (string.IsNullOrWhiteSpace(DefaultWrapperId) && AvailableWrapperIds.Count > 0)
        {
            DefaultWrapperId = AvailableWrapperIds[0];
        }

        foreach (var step in Steps)
        {
            step.RefreshReferenceData();
        }
    }

    public WorkflowDefinition ToModel()
    {
        return new WorkflowDefinition
        {
            Id = (Id ?? string.Empty).Trim(),
            Name = (Name ?? string.Empty).Trim(),
            Description = Description ?? string.Empty,
            WorkingDirectory = WorkingDirectory ?? string.Empty,
            Source = Source ?? "local",
            SourcePath = SourcePath ?? string.Empty,
            GlobalSettings = new WorkflowGlobalSettings
            {
                DefaultWrapperId = DefaultWrapperId ?? string.Empty,
                DefaultModelId = DefaultModelId ?? string.Empty,
                DefaultMaxTokens = ParsePositiveInt(DefaultMaxTokensText, 1200),
                DefaultTimeoutSec = ParsePositiveInt(DefaultTimeoutSecText, 240),
                GlobalFlags = GlobalFlags ?? string.Empty,
                Environment = EnvironmentText ?? string.Empty,
                StopOnError = StopOnError
            },
            Inputs = Inputs.Select(input => input.ToModel()).ToList(),
            Steps = Steps.Select(step => step.ToModel()).ToList()
        };
    }

    public WorkflowBundle ToBundle()
    {
        return new WorkflowBundle
        {
            Workflow = ToModel(),
            EditorState = new WorkflowEditorState
            {
                SelectedStepId = Steps.FirstOrDefault(step => step.IsExpanded)?.Id ?? Steps.FirstOrDefault()?.Id ?? string.Empty,
                ExpandedStepIds = Steps.Where(step => step.IsExpanded).Select(step => step.Id).ToList()
            },
            LastValues = GetCurrentLabelValues()
        };
    }

    public Dictionary<string, string> GetCurrentLabelValues()
    {
        return Inputs.ToDictionary(
            input => input.Key,
            input => string.IsNullOrWhiteSpace(input.CurrentValue) ? input.DefaultValue : input.CurrentValue,
            StringComparer.OrdinalIgnoreCase);
    }

    public void MarkPersisted(string source, string path)
    {
        Source = source;
        SourcePath = path;
    }

    public void StartRun(string runId)
    {
        ActiveRunId = runId;
        RunStatus = "running";
        LastError = string.Empty;
        foreach (var step in Steps)
        {
            step.ResetRunState();
        }
    }

    public async Task RunStepAsync(WorkflowStepViewModel step)
    {
        var handle = _services.WorkflowRuns.StartStepRun(ToModel(), step.Id, GetCurrentLabelValues());
        StartRun(handle.RunId);
        await Task.CompletedTask;
    }

    public void ApplyRun(WorkflowRunState run)
    {
        ActiveRunId = run.RunId;
        RunStatus = run.Status;
        LastError = run.Error;

        foreach (var input in Inputs)
        {
            if (run.LabelValues.TryGetValue(input.Key, out var value))
            {
                input.CurrentValue = value;
            }
        }

        var stepsById = run.Steps.ToDictionary(step => step.StepId, StringComparer.OrdinalIgnoreCase);
        foreach (var step in Steps)
        {
            step.ApplyRunState(stepsById.TryGetValue(step.Id, out var state) ? state : null);
        }
    }

    public void ResetRunState()
    {
        RunStatus = "idle";
        ActiveRunId = string.Empty;
        LastError = string.Empty;
        foreach (var step in Steps)
        {
            step.ResetRunState();
        }
    }

    public void ApplyValidation(WorkflowValidationResult result)
    {
        ValidationStatus = result.Status;
        ValidationReadiness = result.Readiness;
        ValidationSummary = result.Summary;
        LastValidatedAtText = result.ValidatedAtUtc?.ToLocalTime().ToString("g") ?? "Not validated";
    }

    public void ResetValidationState()
    {
        ValidationStatus = "not_validated";
        ValidationReadiness = "Unknown";
        ValidationSummary = "Workflow has not been validated yet.";
        LastValidatedAtText = "Not validated";
    }

    public void AddInputLabel()
    {
        Inputs.Add(new WorkflowInputLabelViewModel(this)
        {
            Key = BuildUniqueInputKey(),
            Label = $"Input {Inputs.Count + 1}",
            Kind = "text",
            DefaultValue = string.Empty,
            CurrentValue = string.Empty
        });
    }

    public void AddStep(string type)
    {
        Steps.Add(new WorkflowStepViewModel(this)
        {
            Id = BuildUniqueStepId(),
            Name = string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase) ? $"Tool Step {Steps.Count + 1}" : $"Agent Step {Steps.Count + 1}",
            Type = string.Equals(type, "tool_call", StringComparison.OrdinalIgnoreCase) ? "tool_call" : "agent",
            Enabled = true,
            WrapperIdOverride = DefaultWrapperId,
            IsExpanded = true
        });
        Steps.Last().RefreshReferenceData();
    }

    internal void RemoveInputLabel(WorkflowInputLabelViewModel input)
    {
        Inputs.Remove(input);
    }

    internal void RemoveStep(WorkflowStepViewModel step)
    {
        Steps.Remove(step);
    }

    internal void DuplicateStep(WorkflowStepViewModel step)
    {
        var clone = WorkflowStepViewModel.FromModel(step.ToModel(), this);
        clone.Id = BuildUniqueStepId();
        clone.Name = $"{step.Name} Copy";
        clone.IsExpanded = true;
        Steps.Insert(Math.Min(Steps.Count, Steps.IndexOf(step) + 1), clone);
    }

    internal void MoveStep(WorkflowStepViewModel step, int delta)
    {
        var currentIndex = Steps.IndexOf(step);
        if (currentIndex < 0)
        {
            return;
        }

        var nextIndex = Math.Clamp(currentIndex + delta, 0, Steps.Count - 1);
        if (nextIndex == currentIndex)
        {
            return;
        }

        Steps.Move(currentIndex, nextIndex);
    }

    private string BuildUniqueInputKey()
    {
        var index = Inputs.Count + 1;
        while (Inputs.Any(input => string.Equals(input.Key, $"input_{index}", StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }

        return $"input_{index}";
    }

    private string BuildUniqueStepId()
    {
        var index = Steps.Count + 1;
        while (Steps.Any(step => string.Equals(step.Id, $"step_{index:00}", StringComparison.OrdinalIgnoreCase)))
        {
            index++;
        }

        return $"step_{index:00}";
    }

    private static int ParsePositiveInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }

    private static void ReplaceValues(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            target.Add(value);
        }
    }
}

public partial class WorkflowInputLabelViewModel : ObservableObject
{
    private readonly WorkflowDocumentViewModel _owner;

    public WorkflowInputLabelViewModel(WorkflowDocumentViewModel owner)
    {
        _owner = owner;
    }

    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _kind = "text";

    [ObservableProperty]
    private bool _required;

    [ObservableProperty]
    private string _defaultValue = string.Empty;

    [ObservableProperty]
    private string _currentValue = string.Empty;

    public IReadOnlyList<string> AvailableKinds { get; } = new[] { "text", "path", "json", "number", "bool" };

    public static WorkflowInputLabelViewModel FromModel(WorkflowInputLabel input, WorkflowDocumentViewModel owner)
    {
        return new WorkflowInputLabelViewModel(owner)
        {
            Key = input.Key,
            Label = input.Label,
            Kind = input.Kind,
            Required = input.Required,
            DefaultValue = input.DefaultValue,
            CurrentValue = input.DefaultValue
        };
    }

    public WorkflowInputLabel ToModel()
    {
        return new WorkflowInputLabel
        {
            Key = Key ?? string.Empty,
            Label = Label ?? string.Empty,
            Kind = Kind ?? "text",
            Required = Required,
            DefaultValue = DefaultValue ?? string.Empty
        };
    }

    [RelayCommand]
    private void Remove()
    {
        _owner.RemoveInputLabel(this);
    }
}

public partial class WorkflowStepViewModel : ObservableObject
{
    private readonly WorkflowDocumentViewModel _owner;

    public WorkflowStepViewModel(WorkflowDocumentViewModel owner)
    {
        _owner = owner;
    }

    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _type = "agent";

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private string _workingDirectoryOverride = string.Empty;

    [ObservableProperty]
    private bool _continueOnError;

    [ObservableProperty]
    private string _templateId = string.Empty;

    [ObservableProperty]
    private string _selectedPresetTemplateId = string.Empty;

    [ObservableProperty]
    private string _wrapperIdOverride = string.Empty;

    [ObservableProperty]
    private string _modelIdOverride = string.Empty;

    [ObservableProperty]
    private string _maxTokensOverrideText = string.Empty;

    [ObservableProperty]
    private string _timeoutSecOverrideText = string.Empty;

    [ObservableProperty]
    private string _rawFlags = string.Empty;

    [ObservableProperty]
    private string _promptTemplate = string.Empty;

    [ObservableProperty]
    private string _artifactInputsText = string.Empty;

    [ObservableProperty]
    private bool _appendPreviousStepOutput = true;

    [ObservableProperty]
    private string _integrationId = "nexuscentral";

    [ObservableProperty]
    private string _toolName = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private string _status = "idle";

    [ObservableProperty]
    private string _statusMessage = "Idle";

    [ObservableProperty]
    private string _finalText = string.Empty;

    [ObservableProperty]
    private string _responseText = string.Empty;

    [ObservableProperty]
    private string _error = string.Empty;

    [ObservableProperty]
    private string _logsText = string.Empty;

    [ObservableProperty]
    private string _childRunId = string.Empty;

    [ObservableProperty]
    private string _artifactOutputText = string.Empty;

    public ObservableCollection<string> AvailableWrapperIds { get; } = new();
    public ObservableCollection<string> AvailableTemplateIds { get; } = new();
    public ObservableCollection<string> AvailableIntegrationIds { get; } = new();
    public ObservableCollection<string> AvailableToolNames { get; } = new();
    public ObservableCollection<WorkflowFlagViewModel> Flags { get; } = new();
    public ObservableCollection<WorkflowBindingViewModel> InputBindings { get; } = new();
    public ObservableCollection<WorkflowOutputBindingViewModel> OutputBindings { get; } = new();
    public ObservableCollection<WorkflowArgumentViewModel> Arguments { get; } = new();
    public IReadOnlyList<string> AvailableStepTypes { get; } = new[] { "agent", "tool_call" };

    public bool IsAgentStep => string.Equals(Type, "agent", StringComparison.OrdinalIgnoreCase);

    public bool IsToolStep => string.Equals(Type, "tool_call", StringComparison.OrdinalIgnoreCase);

    public string TypeLabel => IsToolStep ? "Tool Call" : "Agent";

    public string HeaderStatus => string.IsNullOrWhiteSpace(StatusMessage) ? Status : StatusMessage;

    public string OutputBindingHelpText => IsToolStep
        ? "Tool sources: data, $, $.path"
        : "Agent sources: final_text, primary_text, text_outputs, text_output_0, text_output_1, artifacts, logs, session_id";

    partial void OnTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsAgentStep));
        OnPropertyChanged(nameof(IsToolStep));
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(OutputBindingHelpText));
        if (IsToolStep)
        {
            RefreshReferenceData();
        }
    }

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(HeaderStatus));
    }

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HeaderStatus));
    }

    partial void OnIntegrationIdChanged(string value)
    {
        RefreshAvailableTools();
    }

    partial void OnToolNameChanged(string value)
    {
        EnsureToolArgumentRows();
    }

    public static WorkflowStepViewModel FromModel(WorkflowStepDefinition step, WorkflowDocumentViewModel owner)
    {
        var viewModel = new WorkflowStepViewModel(owner)
        {
            Id = step.Id,
            Name = step.Name,
            Type = step.Type,
            Enabled = step.Enabled,
            WorkingDirectoryOverride = step.WorkingDirectoryOverride,
            ContinueOnError = step.ContinueOnError,
            TemplateId = step.TemplateId,
            SelectedPresetTemplateId = step.TemplateId,
            WrapperIdOverride = step.WrapperIdOverride,
            ModelIdOverride = step.ModelIdOverride,
            MaxTokensOverrideText = step.MaxTokensOverride > 0 ? step.MaxTokensOverride.ToString() : string.Empty,
            TimeoutSecOverrideText = step.TimeoutSecOverride > 0 ? step.TimeoutSecOverride.ToString() : string.Empty,
            RawFlags = step.RawFlags,
            PromptTemplate = step.PromptTemplate,
            ArtifactInputsText = string.Join(Environment.NewLine, step.ArtifactInputs),
            AppendPreviousStepOutput = step.AppendPreviousStepOutput,
            IntegrationId = step.IntegrationId,
            ToolName = step.ToolName
        };

        foreach (var flag in step.Flags)
        {
            viewModel.Flags.Add(WorkflowFlagViewModel.FromModel(flag, viewModel));
        }

        foreach (var binding in step.InputBindings)
        {
            viewModel.InputBindings.Add(WorkflowBindingViewModel.FromModel(binding, viewModel));
        }

        foreach (var binding in step.OutputBindings)
        {
            viewModel.OutputBindings.Add(WorkflowOutputBindingViewModel.FromModel(binding, viewModel));
        }

        foreach (var argument in step.Arguments)
        {
            viewModel.Arguments.Add(new WorkflowArgumentViewModel(viewModel)
            {
                Key = argument.Key,
                Value = argument.Value
            });
        }

        viewModel.RefreshReferenceData();
        viewModel.ResetRunState();
        return viewModel;
    }

    public WorkflowStepDefinition ToModel()
    {
        return new WorkflowStepDefinition
        {
            Id = Id ?? string.Empty,
            Name = Name ?? string.Empty,
            Type = Type ?? "agent",
            Enabled = Enabled,
            WorkingDirectoryOverride = WorkingDirectoryOverride ?? string.Empty,
            ContinueOnError = ContinueOnError,
            TemplateId = TemplateId ?? string.Empty,
            WrapperIdOverride = WrapperIdOverride ?? string.Empty,
            ModelIdOverride = ModelIdOverride ?? string.Empty,
            MaxTokensOverride = ParsePositiveInt(MaxTokensOverrideText, 0),
            TimeoutSecOverride = ParsePositiveInt(TimeoutSecOverrideText, 0),
            Flags = Flags.Select(flag => flag.ToModel()).ToList(),
            RawFlags = RawFlags ?? string.Empty,
            PromptTemplate = PromptTemplate ?? string.Empty,
            ArtifactInputs = SplitMultiline(ArtifactInputsText),
            AppendPreviousStepOutput = AppendPreviousStepOutput,
            IntegrationId = IntegrationId ?? "nexuscentral",
            ToolName = ToolName ?? string.Empty,
            Arguments = Arguments
                .Where(argument => !string.IsNullOrWhiteSpace(argument.Key))
                .ToDictionary(argument => argument.Key, argument => argument.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            InputBindings = InputBindings.Select(binding => binding.ToModel()).ToList(),
            OutputBindings = OutputBindings.Select(binding => binding.ToModel()).ToList()
        };
    }

    public void RefreshReferenceData()
    {
        ReplaceValues(AvailableWrapperIds, _owner.Services.Wrappers.GetWrappers()
            .OrderBy(wrapper => wrapper.Name, StringComparer.OrdinalIgnoreCase)
            .Select(wrapper => wrapper.Id));
        ReplaceValues(AvailableTemplateIds, _owner.Services.Templates.GetTemplates()
            .OrderBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
            .Select(template => template.Id));
        ReplaceValues(AvailableIntegrationIds, _owner.Services.Integrations.GetEnabledIntegrations()
            .OrderBy(integration => integration.Name, StringComparer.OrdinalIgnoreCase)
            .Select(integration => integration.Id));

        if (string.IsNullOrWhiteSpace(IntegrationId) && AvailableIntegrationIds.Count > 0)
        {
            IntegrationId = AvailableIntegrationIds[0];
        }

        RefreshAvailableTools();
    }

    public void ApplyRunState(WorkflowStepRunState? run)
    {
        if (run is null)
        {
            ResetRunState();
            return;
        }

        Status = run.Status;
        StatusMessage = run.StatusMessage;
        FinalText = run.FinalText;
        ResponseText = run.ResponseText;
        Error = run.Error;
        LogsText = string.Join(Environment.NewLine, run.Logs);
        ChildRunId = run.ChildRunId;
        ArtifactOutputText = string.Join(Environment.NewLine, run.ArtifactOutputs.Select(artifact => artifact.Path));
    }

    public void ResetRunState()
    {
        Status = Enabled ? "idle" : "disabled";
        StatusMessage = Enabled ? "Idle" : "Disabled";
        FinalText = string.Empty;
        ResponseText = string.Empty;
        Error = string.Empty;
        LogsText = string.Empty;
        ChildRunId = string.Empty;
        ArtifactOutputText = string.Empty;
    }

    [RelayCommand]
    private async Task RunStepAsync()
    {
        await _owner.RunStepAsync(this);
    }

    [RelayCommand]
    private void Duplicate()
    {
        _owner.DuplicateStep(this);
    }

    [RelayCommand]
    private void Remove()
    {
        _owner.RemoveStep(this);
    }

    [RelayCommand]
    private void MoveUp()
    {
        _owner.MoveStep(this, -1);
    }

    [RelayCommand]
    private void MoveDown()
    {
        _owner.MoveStep(this, 1);
    }

    [RelayCommand]
    private void AddFlag()
    {
        Flags.Add(new WorkflowFlagViewModel(this));
    }

    [RelayCommand]
    private void AddInputBinding()
    {
        InputBindings.Add(new WorkflowBindingViewModel(this));
    }

    [RelayCommand]
    private void AddOutputBinding()
    {
        OutputBindings.Add(new WorkflowOutputBindingViewModel(this)
        {
            Source = IsToolStep ? "data" : "final_text"
        });
    }

    [RelayCommand]
    private void AddArgument()
    {
        Arguments.Add(new WorkflowArgumentViewModel(this));
    }

    [RelayCommand]
    private void ApplyPreset()
    {
        if (string.IsNullOrWhiteSpace(SelectedPresetTemplateId))
        {
            return;
        }

        TemplateId = SelectedPresetTemplateId;
        var template = _owner.Services.Templates.FindById(SelectedPresetTemplateId);
        if (template is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(WrapperIdOverride) && !string.IsNullOrWhiteSpace(template.DefaultWrapperId))
        {
            WrapperIdOverride = template.DefaultWrapperId;
        }

        if (string.IsNullOrWhiteSpace(ModelIdOverride) && !string.IsNullOrWhiteSpace(template.DefaultModelId))
        {
            ModelIdOverride = template.DefaultModelId;
        }

        if (string.IsNullOrWhiteSpace(MaxTokensOverrideText) && template.DefaultMaxTokens > 0)
        {
            MaxTokensOverrideText = template.DefaultMaxTokens.ToString();
        }
    }

    internal void RemoveFlag(WorkflowFlagViewModel flag)
    {
        Flags.Remove(flag);
    }

    internal void RemoveInputBinding(WorkflowBindingViewModel binding)
    {
        InputBindings.Remove(binding);
    }

    internal void RemoveOutputBinding(WorkflowOutputBindingViewModel binding)
    {
        OutputBindings.Remove(binding);
    }

    internal void RemoveArgument(WorkflowArgumentViewModel argument)
    {
        Arguments.Remove(argument);
    }

    private void RefreshAvailableTools()
    {
        ReplaceValues(AvailableToolNames, _owner.Services.Integrations.GetTools(IntegrationId)
            .Where(tool => tool.Enabled)
            .OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .Select(tool => tool.Name));

        if (AvailableToolNames.Count > 0 && !AvailableToolNames.Contains(ToolName))
        {
            ToolName = AvailableToolNames[0];
        }

        EnsureToolArgumentRows();
    }

    private void EnsureToolArgumentRows()
    {
        if (!IsToolStep)
        {
            return;
        }

        var tool = _owner.Services.Integrations.FindTool(IntegrationId, ToolName);
        if (tool is null)
        {
            return;
        }

        foreach (var key in tool.InputContract.Keys)
        {
            if (Arguments.Any(argument => string.Equals(argument.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            Arguments.Add(new WorkflowArgumentViewModel(this)
            {
                Key = key,
                Value = string.Empty
            });
        }
    }

    private static void ReplaceValues(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            target.Add(value);
        }
    }

    private static List<string> SplitMultiline(string? value)
    {
        return (value ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static int ParsePositiveInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
    }
}

public partial class WorkflowFlagViewModel : ObservableObject
{
    private readonly WorkflowStepViewModel _owner;

    public WorkflowFlagViewModel(WorkflowStepViewModel owner)
    {
        _owner = owner;
    }

    [ObservableProperty]
    private string _type = "flag";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;

    public static WorkflowFlagViewModel FromModel(TemplateArgDefinition flag, WorkflowStepViewModel owner)
    {
        return new WorkflowFlagViewModel(owner)
        {
            Type = flag.Type,
            Name = flag.Name,
            Value = flag.Value
        };
    }

    public TemplateArgDefinition ToModel()
    {
        return new TemplateArgDefinition
        {
            Type = Type ?? string.Empty,
            Name = Name ?? string.Empty,
            Value = Value ?? string.Empty
        };
    }

    [RelayCommand]
    private void Remove()
    {
        _owner.RemoveFlag(this);
    }
}

public partial class WorkflowBindingViewModel : ObservableObject
{
    private readonly WorkflowStepViewModel _owner;

    public WorkflowBindingViewModel(WorkflowStepViewModel owner)
    {
        _owner = owner;
    }

    [ObservableProperty]
    private string _target = string.Empty;

    [ObservableProperty]
    private string _sourceType = "label";

    [ObservableProperty]
    private string _sourceKey = string.Empty;

    [ObservableProperty]
    private string _literalValue = string.Empty;

    public IReadOnlyList<string> AvailableSourceTypes { get; } = new[] { "label", "literal" };

    public static WorkflowBindingViewModel FromModel(WorkflowBinding binding, WorkflowStepViewModel owner)
    {
        return new WorkflowBindingViewModel(owner)
        {
            Target = binding.Target,
            SourceType = binding.SourceType,
            SourceKey = binding.SourceKey,
            LiteralValue = binding.LiteralValue
        };
    }

    public WorkflowBinding ToModel()
    {
        return new WorkflowBinding
        {
            Target = Target ?? string.Empty,
            SourceType = SourceType ?? "label",
            SourceKey = SourceKey ?? string.Empty,
            LiteralValue = LiteralValue ?? string.Empty
        };
    }

    [RelayCommand]
    private void Remove()
    {
        _owner.RemoveInputBinding(this);
    }
}

public partial class WorkflowOutputBindingViewModel : ObservableObject
{
    private readonly WorkflowStepViewModel _owner;

    public WorkflowOutputBindingViewModel(WorkflowStepViewModel owner)
    {
        _owner = owner;
    }

    [ObservableProperty]
    private string _source = string.Empty;

    [ObservableProperty]
    private string _targetLabel = string.Empty;

    public static WorkflowOutputBindingViewModel FromModel(WorkflowOutputBinding binding, WorkflowStepViewModel owner)
    {
        return new WorkflowOutputBindingViewModel(owner)
        {
            Source = binding.Source,
            TargetLabel = binding.TargetLabel
        };
    }

    public WorkflowOutputBinding ToModel()
    {
        return new WorkflowOutputBinding
        {
            Source = Source ?? string.Empty,
            TargetLabel = TargetLabel ?? string.Empty
        };
    }

    [RelayCommand]
    private void Remove()
    {
        _owner.RemoveOutputBinding(this);
    }
}

public partial class WorkflowArgumentViewModel : ObservableObject
{
    private readonly WorkflowStepViewModel _owner;

    public WorkflowArgumentViewModel(WorkflowStepViewModel owner)
    {
        _owner = owner;
    }

    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;

    [RelayCommand]
    private void Remove()
    {
        _owner.RemoveArgument(this);
    }
}
