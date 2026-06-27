using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexusCentral_Avalonia.Models;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.ViewModels;

public partial class AutomationsViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private bool _suppressStateUpdates;

    public ObservableCollection<AutomationWorkflowViewModel> Workflows { get; } = new();
    public ObservableCollection<AutomationArtifactItemViewModel> Artifacts { get; } = new();

    [ObservableProperty]
    private string _automationName = "New Automation";

    [ObservableProperty]
    private string _automationGoal = "Build one final output from selected source files.";

    [ObservableProperty]
    private string _targetDirectory = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Define the workflows, select files, and run through /thinking.";

    [ObservableProperty]
    private string _currentPage = "Workflows";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _activeThinkingModelName = "No active /thinking model.";

    [ObservableProperty]
    private AutomationWorkflowViewModel? _selectedWorkflow;

    [ObservableProperty]
    private string _exportYamlText = string.Empty;

    [ObservableProperty]
    private AutomationArtifactItemViewModel? _selectedArtifact;

    [ObservableProperty]
    private string _artifactEditorPath = string.Empty;

    [ObservableProperty]
    private string _artifactEditorText = string.Empty;

    [ObservableProperty]
    private bool _artifactEditorReadOnly = true;

    [ObservableProperty]
    private string _lastRunOutputPath = string.Empty;

    public bool IsWorkflowsPage => string.Equals(CurrentPage, "Workflows", StringComparison.OrdinalIgnoreCase);
    public bool IsIdePage => string.Equals(CurrentPage, "IDE", StringComparison.OrdinalIgnoreCase);
    public bool IsExportPage => string.Equals(CurrentPage, "Export", StringComparison.OrdinalIgnoreCase);
    public bool CanSaveArtifact => SelectedArtifact is not null && !string.IsNullOrWhiteSpace(ArtifactEditorPath);

    public AutomationsViewModel(AppServices services)
    {
        _services = services;
        Workflows.CollectionChanged += OnWorkflowsChanged;
        _services.Library.LibraryInventoryChanged += RefreshActiveThinkingModel;
        _services.Library.InstanceChanged += _ => RefreshActiveThinkingModel();
        _services.Workspaces.WorkspacesChanged += RefreshWorkspaceOptions;

        RefreshWorkspaceOptions();
        ResetToDefaultShape();
        RefreshActiveThinkingModel();
    }

    partial void OnAutomationNameChanged(string value) => NotifyStateChanged();
    partial void OnAutomationGoalChanged(string value) => NotifyStateChanged();
    partial void OnTargetDirectoryChanged(string value) => NotifyStateChanged();

    partial void OnCurrentPageChanged(string value)
    {
        OnPropertyChanged(nameof(IsWorkflowsPage));
        OnPropertyChanged(nameof(IsIdePage));
        OnPropertyChanged(nameof(IsExportPage));
    }

    partial void OnSelectedArtifactChanged(AutomationArtifactItemViewModel? value)
    {
        LoadSelectedArtifact(value);
        OnPropertyChanged(nameof(CanSaveArtifact));
    }

    partial void OnSelectedWorkflowChanged(AutomationWorkflowViewModel? value)
    {
        if (value is null || Workflows.Contains(value))
        {
            return;
        }

        SelectedWorkflow = Workflows.FirstOrDefault();
    }

    [RelayCommand]
    private void ShowPage(string page)
    {
        CurrentPage = string.IsNullOrWhiteSpace(page) ? "Workflows" : page.Trim();
    }

    [RelayCommand]
    public void AddWorkflow()
    {
        var workflow = CreateDefaultWorkflow($"Workflow {Workflows.Count + 1}", GetDefaultWorkspaceId());
        Workflows.Add(workflow);
        SelectedWorkflow = workflow;
        StatusMessage = "Added workflow.";
        NotifyStateChanged();
    }

    [RelayCommand]
    public void RemoveWorkflow(AutomationWorkflowViewModel? workflow)
    {
        if (workflow is null || Workflows.Count <= 1)
        {
            return;
        }

        var removalIndex = Workflows.IndexOf(workflow);
        var wasSelected = ReferenceEquals(SelectedWorkflow, workflow);
        Workflows.Remove(workflow);
        if (wasSelected)
        {
            var replacementIndex = Math.Clamp(removalIndex, 0, Workflows.Count - 1);
            SelectedWorkflow = Workflows.Count > 0 ? Workflows[replacementIndex] : null;
        }
        StatusMessage = "Removed workflow.";
        NotifyStateChanged();
    }

    [RelayCommand]
    public void DuplicateWorkflow(AutomationWorkflowViewModel? workflow)
    {
        if (workflow is null)
        {
            return;
        }

        var duplicate = workflow.Clone($"Copy of {workflow.Name}", NotifyStateChanged);
        Workflows.Add(duplicate);
        SelectedWorkflow = duplicate;
        StatusMessage = "Duplicated workflow.";
        NotifyStateChanged();
    }

    [RelayCommand]
    public void MoveWorkflowUp(AutomationWorkflowViewModel? workflow)
    {
        if (workflow is null)
        {
            return;
        }

        var index = Workflows.IndexOf(workflow);
        if (index <= 0)
        {
            return;
        }

        Workflows.Move(index, index - 1);
        NotifyStateChanged();
    }

    [RelayCommand]
    public void MoveWorkflowDown(AutomationWorkflowViewModel? workflow)
    {
        if (workflow is null)
        {
            return;
        }

        var index = Workflows.IndexOf(workflow);
        if (index < 0 || index >= Workflows.Count - 1)
        {
            return;
        }

        Workflows.Move(index, index + 1);
        NotifyStateChanged();
    }

    [RelayCommand]
    public void AddStepToWorkflow(AutomationWorkflowViewModel? workflow)
    {
        if (workflow is null)
        {
            return;
        }

        workflow.Steps.Add(new AutomationWorkflowStepViewModel(NotifyStateChanged)
        {
            Name = $"Step {workflow.Steps.Count + 1}",
            Enabled = true,
            IncludePreviousStepOutput = workflow.Steps.Count > 0,
            PromptText = "Describe exactly how this step should transform the selected input files.",
            OutputPath = $"outputs/{NormalizeId(workflow.Name)}_step_{workflow.Steps.Count + 1}_{{ts}}.md"
        });
        workflow.CurrentStepIndex = workflow.Steps.Count - 1;
        StatusMessage = "Added step.";
        NotifyStateChanged();
    }

    public void MoveStepUp(AutomationWorkflowViewModel? workflow, AutomationWorkflowStepViewModel? step)
    {
        if (workflow is null || step is null)
        {
            return;
        }

        var index = workflow.Steps.IndexOf(step);
        if (index <= 0)
        {
            return;
        }

        workflow.Steps.Move(index, index - 1);
        workflow.CurrentStepIndex = Math.Max(0, index - 1);
        NotifyStateChanged();
    }

    public void MoveStepDown(AutomationWorkflowViewModel? workflow, AutomationWorkflowStepViewModel? step)
    {
        if (workflow is null || step is null)
        {
            return;
        }

        var index = workflow.Steps.IndexOf(step);
        if (index < 0 || index >= workflow.Steps.Count - 1)
        {
            return;
        }

        workflow.Steps.Move(index, index + 1);
        workflow.CurrentStepIndex = Math.Min(workflow.Steps.Count - 1, index + 1);
        NotifyStateChanged();
    }

    public void RemoveStep(AutomationWorkflowViewModel? workflow, AutomationWorkflowStepViewModel? step)
    {
        if (workflow is null || step is null)
        {
            return;
        }

        workflow.Steps.Remove(step);
        if (workflow.Steps.Count == 0)
        {
            workflow.Steps.Add(new AutomationWorkflowStepViewModel(NotifyStateChanged)
            {
                Name = "Step 1",
                Enabled = true,
                IncludePreviousStepOutput = false,
                PromptText = "Describe exactly how this step should transform the selected input files.",
                OutputPath = $"outputs/{NormalizeId(workflow.Name)}_step_1_{{ts}}.md"
            });
        }

        workflow.CurrentStepIndex = Math.Clamp(workflow.CurrentStepIndex, 0, workflow.Steps.Count - 1);
        NotifyStateChanged();
    }

    public void ShowPreviousStep(AutomationWorkflowViewModel? workflow)
    {
        if (workflow is null || workflow.CurrentStepIndex < 0)
        {
            return;
        }

        workflow.CurrentStepIndex--;
    }

    public void ShowNextStep(AutomationWorkflowViewModel? workflow)
    {
        if (workflow is null)
        {
            return;
        }

        if (workflow.CurrentStepIndex < 0)
        {
            if (workflow.Steps.Count > 0)
            {
                workflow.CurrentStepIndex = 0;
            }

            return;
        }

        if (workflow.CurrentStepIndex >= workflow.Steps.Count - 1)
        {
            return;
        }

        workflow.CurrentStepIndex++;
    }

    [RelayCommand]
    private async Task RunAutomationAsync()
    {
        var errors = Validate(requireExportDirectory: false);
        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return;
        }

        ResetRunState();
        IsRunning = true;
        StatusMessage = "Running automation through /thinking...";

        var request = BuildExecutionRequest();
        var progress = new Progress<AutomationExecutionProgress>(ApplyProgress);
        var result = await _services.AutomationExecution.RunAsync(request, progress);

        IsRunning = false;
        StatusMessage = result.StatusMessage;
        LastRunOutputPath = result.FinalOutputPath;
        RefreshArtifacts();

        if (!string.IsNullOrWhiteSpace(result.FinalOutputPath))
        {
            var artifact = Artifacts.FirstOrDefault(candidate => string.Equals(candidate.ArtifactPath, result.FinalOutputPath, StringComparison.OrdinalIgnoreCase));
            if (artifact is not null)
            {
                SelectedArtifact = artifact;
                CurrentPage = "IDE";
            }
        }
    }

    [RelayCommand]
    private void StopAutomation()
    {
        if (!IsRunning)
        {
            return;
        }

        _services.AutomationExecution.RequestStop();
        StatusMessage = "Stop requested. The current /thinking call will finish before stopping.";
    }

    [RelayCommand]
    private void RefreshArtifacts()
    {
        var previousSelection = SelectedArtifact?.ArtifactPath;
        var items = CollectArtifactItems();

        Artifacts.Clear();
        foreach (var item in items)
        {
            Artifacts.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(previousSelection))
        {
            SelectedArtifact = Artifacts.FirstOrDefault(candidate => string.Equals(candidate.ArtifactPath, previousSelection, StringComparison.OrdinalIgnoreCase));
        }
    }

    [RelayCommand]
    private async Task SaveArtifactAsync()
    {
        if (SelectedArtifact is null || string.IsNullOrWhiteSpace(ArtifactEditorPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(ArtifactEditorPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(ArtifactEditorPath, ArtifactEditorText ?? string.Empty);
        StatusMessage = $"Saved artifact to {ArtifactEditorPath}.";
        RefreshArtifacts();
    }

    [RelayCommand]
    private async Task ExportConfigurationAsync()
    {
        var errors = Validate(requireExportDirectory: true);
        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return;
        }

        if (string.IsNullOrWhiteSpace(TargetDirectory))
        {
            StatusMessage = "Set a target directory first.";
            return;
        }

        Directory.CreateDirectory(TargetDirectory);
        var exportPath = Path.Combine(TargetDirectory, $"{NormalizeId(AutomationName)}_config_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.yaml");
        await File.WriteAllTextAsync(exportPath, ExportYamlText);
        StatusMessage = $"Exported automation config to {exportPath}.";
        RefreshArtifacts();
    }

    [RelayCommand]
    private void ResetShape()
    {
        ResetToDefaultShape();
        StatusMessage = "Reset to the default workflow card layout.";
    }

    public void SetTargetDirectory(string path)
    {
        TargetDirectory = path?.Trim() ?? string.Empty;
        StatusMessage = string.IsNullOrWhiteSpace(TargetDirectory)
            ? "Target directory cleared."
            : $"Target directory set to {TargetDirectory}";
    }

    private void OnWorkflowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (AutomationWorkflowViewModel workflow in e.NewItems)
            {
                workflow.Attach(NotifyStateChanged);
            }
        }

        if (SelectedWorkflow is null || !Workflows.Contains(SelectedWorkflow))
        {
            SelectedWorkflow = Workflows.FirstOrDefault();
        }

        RefreshWorkspaceOptions();
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        if (_suppressStateUpdates)
        {
            return;
        }

        UpdateExportYaml();
        RefreshArtifacts();
    }

    private void UpdateExportYaml()
    {
        ExportYamlText = BuildConfigurationYaml();
    }

    private void RefreshActiveThinkingModel()
    {
        var instance = _services.Library.ResolveInstanceForEndpoint("thinking");
        ActiveThinkingModelName = instance is null
            ? "No active /thinking model."
            : $"/thinking: {instance.DisplayName}";
    }

    private void RefreshWorkspaceOptions()
    {
        var options = _services.Workspaces.GetWorkspaces();
        var defaultWorkspaceId = GetDefaultWorkspaceId(options);

        foreach (var workflow in Workflows)
        {
            workflow.SetWorkspaceOptions(options, defaultWorkspaceId);
        }
    }

    private string GetDefaultWorkspaceId(IReadOnlyList<WorkspaceDefinition>? options = null)
    {
        options ??= _services.Workspaces.GetWorkspaces();

        var activeWorkspace = _services.Workspaces.GetActiveWorkspace();
        if (!string.IsNullOrWhiteSpace(activeWorkspace?.Id) && options.Any(candidate => string.Equals(candidate.Id, activeWorkspace.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return activeWorkspace.Id;
        }

        return options.FirstOrDefault()?.Id ?? string.Empty;
    }

    private void LoadSelectedArtifact(AutomationArtifactItemViewModel? artifact)
    {
        if (artifact is null)
        {
            ArtifactEditorPath = string.Empty;
            ArtifactEditorText = string.Empty;
            ArtifactEditorReadOnly = true;
            return;
        }

        ArtifactEditorPath = artifact.ArtifactPath;
        ArtifactEditorText = File.Exists(artifact.ArtifactPath)
            ? File.ReadAllText(artifact.ArtifactPath)
            : string.Empty;
        ArtifactEditorReadOnly = false;
    }

    private List<AutomationArtifactItemViewModel> CollectArtifactItems()
    {
        var items = new List<AutomationArtifactItemViewModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var workflow in Workflows)
        {
            foreach (var step in workflow.Steps)
            {
                foreach (var inputPath in step.GetInputPaths())
                {
                    var resolvedPath = ResolveWorkspacePath(inputPath, workflow.SelectedWorkspaceRootDirectory);
                    if (!seen.Add(resolvedPath))
                    {
                        continue;
                    }

                    items.Add(new AutomationArtifactItemViewModel
                    {
                        ArtifactPath = resolvedPath,
                        DisplayPath = BuildDisplayPath(resolvedPath),
                        ArtifactKind = "Input",
                        Exists = File.Exists(resolvedPath)
                    });
                }

                var resolvedOutput = ResolveOutputPath(step.OutputPath, workflow.SelectedWorkspaceRootDirectory);
                if (seen.Add(resolvedOutput))
                {
                    items.Add(new AutomationArtifactItemViewModel
                    {
                        ArtifactPath = resolvedOutput,
                        DisplayPath = BuildDisplayPath(resolvedOutput),
                        ArtifactKind = "Output",
                        Exists = File.Exists(resolvedOutput)
                    });
                }
            }
        }

        return items
            .OrderBy(item => item.ArtifactKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DisplayPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> Validate(bool requireExportDirectory)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(AutomationName))
        {
            errors.Add("Set an automation name.");
        }

        if (requireExportDirectory && string.IsNullOrWhiteSpace(TargetDirectory))
        {
            errors.Add("Set a target directory.");
        }

        if (Workflows.Count == 0)
        {
            errors.Add("Add at least one workflow.");
        }

        for (var workflowIndex = 0; workflowIndex < Workflows.Count; workflowIndex++)
        {
            var workflow = Workflows[workflowIndex];
            if (string.IsNullOrWhiteSpace(workflow.Name))
            {
                errors.Add($"Workflow {workflowIndex + 1} needs a name.");
            }

            if (string.IsNullOrWhiteSpace(workflow.SelectedWorkspaceId) || string.IsNullOrWhiteSpace(workflow.SelectedWorkspaceRootDirectory))
            {
                errors.Add($"Workflow '{workflow.Name}' needs a workspace selected.");
            }

            if (workflow.Steps.Count == 0)
            {
                errors.Add($"Workflow '{workflow.Name}' needs at least one step.");
            }

            for (var stepIndex = 0; stepIndex < workflow.Steps.Count; stepIndex++)
            {
                var step = workflow.Steps[stepIndex];
                if (!step.Enabled)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(step.Name))
                {
                    errors.Add($"Workflow '{workflow.Name}' step {stepIndex + 1} needs a name.");
                }

                if (string.IsNullOrWhiteSpace(step.PromptText))
                {
                    errors.Add($"Step '{step.Name}' needs a prompt.");
                }

                if (string.IsNullOrWhiteSpace(step.OutputPath))
                {
                    errors.Add($"Step '{step.Name}' needs an output path.");
                }

                var inputPaths = step.GetInputPaths();
                if (inputPaths.Count == 0 && !step.IncludePreviousStepOutput)
                {
                    errors.Add($"Step '{step.Name}' needs at least one input file or previous-step output enabled.");
                }

                foreach (var inputPath in inputPaths)
                {
                    var resolvedInputPath = ResolveWorkspacePath(inputPath, workflow.SelectedWorkspaceRootDirectory);
                    if (!File.Exists(resolvedInputPath))
                    {
                        errors.Add($"Input file '{inputPath}' for step '{step.Name}' does not exist.");
                    }
                }
            }
        }

        return errors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private AutomationExecutionRequest BuildExecutionRequest()
    {
        return new AutomationExecutionRequest
        {
            Name = AutomationName,
            Goal = AutomationGoal,
            TargetDirectory = TargetDirectory,
            Workflows = Workflows.Select(workflow => new AutomationWorkflowExecutionRequest
            {
                Name = workflow.Name,
                Goal = workflow.GoalText,
                Enabled = workflow.Enabled,
                WorkspaceId = workflow.SelectedWorkspaceId,
                WorkspaceRootDirectory = workflow.SelectedWorkspaceRootDirectory,
                Steps = workflow.Steps.Select(step => new AutomationStepExecutionRequest
                {
                    Name = step.Name,
                    Enabled = step.Enabled,
                    InputPaths = step.GetInputPaths().ToList(),
                    IncludePreviousStepOutput = step.IncludePreviousStepOutput,
                    Prompt = step.PromptText,
                    OutputPath = step.OutputPath
                }).ToList()
            }).ToList()
        };
    }

    private void ApplyProgress(AutomationExecutionProgress progress)
    {
        if (progress.WorkflowIndex < 0 || progress.WorkflowIndex >= Workflows.Count)
        {
            return;
        }

        var workflow = Workflows[progress.WorkflowIndex];
        workflow.StatusText = progress.Message;
        workflow.CurrentStepIndex = Math.Clamp(progress.StepIndex, 0, Math.Max(0, workflow.Steps.Count - 1));

        if (progress.StepIndex < 0 || progress.StepIndex >= workflow.Steps.Count)
        {
            return;
        }

        var step = workflow.Steps[progress.StepIndex];
        step.StatusText = string.IsNullOrWhiteSpace(progress.Error) ? progress.Message : progress.Error;
        if (!string.IsNullOrWhiteSpace(progress.OutputPath))
        {
            step.LastOutputPath = progress.OutputPath;
        }
    }

    private void ResetRunState()
    {
        LastRunOutputPath = string.Empty;
        foreach (var workflow in Workflows)
        {
            workflow.StatusText = workflow.Enabled ? "Ready" : "Disabled";
            foreach (var step in workflow.Steps)
            {
                step.StatusText = step.Enabled ? "Ready" : "Disabled";
                step.LastOutputPath = string.Empty;
            }
        }
    }

    private void ResetToDefaultShape()
    {
        _suppressStateUpdates = true;
        AutomationName = "Artifact Automation";
        AutomationGoal = "Build one final output from selected source files through ordered workflow steps.";
        TargetDirectory = _services.Paths.ResolvedRootDirectory;
        CurrentPage = "Workflows";
        IsRunning = false;
        LastRunOutputPath = string.Empty;
        Workflows.Clear();
        var firstWorkflow = CreateDefaultWorkflow("Workflow 1", GetDefaultWorkspaceId());
        var secondWorkflow = CreateDefaultWorkflow("Workflow 2", GetDefaultWorkspaceId());
        Workflows.Add(firstWorkflow);
        Workflows.Add(secondWorkflow);
        SelectedWorkflow = firstWorkflow;
        _suppressStateUpdates = false;
        RefreshWorkspaceOptions();
        UpdateExportYaml();
        RefreshArtifacts();
        SelectedArtifact = null;
    }

    private AutomationWorkflowViewModel CreateDefaultWorkflow(string name, string? selectedWorkspaceId = null)
    {
        var workflow = new AutomationWorkflowViewModel(NotifyStateChanged)
        {
            Name = name,
            GoalText = "Refine the automation goal for this workflow.",
            Enabled = true,
            StatusText = "Ready"
        };

        workflow.SetWorkspaceOptions(_services.Workspaces.GetWorkspaces(), selectedWorkspaceId ?? GetDefaultWorkspaceId());

        workflow.Steps.Add(new AutomationWorkflowStepViewModel(NotifyStateChanged)
        {
            Name = "Step 1",
            Enabled = true,
            IncludePreviousStepOutput = false,
            PromptText = "Describe exactly how this step should transform the selected input files.",
            OutputPath = $"outputs/{NormalizeId(name)}_step_1_{{ts}}.md",
            StatusText = "Ready"
        });

        workflow.Steps.Add(new AutomationWorkflowStepViewModel(NotifyStateChanged)
        {
            Name = "Step 2",
            Enabled = true,
            IncludePreviousStepOutput = true,
            PromptText = "Continue the workflow using the previous step output and any additional files.",
            OutputPath = $"outputs/{NormalizeId(name)}_step_2_{{ts}}.md",
            StatusText = "Ready"
        });

        return workflow;
    }

    private string BuildConfigurationYaml()
    {
        var yaml = new StringBuilder();
        yaml.AppendLine("automation:");
        yaml.AppendLine($"  name: {EscapeYamlValue(AutomationName)}");
        yaml.AppendLine($"  goal: {EscapeYamlValue(AutomationGoal)}");
        yaml.AppendLine($"  output_root: {EscapeYamlValue(TargetDirectory)}");
        yaml.AppendLine("  workflows:");

        foreach (var workflow in Workflows)
        {
            yaml.AppendLine("    - name: " + EscapeYamlValue(workflow.Name));
            yaml.AppendLine("      enabled: " + workflow.Enabled.ToString().ToLowerInvariant());
            yaml.AppendLine("      goal: " + EscapeYamlValue(workflow.GoalText));
            yaml.AppendLine("      workspace_id: " + EscapeYamlValue(workflow.SelectedWorkspaceId));
            yaml.AppendLine("      workspace_root: " + EscapeYamlValue(workflow.SelectedWorkspaceRootDirectory));
            yaml.AppendLine("      steps:");

            foreach (var step in workflow.Steps)
            {
                yaml.AppendLine("        - name: " + EscapeYamlValue(step.Name));
                yaml.AppendLine("          enabled: " + step.Enabled.ToString().ToLowerInvariant());
                yaml.AppendLine("          inputs:");
                foreach (var inputPath in step.GetInputPaths())
                {
                    yaml.AppendLine("            - " + EscapeYamlValue(inputPath));
                }
                yaml.AppendLine("          include_previous_step_output: " + step.IncludePreviousStepOutput.ToString().ToLowerInvariant());
                yaml.AppendLine("          prompt: |");
                foreach (var line in (step.PromptText ?? string.Empty).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                {
                    yaml.AppendLine("            " + line);
                }
                yaml.AppendLine("          output: " + EscapeYamlValue(step.OutputPath));
            }
        }

        return yaml.ToString();
    }

    private string ResolveWorkspacePath(string path, string? workspaceRootDirectory = null)
    {
        var baseDirectory = string.IsNullOrWhiteSpace(workspaceRootDirectory)
            ? _services.Paths.ResolvedRootDirectory
            : workspaceRootDirectory;

        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private string ResolveOutputPath(string outputPath, string? workspaceRootDirectory = null)
    {
        var normalizedOutput = string.IsNullOrWhiteSpace(outputPath)
            ? string.Empty
            : outputPath.Replace("{ts}", "latest", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(normalizedOutput))
        {
            return string.Empty;
        }

        var baseDirectory = string.IsNullOrWhiteSpace(workspaceRootDirectory)
            ? TargetDirectory
            : workspaceRootDirectory;

        return Path.IsPathRooted(normalizedOutput)
            ? Path.GetFullPath(normalizedOutput)
            : Path.GetFullPath(Path.Combine(baseDirectory, normalizedOutput));
    }

    private string BuildDisplayPath(string fullPath, string? workspaceRootDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return string.Empty;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(workspaceRootDirectory) && fullPath.StartsWith(workspaceRootDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(workspaceRootDirectory, fullPath);
            }

            return Path.GetRelativePath(_services.Paths.ResolvedRootDirectory, fullPath);
        }
        catch
        {
            return fullPath;
        }
    }

    private static string NormalizeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "workflow";
        }

        return new string(value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray());
    }

    private static string EscapeYamlValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "\"\"";
        }

        if (value.Contains(':') || value.Contains('#') || value.Contains('\n') || value.Contains('\r') || value.Contains('"'))
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        return value;
    }
}

