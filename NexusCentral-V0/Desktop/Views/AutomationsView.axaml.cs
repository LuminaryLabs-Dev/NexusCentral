using System.Linq;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NexusCentral_Avalonia.ViewModels;

namespace NexusCentral_Avalonia.Views;

public partial class AutomationsView : UserControl
{
    public AutomationsView()
    {
        InitializeComponent();
    }

    private async void OnFindFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Output Directory",
            AllowMultiple = false
        });

        var selectedPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            viewModel.SetTargetDirectory(selectedPath);
        }
    }

    private async void OnAddInputClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AutomationWorkflowStepViewModel step || step.Owner is not { } workflow || DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(workflow.SelectedWorkspaceRootDirectory))
        {
            viewModel.StatusMessage = "Select a workspace before adding input files.";
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        var suggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(workflow.SelectedWorkspaceRootDirectory);
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Input Files",
            AllowMultiple = true,
            SuggestedStartLocation = suggestedStartLocation
        });

        var addedCount = 0;
        foreach (var file in files)
        {
            var localPath = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath))
            {
                continue;
            }

            if (!TryMakeWorkspaceRelative(workflow.SelectedWorkspaceRootDirectory, localPath, out var relativePath))
            {
                continue;
            }

            if (step.GetInputPaths().Any(path => string.Equals(path, relativePath, System.StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            step.AddInputPath(relativePath);
            addedCount++;
        }

        viewModel.StatusMessage = addedCount == 0
            ? "No input files were added."
            : $"Added {addedCount} input file(s).";
    }

    private void OnRemoveInputClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AutomationWorkflowInputViewModel input || DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        input.RequestRemove();
        viewModel.StatusMessage = "Removed input file.";
    }

    private void OnAddStepClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AutomationWorkflowViewModel workflow || DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        viewModel.AddStepToWorkflow(workflow);
    }

    private void OnDuplicateWorkflowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AutomationWorkflowViewModel workflow || DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        viewModel.DuplicateWorkflow(workflow);
    }

    private void OnMoveWorkflowUpClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AutomationWorkflowViewModel workflow || DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        viewModel.MoveWorkflowUp(workflow);
    }

    private void OnMoveWorkflowDownClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AutomationWorkflowViewModel workflow || DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        viewModel.MoveWorkflowDown(workflow);
    }

    private void OnRemoveWorkflowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AutomationWorkflowViewModel workflow || DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        viewModel.RemoveWorkflow(workflow);
    }

    private void OnPreviousStepClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AutomationWorkflowViewModel workflow || DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        viewModel.ShowPreviousStep(workflow);
    }

    private void OnNextStepClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AutomationWorkflowViewModel workflow || DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        viewModel.ShowNextStep(workflow);
    }

    private void OnMoveStepUpClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AutomationWorkflowStepViewModel step || step.Owner is not { } workflow || DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        viewModel.MoveStepUp(workflow, step);
    }

    private void OnMoveStepDownClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AutomationWorkflowStepViewModel step || step.Owner is not { } workflow || DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        viewModel.MoveStepDown(workflow, step);
    }

    private void OnRemoveStepClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not AutomationWorkflowStepViewModel step || step.Owner is not { } workflow || DataContext is not AutomationsViewModel viewModel)
        {
            return;
        }

        viewModel.RemoveStep(workflow, step);
    }

    private static bool TryMakeWorkspaceRelative(string workspaceRoot, string fullPath, out string relativePath)
    {
        relativePath = string.Empty;

        try
        {
            var fullWorkspaceRoot = Path.GetFullPath(workspaceRoot);
            var fullInputPath = Path.GetFullPath(fullPath);
            var candidate = Path.GetRelativePath(fullWorkspaceRoot, fullInputPath);

            if (candidate.StartsWith("..", System.StringComparison.Ordinal) || Path.IsPathRooted(candidate))
            {
                return false;
            }

            relativePath = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
