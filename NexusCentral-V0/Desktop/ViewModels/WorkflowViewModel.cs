using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Models;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.ViewModels;

public partial class WorkflowViewModel : ViewModelBase
{
    public const string WorkflowSelectorElementId = "workflows.selector";
    public const string SearchElementId = "workflows.search";
    public const string BundleIdElementId = "workflows.bundle_id";
    public const string RunWorkflowElementId = "workflows.run";
    public const string ValidateWorkflowElementId = "workflows.validate";
    public const string StopWorkflowElementId = "workflows.stop";
    public const string SaveWorkflowElementId = "workflows.save_workflow";
    public const string SaveBundleElementId = "workflows.save_bundle";
    public const string LoadBundleElementId = "workflows.load_bundle";
    public const string AddInputElementId = "workflows.add_input";
    public const string AddAgentStepElementId = "workflows.add_agent_step";
    public const string AddToolStepElementId = "workflows.add_tool_step";

    private readonly AppServices _services;
    private bool _suppressLibrarySelection;

    public ObservableCollection<WorkflowLibraryItemViewModel> WorkflowLibrary { get; } = new();

    [ObservableProperty]
    private WorkflowLibraryItemViewModel? _selectedLibraryWorkflow;

    [ObservableProperty]
    private WorkflowDocumentViewModel? _currentWorkflow;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _bundleIdText = string.Empty;

    [ObservableProperty]
    private string _statusText = "Workflow catalog ready.";

    [ObservableProperty]
    private string _filterSummaryText = string.Empty;

    public WorkflowViewModel(AppServices services)
    {
        _services = services;
        _services.Workflows.WorkflowsChanged += OnWorkflowCatalogChanged;
        _services.Wrappers.WrappersChanged += OnReferenceCatalogChanged;
        _services.Templates.TemplatesChanged += OnReferenceCatalogChanged;
        _services.WorkflowRuns.RunUpdated += OnWorkflowRunUpdated;

        RefreshLibrary();
        if (SelectedLibraryWorkflow is not null)
        {
            LoadWorkflowById(SelectedLibraryWorkflow.Id);
        }
        else
        {
            CurrentWorkflow = WorkflowDocumentViewModel.FromModel(_services.Workflows.CreateEmptyWorkflow(), _services);
            BundleIdText = CurrentWorkflow.Id;
        }
    }

    public bool HasCurrentWorkflow => CurrentWorkflow is not null;

    partial void OnSearchTextChanged(string value)
    {
        RefreshLibrary();
    }

    partial void OnSelectedLibraryWorkflowChanged(WorkflowLibraryItemViewModel? value)
    {
        if (_suppressLibrarySelection || value is null)
        {
            return;
        }

        LoadWorkflowById(value.Id);
    }

    partial void OnCurrentWorkflowChanged(WorkflowDocumentViewModel? value)
    {
        OnPropertyChanged(nameof(HasCurrentWorkflow));
    }

    [RelayCommand]
    private void Refresh()
    {
        _services.Workflows.Reload();
        if (CurrentWorkflow is not null)
        {
            CurrentWorkflow.RefreshReferenceData();
        }

        StatusText = $"Loaded {WorkflowLibrary.Count} workflows.";
    }

    [RelayCommand]
    private void NewWorkflow()
    {
        CurrentWorkflow = WorkflowDocumentViewModel.FromModel(_services.Workflows.CreateEmptyWorkflow(), _services);
        BundleIdText = CurrentWorkflow.Id;
        SetSelectedLibraryWorkflow(null);
        StatusText = $"Created new workflow '{CurrentWorkflow.Name}'.";
    }

    [RelayCommand]
    private void DuplicateWorkflow()
    {
        if (CurrentWorkflow is null)
        {
            return;
        }

        var clone = CurrentWorkflow.ToModel();
        clone.Id = $"{clone.Id}_copy";
        clone.Name = $"{clone.Name} Copy";
        CurrentWorkflow = WorkflowDocumentViewModel.FromModel(clone, _services);
        BundleIdText = CurrentWorkflow.Id;
        SetSelectedLibraryWorkflow(null);
        StatusText = $"Duplicated workflow as '{CurrentWorkflow.Name}'.";
    }

    [RelayCommand]
    private void SaveWorkflow()
    {
        if (CurrentWorkflow is null)
        {
            return;
        }

        var path = _services.Workflows.SaveWorkflow(CurrentWorkflow.ToModel());
        CurrentWorkflow.MarkPersisted("local", path);
        BundleIdText = CurrentWorkflow.Id;
        RefreshLibrary(CurrentWorkflow.Id);
        StatusText = $"Saved workflow to {path}.";
    }

    [RelayCommand]
    private void SaveBundle()
    {
        if (CurrentWorkflow is null)
        {
            return;
        }

        var bundleId = string.IsNullOrWhiteSpace(BundleIdText) ? CurrentWorkflow.Id : BundleIdText.Trim();
        var path = _services.Workflows.SaveBundle(CurrentWorkflow.ToBundle(), bundleId);
        BundleIdText = Path.GetFileNameWithoutExtension(path);
        StatusText = $"Saved workflow bundle to {path}.";
    }

    [RelayCommand]
    private void LoadBundle()
    {
        var bundleId = (BundleIdText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(bundleId))
        {
            StatusText = "Enter a workflow bundle id or path first.";
            return;
        }

        var bundle = _services.Workflows.LoadBundle(bundleId);
        if (bundle is null)
        {
            StatusText = $"Workflow bundle '{bundleId}' was not found.";
            return;
        }

        CurrentWorkflow = WorkflowDocumentViewModel.FromBundle(bundle, _services);
        BundleIdText = bundleId;
        SelectLibraryWorkflow(CurrentWorkflow.Id);
        StatusText = $"Loaded workflow bundle '{bundleId}'.";
    }