public partial class AutomationWorkflowViewModel : ObservableObject
{
    private Action? _changed;

    public ObservableCollection<WorkspaceDefinition> WorkspaceOptions { get; } = new();
    public ObservableCollection<AutomationWorkflowStepViewModel> Steps { get; } = new();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _goalText = string.Empty;

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private int _currentStepIndex = -1;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private WorkspaceDefinition? _selectedWorkspace;

    public AutomationWorkflowStepViewModel? CurrentStep =>
        CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;

    public bool IsOverviewPage => CurrentStepIndex < 0;
    public bool IsStepPage => CurrentStepIndex >= 0;
    public string StepCounterText => CurrentStepIndex < 0
        ? "Step 0 - Goal"
        : Steps.Count == 0
            ? "No steps"
            : $"Step {CurrentStepIndex + 1} of {Steps.Count}";
    public bool CanGoPreviousStep => CurrentStepIndex >= 0;
    public bool CanGoNextStep => CurrentStepIndex < Steps.Count - 1;
    public string SelectedWorkspaceId => SelectedWorkspace?.Id ?? string.Empty;
    public string SelectedWorkspaceName => SelectedWorkspace?.Name ?? "Select workspace";
    public string SelectedWorkspaceRootDirectory => SelectedWorkspace?.RootDirectory ?? string.Empty;
    public string SelectedWorkspaceSummary => string.IsNullOrWhiteSpace(SelectedWorkspaceRootDirectory) ? "No workspace selected." : SelectedWorkspaceRootDirectory;

