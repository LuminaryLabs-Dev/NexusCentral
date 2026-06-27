using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class WorkflowRunService
{
    private const string DefaultWorkflowTemplateId = "workflow_agent_step";

    private readonly AppPathsService _paths;
    private readonly WorkflowCatalogService _catalog;
    private readonly IntegrationRegistryService _integrations;
    private readonly WrapperRunService _wrapperRuns;
    private readonly LogService _logs;
    private readonly object _lock = new();
    private readonly Dictionary<string, WorkflowRunState> _runs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _cancellations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<WorkflowRunState>> _completionTasks = new(StringComparer.OrdinalIgnoreCase);

    public event Action<WorkflowRunState>? RunUpdated;

    public WorkflowRunService(
        AppPathsService paths,
        WorkflowCatalogService catalog,
        IntegrationRegistryService integrations,
        WrapperRunService wrapperRuns,
        LogService logs)
    {
        _paths = paths;
        _catalog = catalog;
        _integrations = integrations;
        _wrapperRuns = wrapperRuns;
        _logs = logs;
    }

    public WorkflowRunHandle StartRun(WorkflowDefinition workflow, IReadOnlyDictionary<string, string>? inputValues = null)
    {
        return StartInternal(workflow, inputValues, null, false);
    }

    public WorkflowRunHandle StartStepRun(WorkflowDefinition workflow, string stepId, IReadOnlyDictionary<string, string>? inputValues = null)
    {
        return StartInternal(workflow, inputValues, stepId, true);
    }

    public WorkflowRunState? GetRun(string? runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return null;
        }

        lock (_lock)
        {
            return _runs.TryGetValue(runId, out var state) ? state.Clone() : null;
        }
    }

    public WorkflowStepRunState? GetStepRun(string? runId, string? stepId)
    {
        var run = GetRun(runId);
        if (run is null || string.IsNullOrWhiteSpace(stepId))
        {
            return null;
        }

        return run.Steps.FirstOrDefault(step => string.Equals(step.StepId, stepId, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<WorkflowRunState> GetRecentRuns(int limit = 20)
    {
        lock (_lock)
        {
            return _runs.Values
                .OrderByDescending(run => run.CreatedAtUtc)
                .Take(Math.Max(1, limit))
                .Select(run => run.Clone())
                .ToList();
        }
    }

    public bool StopRun(string? runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return false;
        }

        CancellationTokenSource? cts;
        lock (_lock)
        {
            _cancellations.TryGetValue(runId, out cts);
        }

        if (cts is null)
        {
            return false;
        }

        cts.Cancel();
        UpdateRun(runId, run =>
        {
            run.Status = "stopping";
        });
        return true;
    }

    private WorkflowRunHandle StartInternal(WorkflowDefinition workflow, IReadOnlyDictionary<string, string>? inputValues, string? singleStepId, bool singleStep)
    {
        var clonedWorkflow = workflow.Clone();
        var runId = $"wfrun-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..44];
        var stepStates = clonedWorkflow.Steps
            .Where(step => !singleStep || string.Equals(step.Id, singleStepId, StringComparison.OrdinalIgnoreCase))
            .Select(step => new WorkflowStepRunState
            {
                StepId = step.Id,
                StepName = step.Name,
                Type = step.Type,
                Status = step.Enabled ? "queued" : "skipped",
                StatusMessage = step.Enabled ? "Queued" : "Disabled"
            })
            .ToList();
        var initialLabels = BuildInitialLabels(clonedWorkflow, inputValues);
        var state = new WorkflowRunState
        {
            RunId = runId,
            WorkflowId = clonedWorkflow.Id,
            WorkflowName = clonedWorkflow.Name,
            Status = "queued",
            LabelValues = initialLabels,
            Steps = stepStates,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var cts = new CancellationTokenSource();
        lock (_lock)
        {
            _runs[runId] = state.Clone();
            _cancellations[runId] = cts;
        }
        RunUpdated?.Invoke(state.Clone());

        var completion = ExecuteRunAsync(runId, clonedWorkflow, initialLabels, singleStepId, singleStep, cts.Token);
        lock (_lock)
        {
            _completionTasks[runId] = completion;
        }

        return new WorkflowRunHandle(runId, completion);
    }

    private async Task<WorkflowRunState> ExecuteRunAsync(
        string runId,
        WorkflowDefinition workflow,
        Dictionary<string, string> labelValues,
        string? singleStepId,
        bool singleStep,
        CancellationToken cancellationToken)
    {
        UpdateRun(runId, run =>
        {
            run.Status = "running";
            run.StartedAtUtc = DateTimeOffset.UtcNow;
        });

        string previousPrimaryOutput = string.Empty;
        foreach (var step in workflow.Steps)
        {
            if (singleStep && !string.Equals(step.Id, singleStepId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!step.Enabled)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var stepState = GetMutableStep(runId, step.Id);
            if (stepState is null)
            {
                continue;
            }

            var workingDirectory = ResolveWorkingDirectory(workflow.WorkingDirectory, step.WorkingDirectoryOverride);
            UpdateRun(runId, run =>
            {
                run.CurrentStepId = step.Id;
            });
            stepState.WorkingDirectory = workingDirectory;
            stepState.Status = "running";
            stepState.StatusMessage = "Running";
            stepState.StartedAtUtc = DateTimeOffset.UtcNow;
            EmitRun(runId);

            StepExecutionResult result;
            try
            {
                result = string.Equals(step.Type, "tool_call", StringComparison.OrdinalIgnoreCase)
                    ? await ExecuteToolStepAsync(runId, workflow, step, labelValues, workingDirectory, cancellationToken)
                    : await ExecuteAgentStepAsync(runId, workflow, step, labelValues, previousPrimaryOutput, workingDirectory, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                stepState.Status = "stopped";
                stepState.StatusMessage = "Stopped";
                stepState.CompletedAtUtc = DateTimeOffset.UtcNow;
                UpdateRun(runId, run =>
                {
                    run.Status = "stopped";
                    run.CompletedAtUtc = DateTimeOffset.UtcNow;
                });
                Cleanup(runId);
                return GetRun(runId)!;
            }
            catch (Exception ex)
            {
                result = new StepExecutionResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }

            stepState.FinalText = result.FinalText;
            stepState.ResponseText = result.ResponseText;
            stepState.ChildRunId = result.ChildRunId;
            stepState.Logs = result.Logs.ToList();
            stepState.ArtifactOutputs = result.Artifacts.Select(artifact => new WrapperArtifact
            {
                Label = artifact.Label,
                Path = artifact.Path,
                Kind = artifact.Kind
            }).ToList();
            stepState.OutputLabels = result.OutputLabels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            stepState.ResolvedPrompt = result.ResolvedPrompt;
            stepState.ResolvedToolCall = result.ResolvedToolCall;
            stepState.Error = result.Error;
            stepState.Status = result.Success ? "done" : "error";
            stepState.StatusMessage = result.Success ? "Completed" : "Failed";
            stepState.CompletedAtUtc = DateTimeOffset.UtcNow;

            foreach (var pair in result.OutputLabels)
            {
                labelValues[pair.Key] = pair.Value;
            }
            UpdateRun(runId, run =>
            {
                run.LabelValues = labelValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            });

            previousPrimaryOutput = result.PrimaryOutput;
            EmitRun(runId);

            if (!result.Success && workflow.GlobalSettings.StopOnError && !step.ContinueOnError)
            {
                UpdateRun(runId, run =>
                {
                    run.Status = "error";
                    run.Error = string.IsNullOrWhiteSpace(result.Error) ? $"Workflow step '{step.Name}' failed." : result.Error;
                    run.CompletedAtUtc = DateTimeOffset.UtcNow;
                });
                Cleanup(runId);
                return GetRun(runId)!;
            }
        }

        UpdateRun(runId, run =>
        {
            if (!string.Equals(run.Status, "error", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(run.Status, "stopped", StringComparison.OrdinalIgnoreCase))
            {
                run.Status = "done";
                run.CompletedAtUtc = DateTimeOffset.UtcNow;
            }
        });
        Cleanup(runId);
        return GetRun(runId)!;
    }

    private async Task<StepExecutionResult> ExecuteAgentStepAsync(
        string runId,
        WorkflowDefinition workflow,
        WorkflowStepDefinition step,
        Dictionary<string, string> sharedLabels,
        string previousPrimaryOutput,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var templateContext = BuildTemplateContext(sharedLabels, workingDirectory, previousPrimaryOutput, step);
        var prompt = RenderTemplate(step.PromptTemplate, templateContext);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = $"Execute workflow step '{step.Name}'.";
        }

        var modelId = string.IsNullOrWhiteSpace(step.ModelIdOverride)
            ? workflow.GlobalSettings.DefaultModelId
            : step.ModelIdOverride;
        var maxTokens = step.MaxTokensOverride > 0 ? step.MaxTokensOverride : Math.Max(1, workflow.GlobalSettings.DefaultMaxTokens);
        var timeoutSec = step.TimeoutSecOverride > 0 ? step.TimeoutSecOverride : Math.Max(1, workflow.GlobalSettings.DefaultTimeoutSec);
        var wrapperId = string.IsNullOrWhiteSpace(step.WrapperIdOverride)
            ? workflow.GlobalSettings.DefaultWrapperId
            : step.WrapperIdOverride;
        var artifactInputs = step.ArtifactInputs
            .Select(path => ResolvePathValue(RenderTemplate(path, templateContext), workingDirectory))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        var progressLogs = new List<string>();
        var progress = new Progress<WrapperRunStreamEvent>(streamEvent =>
        {
            var stepState = GetMutableStep(runId, step.Id);
            if (stepState is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(streamEvent.Text))
            {
                stepState.Logs.Add(streamEvent.Text);
            }

            if (streamEvent.Artifact is not null && !stepState.ArtifactOutputs.Any(existing => string.Equals(existing.Path, streamEvent.Artifact.Path, StringComparison.OrdinalIgnoreCase)))
            {
                stepState.ArtifactOutputs.Add(streamEvent.Artifact);
            }

            stepState.StatusMessage = streamEvent.Type switch
            {
                "status" => string.IsNullOrWhiteSpace(streamEvent.Status) ? "Running" : streamEvent.Status,
                "error" => "Error",
                _ => stepState.StatusMessage
            };
            EmitRun(runId);
        });

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["workspace_root"] = _paths.ResolvedRootDirectory,
            ["working_directory"] = workingDirectory,
            ["workflow_run_id"] = runId,
            ["workflow_step_id"] = step.Id,
            ["timeout_sec"] = timeoutSec.ToString(),
            ["workflow_global_flags"] = workflow.GlobalSettings.GlobalFlags ?? string.Empty,
            ["workflow_step_raw_flags"] = step.RawFlags ?? string.Empty,
            ["workflow_structured_flags_json"] = JsonSerializer.Serialize(step.Flags ?? new List<TemplateArgDefinition>()),
            ["workflow_environment_text"] = workflow.GlobalSettings.Environment ?? string.Empty
        };
        if (sharedLabels.TryGetValue("session_id", out var workflowSessionId) && !string.IsNullOrWhiteSpace(workflowSessionId))
        {
            metadata["session_id"] = workflowSessionId;
        }

        metadata["goal_context"] = sharedLabels.TryGetValue("goal_context", out var goalContext) && !string.IsNullOrWhiteSpace(goalContext)
            ? goalContext
            : prompt;

        var handle = _wrapperRuns.StartRun(new WrapperRunRequest
        {
            TemplateId = string.IsNullOrWhiteSpace(step.TemplateId) ? DefaultWorkflowTemplateId : step.TemplateId,
            WrapperId = wrapperId,
            Mode = "wrapper",
            Prompt = prompt,
            RootDirectory = workingDirectory,
            SessionId = metadata.TryGetValue("session_id", out var sessionId) ? sessionId : string.Empty,
            ArtifactInputs = artifactInputs,
            ModelId = modelId ?? string.Empty,
            MaxTokens = maxTokens,
            NexuscentralBaseUrl = "http://localhost:3005",
            LlmToolRoute = "/nexuscentral/thinking",
            Metadata = metadata
        }, progress);

        var mutable = GetMutableStep(runId, step.Id);
        if (mutable is not null)
        {
            mutable.ChildRunId = handle.RunId;
            EmitRun(runId);
        }

        using var _ = cancellationToken.Register(() => _wrapperRuns.CancelRun(handle.RunId));
        var wrapperState = await handle.Completion;
        progressLogs.AddRange(wrapperState.Logs);

        var outputLabels = ResolveAgentOutputLabels(step, wrapperState);
        return new StepExecutionResult
        {
            Success = string.Equals(wrapperState.Status, "done", StringComparison.OrdinalIgnoreCase),
            PrimaryOutput = wrapperState.FinalText,
            FinalText = wrapperState.FinalText,
            Error = wrapperState.Error,
            ChildRunId = handle.RunId,
            Logs = wrapperState.Logs,
            Artifacts = wrapperState.ArtifactOutputs,
            OutputLabels = outputLabels,
            ResolvedPrompt = prompt
        };
    }

    private async Task<StepExecutionResult> ExecuteToolStepAsync(
        string runId,
        WorkflowDefinition workflow,
        WorkflowStepDefinition step,
        Dictionary<string, string> sharedLabels,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var integration = _integrations.GetIntegration(step.IntegrationId);
        if (integration is null || !integration.Enabled)
        {
            return new StepExecutionResult
            {
                Success = false,
                Error = $"Integration '{step.IntegrationId}' is not available."
            };
        }

        var tool = integration.Tools.FirstOrDefault(candidate => string.Equals(candidate.Name, step.ToolName, StringComparison.OrdinalIgnoreCase));
        if (tool is null || !tool.Enabled)
        {
            return new StepExecutionResult
            {
                Success = false,
                Error = $"Tool '{step.ToolName}' is not available on integration '{step.IntegrationId}'."
            };
        }

        var context = BuildTemplateContext(sharedLabels, workingDirectory, string.Empty, step);
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var argumentLogs = new List<string>();

        var bindingByTarget = step.InputBindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.Target))
            .GroupBy(binding => binding.Target, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        foreach (var key in tool.InputContract.Keys.Union(step.Arguments.Keys, StringComparer.OrdinalIgnoreCase).Union(bindingByTarget.Keys, StringComparer.OrdinalIgnoreCase))
        {
            string? rawValue = null;
            if (bindingByTarget.TryGetValue(key, out var binding))
            {
                rawValue = ResolveBinding(binding, sharedLabels, context);
            }
            else if (step.Arguments.TryGetValue(key, out var literal))
            {
                rawValue = RenderTemplate(literal, context);
            }

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            payload[key] = ConvertScalar(rawValue);
            argumentLogs.Add($"{key}={rawValue}");
        }

        var toolCallText = $"{tool.Method} {tool.Route} :: {string.Join(", ", argumentLogs)}";
        var responseText = await CallToolAsync(tool, payload, cancellationToken);
        using var responseDocument = JsonDocument.Parse(responseText);
        var root = responseDocument.RootElement;
        var ok = root.TryGetProperty("ok", out var okElement) && okElement.GetBoolean();
        var error = ok
            ? string.Empty
            : root.TryGetProperty("error", out var errorElement) && errorElement.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString() ?? "Tool call failed."
                : "Tool call failed.";

        var outputLabels = ResolveToolOutputLabels(step, root);
        return new StepExecutionResult
        {
            Success = ok,
            PrimaryOutput = outputLabels.Values.FirstOrDefault() ?? ExtractToolOutput(root, "data"),
            ResponseText = responseText,
            Error = error,
            Logs = new List<string> { toolCallText, responseText },
            OutputLabels = outputLabels,
            ResolvedToolCall = toolCallText
        };
    }

    private async Task<string> CallToolAsync(ToolDefinition tool, Dictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };
        var route = tool.Route.StartsWith("/", StringComparison.Ordinal) ? tool.Route : $"/{tool.Route}";
        var url = $"http://localhost:3005{route}";

        if (string.Equals(tool.Method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            if (payload.Count > 0)
            {
                var query = string.Join("&",
                    payload.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(ConvertValueToString(pair.Value))}"));
                url = $"{url}?{query}";
            }
            return await client.GetStringAsync(url, cancellationToken);
        }

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, content, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static Dictionary<string, string> BuildInitialLabels(WorkflowDefinition workflow, IReadOnlyDictionary<string, string>? inputValues)
    {
        var labels = workflow.Inputs.ToDictionary(
            input => input.Key,
            input => input.DefaultValue ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
        labels["working_directory"] = workflow.WorkingDirectory;
        labels["workflow_name"] = workflow.Name;
        labels["workflow_id"] = workflow.Id;

        if (inputValues is not null)
        {
            foreach (var pair in inputValues)
            {
                labels[pair.Key] = pair.Value;
            }
        }

        return labels;
    }

    private Dictionary<string, string> BuildTemplateContext(
        IReadOnlyDictionary<string, string> sharedLabels,
        string workingDirectory,
        string previousPrimaryOutput,
        WorkflowStepDefinition step)
    {
        var context = sharedLabels.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        context["working_directory"] = workingDirectory;
        if (!string.IsNullOrWhiteSpace(previousPrimaryOutput) && step.AppendPreviousStepOutput)
        {
            context["previous_step_output"] = previousPrimaryOutput;
        }

        foreach (var binding in step.InputBindings)
        {
            if (string.IsNullOrWhiteSpace(binding.Target))
            {
                continue;
            }
            context[binding.Target] = ResolveBinding(binding, sharedLabels, context);
        }

        return context;
    }

    private static string ResolveBinding(
        WorkflowBinding binding,
        IReadOnlyDictionary<string, string> sharedLabels,
        IReadOnlyDictionary<string, string> context)
    {
        if (string.Equals(binding.SourceType, "label", StringComparison.OrdinalIgnoreCase))
        {
            return sharedLabels.TryGetValue(binding.SourceKey ?? string.Empty, out var labelValue)
                ? labelValue
                : context.TryGetValue(binding.SourceKey ?? string.Empty, out var contextValue)
                    ? contextValue
                    : string.Empty;
        }

        return RenderTemplate(binding.LiteralValue ?? string.Empty, context);
    }

    private static Dictionary<string, string> ResolveAgentOutputLabels(WorkflowStepDefinition step, WrapperRunState wrapperState)
    {
        var outputLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (step.OutputBindings.Count == 0)
        {
            outputLabels[$"{step.Id}.final_text"] = wrapperState.FinalText ?? string.Empty;
            return outputLabels;
        }

        foreach (var binding in step.OutputBindings)
        {
            if (string.IsNullOrWhiteSpace(binding.TargetLabel))
            {
                continue;
            }

            outputLabels[binding.TargetLabel] = ResolveAgentOutputValue(binding.Source, wrapperState);
        }

        return outputLabels;
    }

    private static string ResolveAgentOutputValue(string? source, WrapperRunState wrapperState)
    {
        var normalizedSource = (source ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedSource) || string.Equals(normalizedSource, "final_text", StringComparison.OrdinalIgnoreCase))
        {
            return wrapperState.FinalText ?? string.Empty;
        }

        if (string.Equals(normalizedSource, "primary_text", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(wrapperState.PrimaryTextOutput)
                ? wrapperState.PrimaryTextOutput
                : wrapperState.FinalText ?? string.Empty;
        }

        if (string.Equals(normalizedSource, "text_outputs", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(Environment.NewLine, wrapperState.TextOutputs);
        }

        if (string.Equals(normalizedSource, "artifacts", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(Environment.NewLine, wrapperState.ArtifactOutputs.Select(artifact => artifact.Path));
        }

        if (string.Equals(normalizedSource, "logs", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(Environment.NewLine, wrapperState.Logs);
        }

        if (string.Equals(normalizedSource, "session_id", StringComparison.OrdinalIgnoreCase))
        {
            return wrapperState.SessionId ?? string.Empty;
        }

        if (normalizedSource.StartsWith("text_output_", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(normalizedSource["text_output_".Length..], out var outputIndex) &&
            outputIndex >= 0)
        {
            return outputIndex < wrapperState.TextOutputs.Count
                ? wrapperState.TextOutputs[outputIndex] ?? string.Empty
                : string.Empty;
        }

        return wrapperState.FinalText ?? string.Empty;
    }

    private static Dictionary<string, string> ResolveToolOutputLabels(WorkflowStepDefinition step, JsonElement responseRoot)
    {
        var outputLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (step.OutputBindings.Count == 0)
        {
            outputLabels[$"{step.Id}.response"] = ExtractToolOutput(responseRoot, "data");
            return outputLabels;
        }

        foreach (var binding in step.OutputBindings)
        {
            if (string.IsNullOrWhiteSpace(binding.TargetLabel))
            {
                continue;
            }

            outputLabels[binding.TargetLabel] = ExtractToolOutput(responseRoot, string.IsNullOrWhiteSpace(binding.Source) ? "data" : binding.Source);
        }

        return outputLabels;
    }

    private static string ExtractToolOutput(JsonElement root, string source)
    {
        if (string.IsNullOrWhiteSpace(source) || string.Equals(source, "data", StringComparison.OrdinalIgnoreCase))
        {
            return root.TryGetProperty("data", out var dataElement) ? JsonElementToString(dataElement) : string.Empty;
        }

        if (source == "$")
        {
            return JsonElementToString(root);
        }

        var segments = source.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        foreach (var segment in segments)
        {
            if (segment == "$")
            {
                continue;
            }

            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return string.Empty;
            }
        }

        return JsonElementToString(current);
    }

    private static string JsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => string.Empty,
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Number => element.GetRawText(),
            _ => element.GetRawText()
        };
    }

    private static string ResolveWorkingDirectory(string workflowWorkingDirectory, string? stepOverride)
    {
        if (!string.IsNullOrWhiteSpace(stepOverride))
        {
            return ResolvePathValue(stepOverride, workflowWorkingDirectory);
        }

        return ResolvePathValue(workflowWorkingDirectory, Directory.GetCurrentDirectory());
    }

    private static string ResolvePathValue(string? value, string baseDirectory)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return Path.GetFullPath(baseDirectory);
        }

        return Path.IsPathRooted(trimmed)
            ? Path.GetFullPath(trimmed)
            : Path.GetFullPath(Path.Combine(baseDirectory, trimmed));
    }

    private static object? ConvertScalar(string rawValue)
    {
        var trimmed = rawValue.Trim();
        if (bool.TryParse(trimmed, out var boolValue))
        {
            return boolValue;
        }
        if (int.TryParse(trimmed, out var intValue))
        {
            return intValue;
        }
        if (double.TryParse(trimmed, out var doubleValue))
        {
            return doubleValue;
        }
        return trimmed;
    }

    private static string ConvertValueToString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string str => str,
            JsonElement element => JsonElementToString(element),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string RenderTemplate(string template, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return Regex.Replace(template, @"\{\{([^{}]+)\}\}", match =>
        {
            var key = match.Groups[1].Value.Trim();
            return values.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
        });
    }

    private WorkflowStepRunState? GetMutableStep(string runId, string stepId)
    {
        lock (_lock)
        {
            if (!_runs.TryGetValue(runId, out var run))
            {
                return null;
            }

            return run.Steps.FirstOrDefault(step => string.Equals(step.StepId, stepId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void UpdateRun(string runId, Action<WorkflowRunState> update)
    {
        lock (_lock)
        {
            if (_runs.TryGetValue(runId, out var run))
            {
                update(run);
            }
        }

        EmitRun(runId);
    }

    private void EmitRun(string runId)
    {
        WorkflowRunState? snapshot = null;
        lock (_lock)
        {
            if (_runs.TryGetValue(runId, out var run))
            {
                snapshot = run.Clone();
            }
        }

        if (snapshot is not null)
        {
            RunUpdated?.Invoke(snapshot);
        }
    }

    private void Cleanup(string runId)
    {
        lock (_lock)
        {
            _cancellations.Remove(runId);
            _completionTasks.Remove(runId);
        }
    }

    private sealed class StepExecutionResult
    {
        public bool Success { get; set; }
        public string PrimaryOutput { get; set; } = string.Empty;
        public string FinalText { get; set; } = string.Empty;
        public string ResponseText { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string ChildRunId { get; set; } = string.Empty;
        public string ResolvedPrompt { get; set; } = string.Empty;
        public string ResolvedToolCall { get; set; } = string.Empty;
        public IReadOnlyList<string> Logs { get; set; } = Array.Empty<string>();
        public IReadOnlyList<WrapperArtifact> Artifacts { get; set; } = Array.Empty<WrapperArtifact>();
        public Dictionary<string, string> OutputLabels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
