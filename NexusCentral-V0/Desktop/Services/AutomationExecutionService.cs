using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class AutomationExecutionService
{
    private readonly AppPathsService _paths;
    private readonly RuntimeService _runtime;
    private readonly LogService _logs;
    private bool _stopRequested;

    public AutomationExecutionService(AppPathsService paths, RuntimeService runtime, LogService logs)
    {
        _paths = paths;
        _runtime = runtime;
        _logs = logs;
    }

    public void RequestStop()
    {
        _stopRequested = true;
    }

    public async Task<AutomationExecutionResult> RunAsync(AutomationExecutionRequest request, IProgress<AutomationExecutionProgress>? progress = null)
    {
        _stopRequested = false;

        var result = new AutomationExecutionResult();
        var generatedFiles = new List<string>();
        string previousStepOutputPath = string.Empty;
        var fallbackTargetDirectory = string.IsNullOrWhiteSpace(request.TargetDirectory)
            ? _paths.ResolvedRootDirectory
            : request.TargetDirectory;

        Directory.CreateDirectory(fallbackTargetDirectory);

        for (var workflowIndex = 0; workflowIndex < request.Workflows.Count; workflowIndex++)
        {
            var workflow = request.Workflows[workflowIndex];
            if (!workflow.Enabled)
            {
                continue;
            }

            for (var stepIndex = 0; stepIndex < workflow.Steps.Count; stepIndex++)
            {
                if (_stopRequested)
                {
                    result.Stopped = true;
                    result.StatusMessage = "Automation stopped before the next step started.";
                    result.GeneratedFiles = generatedFiles;
                    result.FinalOutputPath = previousStepOutputPath;
                    return result;
                }

                var step = workflow.Steps[stepIndex];
                if (!step.Enabled)
                {
                    continue;
                }

                progress?.Report(new AutomationExecutionProgress
                {
                    WorkflowName = workflow.Name,
                    StepName = step.Name,
                    WorkflowIndex = workflowIndex,
                    StepIndex = stepIndex,
                    Status = "running",
                    Message = "Aggregating inputs..."
                });

                var resolvedInputPaths = ResolveInputPaths(workflow.WorkspaceRootDirectory, step, previousStepOutputPath);
                var missingInputs = resolvedInputPaths.Where(path => !File.Exists(path)).ToList();
                if (missingInputs.Count > 0)
                {
                    result.StatusMessage = $"Step '{step.Name}' is missing input files: {string.Join(", ", missingInputs)}";
                    progress?.Report(new AutomationExecutionProgress
                    {
                        WorkflowName = workflow.Name,
                        StepName = step.Name,
                        WorkflowIndex = workflowIndex,
                        StepIndex = stepIndex,
                        Status = "error",
                        Error = result.StatusMessage,
                        Message = result.StatusMessage
                    });
                    return result;
                }

                var outputPath = ResolveOutputPath(workflow.WorkspaceRootDirectory, fallbackTargetDirectory, step.OutputPath, workflow.Name, step.Name);
                var prompt = await BuildPromptAsync(request, workflow, step, resolvedInputPaths);

                progress?.Report(new AutomationExecutionProgress
                {
                    WorkflowName = workflow.Name,
                    StepName = step.Name,
                    WorkflowIndex = workflowIndex,
                    StepIndex = stepIndex,
                    Status = "running",
                    Message = "Calling /thinking...",
                    OutputPath = outputPath
                });

                var invocation = await _runtime.RunThinkingAsync(prompt);
                if (!invocation.Ok)
                {
                    result.StatusMessage = string.IsNullOrWhiteSpace(invocation.Error)
                        ? $"Step '{step.Name}' failed."
                        : invocation.Error;
                    progress?.Report(new AutomationExecutionProgress
                    {
                        WorkflowName = workflow.Name,
                        StepName = step.Name,
                        WorkflowIndex = workflowIndex,
                        StepIndex = stepIndex,
                        Status = "error",
                        OutputPath = outputPath,
                        Error = result.StatusMessage,
                        Message = result.StatusMessage
                    });
                    _logs.Append("error", "automations", result.StatusMessage);
                    return result;
                }

                var outputDirectory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                await File.WriteAllTextAsync(outputPath, invocation.Output ?? string.Empty);
                generatedFiles.Add(outputPath);
                previousStepOutputPath = outputPath;

                progress?.Report(new AutomationExecutionProgress
                {
                    WorkflowName = workflow.Name,
                    StepName = step.Name,
                    WorkflowIndex = workflowIndex,
                    StepIndex = stepIndex,
                    Status = "done",
                    OutputPath = outputPath,
                    Message = $"Wrote {outputPath}"
                });
            }
        }

        result.Success = true;
        result.GeneratedFiles = generatedFiles;
        result.FinalOutputPath = previousStepOutputPath;
        result.StatusMessage = string.IsNullOrWhiteSpace(previousStepOutputPath)
            ? "Automation completed with no enabled steps."
            : $"Automation complete. Final output: {previousStepOutputPath}";
        return result;
    }

    public static List<string> ParsePathList(string text)
    {
        return (text ?? string.Empty)
            .Split(new[] { "\r\n", "\n", ",", ";" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> ResolveInputPaths(string workflowRootDirectory, AutomationStepExecutionRequest step, string previousStepOutputPath)
    {
        var baseDirectory = string.IsNullOrWhiteSpace(workflowRootDirectory)
            ? _paths.ResolvedRootDirectory
            : workflowRootDirectory;

        var resolved = step.InputPaths
            .Select(path => ResolvePath(path, baseDirectory))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        if (step.IncludePreviousStepOutput && !string.IsNullOrWhiteSpace(previousStepOutputPath))
        {
            resolved.Add(previousStepOutputPath);
        }

        return resolved
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<string> BuildPromptAsync(
        AutomationExecutionRequest automation,
        AutomationWorkflowExecutionRequest workflow,
        AutomationStepExecutionRequest step,
        IReadOnlyList<string> inputPaths)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Automation:");
        builder.AppendLine(automation.Name);
        builder.AppendLine();
        builder.AppendLine("Automation Goal:");
        builder.AppendLine(automation.Goal);
        builder.AppendLine();
        builder.AppendLine("Workflow:");
        builder.AppendLine(workflow.Name);
        if (!string.IsNullOrWhiteSpace(workflow.WorkspaceRootDirectory))
        {
            builder.AppendLine();
            builder.AppendLine("Workspace Root:");
            builder.AppendLine(workflow.WorkspaceRootDirectory);
        }
        if (!string.IsNullOrWhiteSpace(workflow.Goal))
        {
            builder.AppendLine();
            builder.AppendLine("Workflow Goal:");
            builder.AppendLine(workflow.Goal);
        }

        builder.AppendLine();
        builder.AppendLine("Step:");
        builder.AppendLine(step.Name);
        builder.AppendLine();
        builder.AppendLine("Prompt:");
        builder.AppendLine(step.Prompt);
        builder.AppendLine();
        builder.AppendLine("Input Files:");
        foreach (var inputPath in inputPaths)
        {
            builder.AppendLine($"- {inputPath}");
        }

        for (var index = 0; index < inputPaths.Count; index++)
        {
            var inputPath = inputPaths[index];
            var relativePath = GetDisplayPath(inputPath, workflow.WorkspaceRootDirectory);
            var content = await File.ReadAllTextAsync(inputPath);
            builder.AppendLine();
            builder.AppendLine($"--- File {index + 1}: {relativePath} ---");
            builder.AppendLine(content);
        }

        return builder.ToString();
    }

    private string ResolveOutputPath(string workflowRootDirectory, string fallbackTargetDirectory, string outputPath, string workflowName, string stepName)
    {
        var normalizedOutput = string.IsNullOrWhiteSpace(outputPath)
            ? $"{NormalizeId(workflowName)}_{NormalizeId(stepName)}_output_{{ts}}.md"
            : outputPath.Trim();

        normalizedOutput = normalizedOutput.Replace("{ts}", DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss"));
        var baseDirectory = !string.IsNullOrWhiteSpace(workflowRootDirectory)
            ? workflowRootDirectory
            : fallbackTargetDirectory;
        return ResolvePath(normalizedOutput, baseDirectory);
    }

    private string ResolvePath(string path, string baseDirectory)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private string GetDisplayPath(string fullPath, string? workspaceRoot = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(workspaceRoot) && fullPath.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(workspaceRoot, fullPath);
            }

            return Path.GetRelativePath(_paths.ResolvedRootDirectory, fullPath);
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
            return "step";
        }

        return new string(value.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray());
    }
}