    public AutomationWorkflowViewModel(Action? changed = null)
    {
        Attach(changed);
        Steps.CollectionChanged += OnStepsChanged;
    }

    public void Attach(Action? changed)
    {
        _changed = changed;
        foreach (var step in Steps)
        {
            step.Attach(changed);
        }
    }

    public void SetWorkspaceOptions(IEnumerable<WorkspaceDefinition> options, string? defaultWorkspaceId = null)
    {
        var previousWorkspaceId = SelectedWorkspace?.Id ?? string.Empty;

        WorkspaceOptions.Clear();
        foreach (var option in options)
        {
            WorkspaceOptions.Add(option.Clone());
        }

        var candidateId = !string.IsNullOrWhiteSpace(defaultWorkspaceId) && WorkspaceOptions.Any(candidate => string.Equals(candidate.Id, defaultWorkspaceId, StringComparison.OrdinalIgnoreCase))
            ? defaultWorkspaceId
            : previousWorkspaceId;

        if (string.IsNullOrWhiteSpace(candidateId) || !WorkspaceOptions.Any(candidate => string.Equals(candidate.Id, candidateId, StringComparison.OrdinalIgnoreCase)))
        {
            candidateId = WorkspaceOptions.FirstOrDefault()?.Id ?? string.Empty;
        }

        SelectedWorkspace = WorkspaceOptions.FirstOrDefault(candidate => string.Equals(candidate.Id, candidateId, StringComparison.OrdinalIgnoreCase))
            ?? WorkspaceOptions.FirstOrDefault();

        OnPropertyChanged(nameof(SelectedWorkspaceName));
        OnPropertyChanged(nameof(SelectedWorkspaceRootDirectory));
        OnPropertyChanged(nameof(SelectedWorkspaceSummary));
    }