    [RelayCommand]
    private async Task RunWorkflowAsync()
    {
        if (CurrentWorkflow is null)
        {
            return;
        }

        var validation = _services.WorkflowValidation.Validate(CurrentWorkflow.ToModel());
        CurrentWorkflow.ApplyValidation(validation);
        if (!validation.IsValid)
        {
            StatusText = "Workflow validation failed. Fix errors before running.";
            await Task.CompletedTask;
            return;
        }

        var handle = _services.WorkflowRuns.StartRun(CurrentWorkflow.ToModel(), CurrentWorkflow.GetCurrentLabelValues());
        CurrentWorkflow.StartRun(handle.RunId);
        StatusText = $"Workflow run {handle.RunId} started.";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ValidateWorkflow()
    {
        if (CurrentWorkflow is null)
        {
            return;
        }

        var validation = _services.WorkflowValidation.Validate(CurrentWorkflow.ToModel());
        CurrentWorkflow.ApplyValidation(validation);
        StatusText = validation.IsValid
            ? $"Workflow '{CurrentWorkflow.Name}' is ready to run."
            : $"Workflow '{CurrentWorkflow.Name}' has validation errors.";
    }

    [RelayCommand]
    private void StopWorkflow()
    {
        if (CurrentWorkflow is null || string.IsNullOrWhiteSpace(CurrentWorkflow.ActiveRunId))
        {
            StatusText = "No active workflow run to stop.";
            return;
        }

        var stopped = _services.WorkflowRuns.StopRun(CurrentWorkflow.ActiveRunId);
        StatusText = stopped
            ? $"Stop requested for workflow run {CurrentWorkflow.ActiveRunId}."
            : $"Workflow run {CurrentWorkflow.ActiveRunId} was not found.";
    }

    [RelayCommand]
    private void AddInputLabel()
    {
        CurrentWorkflow?.AddInputLabel();
        StatusText = "Added workflow input label.";
    }

    [RelayCommand]
    private void AddAgentStep()
    {
        CurrentWorkflow?.AddStep("agent");
        StatusText = "Added agent step.";
    }

    [RelayCommand]
    private void AddToolStep()
    {
        CurrentWorkflow?.AddStep("tool_call");
        StatusText = "Added tool-call step.";
    }

    public static string BuildStepElementId(string stepId) => $"workflows.step.{stepId}";

    public static string BuildStepRunElementId(string stepId) => $"workflows.step_run.{stepId}";

    private void OnWorkflowCatalogChanged()
    {
        RefreshLibrary(CurrentWorkflow?.Id ?? SelectedLibraryWorkflow?.Id);
    }

    private void OnReferenceCatalogChanged()
    {
        CurrentWorkflow?.RefreshReferenceData();
    }

    private void OnWorkflowRunUpdated(WorkflowRunState run)
    {
        if (CurrentWorkflow is null || !string.Equals(CurrentWorkflow.Id, run.WorkflowId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentWorkflow.ApplyRun(run);
        StatusText = $"Workflow {run.WorkflowName} is {run.Status}.";
    }

    private void LoadWorkflowById(string workflowId)
    {
        var workflow = _services.Workflows.FindById(workflowId);
        if (workflow is null)
        {
            StatusText = $"Workflow '{workflowId}' was not found.";
            return;
        }

        CurrentWorkflow = WorkflowDocumentViewModel.FromModel(workflow, _services);
        BundleIdText = workflow.Id;
        StatusText = $"Loaded workflow '{workflow.Name}'.";
    }

    public void OpenWorkflowDocument(WorkflowDefinition workflow, string statusText)
    {
        CurrentWorkflow = WorkflowDocumentViewModel.FromModel(workflow, _services);
        BundleIdText = workflow.Id;
        SetSelectedLibraryWorkflow(null);
        StatusText = statusText;
    }

    private void RefreshLibrary(string? preferredWorkflowId = null)
    {
        var selectedId = preferredWorkflowId ?? SelectedLibraryWorkflow?.Id ?? CurrentWorkflow?.Id ?? string.Empty;
        var workflows = _services.Workflows.GetWorkflows()
            .Where(MatchesSearch)
            .OrderBy(workflow => workflow.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        WorkflowLibrary.Clear();
        foreach (var workflow in workflows)
        {
            WorkflowLibrary.Add(new WorkflowLibraryItemViewModel(workflow));
        }

        FilterSummaryText = $"{WorkflowLibrary.Count} workflow{(WorkflowLibrary.Count == 1 ? string.Empty : "s")}";
        SelectLibraryWorkflow(selectedId);
    }

    private void SelectLibraryWorkflow(string? workflowId)
    {
        _suppressLibrarySelection = true;
        SelectedLibraryWorkflow = string.IsNullOrWhiteSpace(workflowId)
            ? null
            : WorkflowLibrary.FirstOrDefault(item => string.Equals(item.Id, workflowId, StringComparison.OrdinalIgnoreCase));
        _suppressLibrarySelection = false;
    }

    private void SetSelectedLibraryWorkflow(WorkflowLibraryItemViewModel? workflow)
    {
        _suppressLibrarySelection = true;
        SelectedLibraryWorkflow = workflow;
        _suppressLibrarySelection = false;
    }

    private bool MatchesSearch(WorkflowDefinition workflow)
    {
        var needle = (SearchText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(needle))
        {
            return true;
        }

        return workflow.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
               workflow.Id.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
               workflow.Description.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
               workflow.Tags.Any(tag => tag.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