    partial void OnNameChanged(string value) => _changed?.Invoke();
    partial void OnGoalTextChanged(string value) => _changed?.Invoke();
    partial void OnEnabledChanged(bool value) => _changed?.Invoke();
    partial void OnStatusTextChanged(string value) => _changed?.Invoke();

    partial void OnSelectedWorkspaceChanged(WorkspaceDefinition? value)
    {
        OnPropertyChanged(nameof(SelectedWorkspaceName));
        OnPropertyChanged(nameof(SelectedWorkspaceRootDirectory));
        OnPropertyChanged(nameof(SelectedWorkspaceSummary));
        _changed?.Invoke();
    }

    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(IsOverviewPage));
        OnPropertyChanged(nameof(IsStepPage));
        OnPropertyChanged(nameof(StepCounterText));
        OnPropertyChanged(nameof(CanGoPreviousStep));
        OnPropertyChanged(nameof(CanGoNextStep));
    }

    public AutomationWorkflowViewModel Clone(string? nameOverride = null, Action? changed = null)
    {
        var clone = new AutomationWorkflowViewModel(changed)
        {
            Name = nameOverride ?? Name,
            GoalText = GoalText,
            Enabled = Enabled,
            CurrentStepIndex = CurrentStepIndex,
            IsExpanded = IsExpanded,
            StatusText = StatusText,
        };

        clone.SetWorkspaceOptions(WorkspaceOptions, SelectedWorkspaceId);

        foreach (var step in Steps)
        {
            clone.Steps.Add(step.Clone(changed));
        }

        return clone;
    }

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (AutomationWorkflowStepViewModel step in e.NewItems)
            {
                step.Attach(_changed);
                step.AttachOwner(this);
            }
        }

        CurrentStepIndex = CurrentStepIndex < 0
            ? -1
            : Math.Clamp(CurrentStepIndex, 0, Math.Max(0, Steps.Count - 1));
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(IsOverviewPage));
        OnPropertyChanged(nameof(IsStepPage));
        OnPropertyChanged(nameof(StepCounterText));
        OnPropertyChanged(nameof(CanGoPreviousStep));
        OnPropertyChanged(nameof(CanGoNextStep));
        _changed?.Invoke();
    }
}

public partial class AutomationWorkflowStepViewModel : ObservableObject
{
    private Action? _changed;

    internal AutomationWorkflowViewModel? Owner { get; private set; }

    public ObservableCollection<AutomationWorkflowInputViewModel> InputFiles { get; } = new();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private bool _includePreviousStepOutput;

    [ObservableProperty]
    private string _promptText = string.Empty;

    [ObservableProperty]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _lastOutputPath = string.Empty;

    public AutomationWorkflowStepViewModel(Action? changed = null)
    {
        Attach(changed);
        InputFiles.CollectionChanged += OnInputFilesChanged;
    }

    internal void AttachOwner(AutomationWorkflowViewModel owner)
    {
        Owner = owner;
    }

    public string InputsText
    {
        get => string.Join(Environment.NewLine, GetInputPaths());
        set => SetInputPaths(AutomationExecutionService.ParsePathList(value));
    }

    public void Attach(Action? changed)
    {
        _changed = changed;
        foreach (var input in InputFiles)
        {
            input.Attach(changed);
        }
    }

    public void SetInputPaths(IEnumerable<string> inputPaths)
    {
        InputFiles.CollectionChanged -= OnInputFilesChanged;
        InputFiles.Clear();
        foreach (var path in inputPaths)
        {
            AddInputPath(path);
        }
        InputFiles.CollectionChanged += OnInputFilesChanged;
        _changed?.Invoke();
    }

    public AutomationWorkflowInputViewModel AddInputPath(string path)
    {
        var item = new AutomationWorkflowInputViewModel(_changed)
        {
            PathText = path?.Trim() ?? string.Empty
        };
        item.RemoveRequested = () => InputFiles.Remove(item);
        InputFiles.Add(item);
        return item;
    }

    public bool RemoveInputPath(AutomationWorkflowInputViewModel? item)
    {
        if (item is null)
        {
            return false;
        }

        return InputFiles.Remove(item);
    }

    public IReadOnlyList<string> GetInputPaths()
    {
        return InputFiles
            .Select(item => item.PathText?.Trim() ?? string.Empty)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    partial void OnNameChanged(string value) => _changed?.Invoke();
    partial void OnEnabledChanged(bool value) => _changed?.Invoke();
    partial void OnIncludePreviousStepOutputChanged(bool value) => _changed?.Invoke();
    partial void OnPromptTextChanged(string value) => _changed?.Invoke();
    partial void OnOutputPathChanged(string value) => _changed?.Invoke();
    partial void OnStatusTextChanged(string value) => _changed?.Invoke();
    partial void OnLastOutputPathChanged(string value) => _changed?.Invoke();

    private void OnInputFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (AutomationWorkflowInputViewModel input in e.NewItems)
            {
                input.Attach(_changed);
                input.RemoveRequested = () => InputFiles.Remove(input);
            }
        }

        _changed?.Invoke();
    }

    public AutomationWorkflowStepViewModel Clone(Action? changed = null)
    {
        var clone = new AutomationWorkflowStepViewModel(changed)
        {
            Name = Name,
            Enabled = Enabled,
            IncludePreviousStepOutput = IncludePreviousStepOutput,
            PromptText = PromptText,
            OutputPath = OutputPath,
            StatusText = StatusText,
            LastOutputPath = LastOutputPath
        };

        clone.SetInputPaths(GetInputPaths());
        return clone;
    }
}

public partial class AutomationWorkflowInputViewModel : ObservableObject
{
    private Action? _changed;

    [ObservableProperty]
    private string _pathText = string.Empty;

    public Action? RemoveRequested { get; set; }

    public AutomationWorkflowInputViewModel(Action? changed = null)
    {
        Attach(changed);
    }

    public void Attach(Action? changed)
    {
        _changed = changed;
    }

    partial void OnPathTextChanged(string value) => _changed?.Invoke();

    public void RequestRemove()
    {
        RemoveRequested?.Invoke();
    }
}

public partial class AutomationArtifactItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _artifactPath = string.Empty;

    [ObservableProperty]
    private string _displayPath = string.Empty;

    [ObservableProperty]
    private string _artifactKind = string.Empty;

    [ObservableProperty]
    private bool _exists;
}
