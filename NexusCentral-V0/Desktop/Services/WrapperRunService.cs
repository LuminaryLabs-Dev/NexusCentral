using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class WrapperRunService
{
    private readonly AppPathsService _paths;
    private readonly TemplateCatalogService _templates;
    private readonly WrapperCatalogService _wrappers;
    private readonly ModelLibraryService _library;
    private readonly RuntimeService _runtime;
    private readonly LogService _logs;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private readonly object _lock = new();
    private readonly Dictionary<string, WrapperRunState> _runs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Process> _runningProcesses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<WrapperRunState>> _completionTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _cancelledRuns = new(StringComparer.OrdinalIgnoreCase);

    public event Action<WrapperRunState>? RunUpdated;

    public WrapperRunService(
        AppPathsService paths,
        TemplateCatalogService templates,
        WrapperCatalogService wrappers,
        ModelLibraryService library,
        RuntimeService runtime,
        LogService logs)
    {
        _paths = paths;
        _templates = templates;
        _wrappers = wrappers;
        _library = library;
        _runtime = runtime;
        _logs = logs;
    }

    public WrapperRunHandle StartRun(WrapperRunRequest request, IProgress<WrapperRunStreamEvent>? progress = null)
    {
        var template = _templates.FindById(request.TemplateId);
        if (template is null)
        {
            var failedState = CreateFailedRun("TEMPLATE_NOT_FOUND", request);
            return new WrapperRunHandle(failedState.RunId, Task.FromResult(failedState));
        }

        var wrapperId = string.IsNullOrWhiteSpace(request.WrapperId) ? template.DefaultWrapperId : request.WrapperId;
        var wrapper = _wrappers.FindById(wrapperId);
        if (wrapper is null)
        {
            var failedState = CreateFailedRun("WRAPPER_NOT_FOUND", request, template);
            return new WrapperRunHandle(failedState.RunId, Task.FromResult(failedState));
        }

        var runId = string.IsNullOrWhiteSpace(request.Metadata.GetValueOrDefault("run_id"))
            ? $"wraprun-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..44]
            : request.Metadata["run_id"];
        var runDirectory = Path.Combine(_paths.WrapperRunsDirectory, runId);
        var outputDirectory = Path.Combine(runDirectory, "output");
        Directory.CreateDirectory(runDirectory);
        Directory.CreateDirectory(outputDirectory);
        var sessionId = ResolveSessionId(request, runId);
        var rootDirectory = ResolveRootDirectory(request);
        var textInputs = BuildTextInputs(wrapper, request);

        var state = new WrapperRunState
        {
            RunId = runId,
            TemplateId = template.Id,
            TemplateName = template.Name,
            WrapperId = wrapper.Id,
            WrapperName = wrapper.Name,
            IoContractId = wrapper.IoContractId,
            Status = "queued",
            StatusMessage = "Queued",
            Prompt = request.Prompt,
            RootDirectory = rootDirectory,
            SessionId = sessionId,
            TextInputs = textInputs,
            ContractStatus = "queued",
            OutputDir = outputDirectory,
            ArtifactInputs = request.ArtifactInputs.ToList(),
            ModelId = request.ModelId,
            MaxTokens = request.MaxTokens,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        WriteState(runDirectory, state);
        StoreState(state);

        var completion = string.Equals(wrapper.Transport, "builtin", StringComparison.OrdinalIgnoreCase)
            ? RunBuiltinAsync(runDirectory, template, wrapper, state, request, progress)
            : RunCliAsync(runDirectory, template, wrapper, state, request, progress);

        lock (_lock)
        {
            _completionTasks[runId] = completion;
        }

        return new WrapperRunHandle(runId, completion);
    }

    public WrapperRunState? GetRun(string? runId)
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

    public IReadOnlyList<WrapperRunState> GetRecentRuns(int limit = 20)
    {
        lock (_lock)
        {
            return _runs.Values
                .OrderByDescending(state => state.CreatedAtUtc)
                .Take(Math.Max(1, limit))
                .Select(state => state.Clone())
                .ToList();
        }
    }

    public IReadOnlyList<WrapperArtifact> GetArtifacts(string? runId) =>
        GetRun(runId)?.ArtifactOutputs ?? new List<WrapperArtifact>();

    public bool CancelRun(string? runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return false;
        }

        Process? process;
        lock (_lock)
        {
            _cancelledRuns.Add(runId);
            _runningProcesses.TryGetValue(runId, out process);
        }

        if (process is null)
        {
            var state = GetRun(runId);
            if (state is not null && !string.Equals(state.Status, "done", StringComparison.OrdinalIgnoreCase) && !string.Equals(state.Status, "error", StringComparison.OrdinalIgnoreCase))
            {
                state.Status = "stopped";
                state.StatusMessage = "Stopped";
                state.CompletedAtUtc = DateTimeOffset.UtcNow;
                StoreState(state);
            }
            return true;
        }

        try
        {
            if (process.HasExited)
            {
                return true;
            }

            process.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<WrapperRunState> RunBuiltinAsync(
        string runDirectory,
        TemplateDefinition template,
        WrapperDefinition wrapper,
        WrapperRunState state,
        WrapperRunRequest request,
        IProgress<WrapperRunStreamEvent>? progress)
    {
        UpdateState(state.RunId, state =>
        {
            state.Status = "running";
            state.StatusMessage = "Running direct LLM";
            state.StartedAtUtc = DateTimeOffset.UtcNow;
        }, runDirectory, progress, new WrapperRunStreamEvent
        {
            Type = "status",
            Status = "Running direct LLM",
            Category = "model",
            Section = "model",
            Level = "info"
        });

        var effectiveModelId = !string.IsNullOrWhiteSpace(request.ModelId)
            ? request.ModelId
            : _library.ResolveDirectChatAutoInstance()?.ModelId
              ?? _library.ResolveInstanceForEndpoint("instruct")?.ModelId
              ?? _library.ResolveInstanceForEndpoint("thinking")?.ModelId
              ?? string.Empty;
        var result = await _runtime.RunInstructAsync(request.Prompt, effectiveModelId, request.MaxTokens, null);
        if (!result.Ok)
        {
            UpdateState(state.RunId, existing =>
            {
                existing.Status = "error";
                existing.StatusMessage = "Runtime error";
                existing.Error = result.Error;
                existing.ModelId = result.ModelId;
                existing.ContractStatus = "error";
                existing.CompletedAtUtc = DateTimeOffset.UtcNow;
                existing.ExitCode = 1;
            }, runDirectory, progress, new WrapperRunStreamEvent
            {
                Type = "error",
                Text = result.Error,
                Category = "model",
                Section = "model",
                Level = "error"
            });
        }
        else
        {
            UpdateState(state.RunId, existing =>
            {
                existing.Status = "done";
                existing.StatusMessage = "Completed";
                existing.FinalText = result.Output;
                existing.PrimaryTextOutput = result.Output;
                existing.TextOutputs = string.IsNullOrWhiteSpace(result.Output) ? new List<string>() : new List<string> { result.Output };
                existing.ModelId = result.ModelId;
                existing.ContractStatus = "completed";
                existing.CompletedAtUtc = DateTimeOffset.UtcNow;
                existing.ExitCode = 0;
            }, runDirectory, progress, new WrapperRunStreamEvent
            {
                Type = "final",
                Text = result.Output,
                Category = "final_marker",
                Section = "model",
                Level = "success"
            });
        }

        var finalState = FinalizeArtifacts(state.RunId, template, runDirectory);
        CleanupRun(state.RunId);
        return finalState;
    }

    private Task<WrapperRunState> RunCliAsync(
        string runDirectory,
        TemplateDefinition template,
        WrapperDefinition wrapper,
        WrapperRunState state,
        WrapperRunRequest request,
        IProgress<WrapperRunStreamEvent>? progress)
    {
        var resolvedCommand = _wrappers.ResolveCommand(wrapper);
        if (string.IsNullOrWhiteSpace(resolvedCommand))
        {
            UpdateState(state.RunId, existing =>
            {
                existing.Status = "error";
                existing.StatusMessage = "Wrapper unavailable";
                existing.Error = "Could not resolve wrapper command.";
                existing.ContractStatus = "error";
                existing.CompletedAtUtc = DateTimeOffset.UtcNow;
                existing.ExitCode = 1;
            }, runDirectory, progress, new WrapperRunStreamEvent { Type = "error", Text = "Could not resolve wrapper command." });
            return Task.FromResult(FinalizeArtifacts(state.RunId, template, runDirectory));
        }

        var spec = new WrapperRunSpec
        {
            RunId = state.RunId,
            TemplateId = template.Id,
            WrapperId = wrapper.Id,
            IoContractId = wrapper.IoContractId,
            TextInputRoles = wrapper.TextInputRoles.ToList(),
            TextOutputRoles = wrapper.TextOutputRoles.ToList(),
            TextInputs = state.TextInputs.ToList(),
            TextOutputs = state.TextOutputs.ToList(),
            PrimaryTextOutput = state.PrimaryTextOutput,
            ContractStatus = state.ContractStatus,
            ArtifactInputs = request.ArtifactInputs.ToList(),
            OutputDir = state.OutputDir,
            ModelId = request.ModelId,
            MaxTokens = request.MaxTokens,
            NexuscentralBaseUrl = request.NexuscentralBaseUrl,
            LlmToolRoute = string.IsNullOrWhiteSpace(request.LlmToolRoute) ? "/nexuscentral/thinking" : request.LlmToolRoute,
            ArgTemplates = wrapper.ArgTemplates.ToList(),
            SourceBundlePath = wrapper.SourceBundlePath,
            Metadata = request.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };

        var specPath = Path.Combine(runDirectory, "run-spec.json");
        File.WriteAllText(specPath, JsonSerializer.Serialize(spec, _jsonOptions));

        ProcessStartInfo psi;
        string? externalOutputPath = null;
        if (IsExternalBatchedContract(wrapper))
        {
            externalOutputPath = Path.Combine(runDirectory, "agent-output.json");
            psi = BuildExternalContractProcessStartInfo(runDirectory, wrapper, state, spec, resolvedCommand, externalOutputPath);
        }
        else
        {
            psi = new ProcessStartInfo(resolvedCommand)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = runDirectory,
            };
            foreach (var argument in _wrappers.ResolveArgs(wrapper))
            {
                psi.ArgumentList.Add(argument);
            }
            psi.ArgumentList.Add("--run-spec");
            psi.ArgumentList.Add(specPath);
            foreach (var environmentVariable in _wrappers.ResolveEnvironment(wrapper))
            {
                psi.Environment[environmentVariable.Key] = environmentVariable.Value;
            }
        }

        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (Exception ex)
        {
            UpdateState(state.RunId, existing =>
            {
                existing.Status = "error";
                existing.StatusMessage = "Wrapper failed to start";
                existing.Error = ex.Message;
                existing.ContractStatus = "error";
                existing.CompletedAtUtc = DateTimeOffset.UtcNow;
                existing.ExitCode = 1;
            }, runDirectory, progress, new WrapperRunStreamEvent { Type = "error", Text = ex.Message });
            return Task.FromResult(FinalizeArtifacts(state.RunId, template, runDirectory));
        }

        if (process is null)
        {
            UpdateState(state.RunId, existing =>
            {
                existing.Status = "error";
                existing.StatusMessage = "Wrapper failed to start";
                existing.Error = "Process start returned null.";
                existing.ContractStatus = "error";
                existing.CompletedAtUtc = DateTimeOffset.UtcNow;
                existing.ExitCode = 1;
            }, runDirectory, progress, new WrapperRunStreamEvent { Type = "error", Text = "Process start returned null." });
            return Task.FromResult(FinalizeArtifacts(state.RunId, template, runDirectory));
        }

        lock (_lock)
        {
            _runningProcesses[state.RunId] = process;
        }

        UpdateState(state.RunId, existing =>
        {
            existing.Status = "running";
            existing.StatusMessage = "Running wrapper";
            existing.StartedAtUtc = DateTimeOffset.UtcNow;
            existing.ContractStatus = "running";
        }, runDirectory, progress, new WrapperRunStreamEvent { Type = "status", Status = "Running wrapper" });

        return IsExternalBatchedContract(wrapper)
            ? MonitorExternalContractRunAsync(state.RunId, runDirectory, template, wrapper, process, externalOutputPath ?? string.Empty, progress)
            : MonitorCliRunAsync(state.RunId, runDirectory, template, process, progress);
    }

    private ProcessStartInfo BuildExternalContractProcessStartInfo(
        string runDirectory,
        WrapperDefinition wrapper,
        WrapperRunState state,
        WrapperRunSpec spec,
        string resolvedCommand,
        string externalOutputPath)
    {
        var inputPath = Path.Combine(runDirectory, "agent-input.json");
        var payload = new ExternalBatchedAgentInput
        {
            RunId = state.RunId,
            SessionId = state.SessionId,
            WrapperId = state.WrapperId,
            WrapperName = state.WrapperName,
            IoContractId = wrapper.IoContractId,
            Prompt = state.Prompt,
            RootDirectory = state.RootDirectory,
            TextInputRoles = wrapper.TextInputRoles.ToList(),
            TextInputs = state.TextInputs.ToList(),
            ArtifactInputs = state.ArtifactInputs.ToList(),
            ModelId = state.ModelId,
            MaxTokens = state.MaxTokens,
            OutputDir = state.OutputDir,
            OutputJson = externalOutputPath,
            NexuscentralBaseUrl = spec.NexuscentralBaseUrl,
            LlmToolRoute = spec.LlmToolRoute,
            Metadata = spec.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };
        File.WriteAllText(inputPath, JsonSerializer.Serialize(payload, _jsonOptions));

        var psi = new ProcessStartInfo(resolvedCommand)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrWhiteSpace(wrapper.SourceBundlePath) ? runDirectory : wrapper.SourceBundlePath,
        };

        foreach (var argument in ExpandArgTemplates(wrapper, inputPath, externalOutputPath, state))
        {
            psi.ArgumentList.Add(argument);
        }

        foreach (var environmentVariable in _wrappers.ResolveEnvironment(wrapper))
        {
            psi.Environment[environmentVariable.Key] = environmentVariable.Value;
        }

        psi.Environment["NEXUSCENTRAL_RUN_ID"] = state.RunId;
        psi.Environment["NEXUSCENTRAL_SESSION_ID"] = state.SessionId;
        psi.Environment["NEXUSCENTRAL_ROOT_DIRECTORY"] = state.RootDirectory;
        psi.Environment["NEXUSCENTRAL_OUTPUT_DIR"] = state.OutputDir;
        psi.Environment["NEXUSCENTRAL_OUTPUT_JSON"] = externalOutputPath;
        return psi;
    }

    private IReadOnlyList<string> ExpandArgTemplates(WrapperDefinition wrapper, string inputJsonPath, string outputJsonPath, WrapperRunState state)
    {
        var templates = wrapper.ArgTemplates.Count > 0
            ? wrapper.ArgTemplates
            : new List<string> { "--input-json", "{input_json}", "--output-json", "{output_json}" };

        return templates
            .Select(template => template
                .Replace("{input_json}", inputJsonPath, StringComparison.OrdinalIgnoreCase)
                .Replace("{output_json}", outputJsonPath, StringComparison.OrdinalIgnoreCase)
                .Replace("{model_id}", state.ModelId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{max_tokens}", state.MaxTokens.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{session_id}", state.SessionId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{root_directory}", state.RootDirectory ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{bundle_dir}", wrapper.SourceBundlePath ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{output_dir}", state.OutputDir ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool IsExternalBatchedContract(WrapperDefinition wrapper) =>
        string.Equals(wrapper.IoContractId, "external_batched_agent_v1", StringComparison.OrdinalIgnoreCase);

    private static string ResolveSessionId(WrapperRunRequest request, string runId)
    {
        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            return request.SessionId;
        }

        if (request.Metadata.TryGetValue("session_id", out var sessionId) && !string.IsNullOrWhiteSpace(sessionId))
        {
            return sessionId;
        }

        return runId;
    }

    private static List<string> BuildTextInputs(WrapperDefinition wrapper, WrapperRunRequest request)
    {
        if (wrapper.TextInputRoles.Count == 0)
        {
            return new List<string> { request.Prompt };
        }

        return wrapper.TextInputRoles
            .Select(role => ResolveTextInputValue(role, request))
            .ToList();
    }

    private static string ResolveTextInputValue(string role, WrapperRunRequest request)
    {
        var normalizedRole = (role ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedRole switch
        {
            "prompt" => request.Prompt,
            "current_prompt" => request.Prompt,
            "root_directory" => request.RootDirectory,
            "workspace_root" => request.RootDirectory,
            "working_directory" => request.RootDirectory,
            "goal_context" => request.Metadata.TryGetValue("goal_context", out var goalContext) && !string.IsNullOrWhiteSpace(goalContext)
                ? goalContext
                : request.Prompt,
            _ => request.Metadata.TryGetValue(role ?? string.Empty, out var value)
                ? value
                : string.Empty
        };
    }

    private string ResolveRootDirectory(WrapperRunRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.RootDirectory))
        {
            return Path.GetFullPath(request.RootDirectory);
        }

        if (request.Metadata.TryGetValue("root_directory", out var rootDirectory) && !string.IsNullOrWhiteSpace(rootDirectory))
        {
            return Path.GetFullPath(rootDirectory);
        }

        if (request.Metadata.TryGetValue("workspace_root", out var workspaceRoot) && !string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return Path.GetFullPath(workspaceRoot);
        }

        if (request.Metadata.TryGetValue("working_directory", out var workingDirectory) && !string.IsNullOrWhiteSpace(workingDirectory))
        {
            return Path.GetFullPath(workingDirectory);
        }

        return _paths.ResolvedRootDirectory;
    }

    private async Task<WrapperRunState> MonitorCliRunAsync(
        string runId,
        string runDirectory,
        TemplateDefinition template,
        Process process,
        IProgress<WrapperRunStreamEvent>? progress)
    {
        var rawLogPath = Path.Combine(runDirectory, "stdout.ndjson");
        await using var rawLogWriter = new StreamWriter(rawLogPath, append: true);

        async Task ReadStreamAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                await rawLogWriter.WriteLineAsync(line);
                HandleProcessLine(runId, runDirectory, line, progress);
            }
        }

        var stdoutTask = ReadStreamAsync(process.StandardOutput);
        var stderrTask = ReadStreamAsync(process.StandardError);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync();

        var cancelled = false;
        lock (_lock)
        {
            cancelled = _cancelledRuns.Contains(runId);
        }

        UpdateState(runId, existing =>
        {
            existing.CompletedAtUtc = DateTimeOffset.UtcNow;
            existing.ExitCode = process.ExitCode;
            if (cancelled)
            {
                existing.Status = "stopped";
                existing.StatusMessage = "Stopped";
                existing.ContractStatus = "stopped";
            }
            else if (process.ExitCode == 0 && string.IsNullOrWhiteSpace(existing.Error))
            {
                existing.Status = "done";
                existing.StatusMessage = "Completed";
                existing.ContractStatus = "completed";
            }
            else if (string.IsNullOrWhiteSpace(existing.Status) || string.Equals(existing.Status, "running", StringComparison.OrdinalIgnoreCase))
            {
                existing.Status = "error";
                existing.StatusMessage = "Wrapper failed";
                existing.ContractStatus = "error";
                if (string.IsNullOrWhiteSpace(existing.Error))
                {
                    existing.Error = $"Process exited with code {process.ExitCode}.";
                }
            }
        }, runDirectory, progress, cancelled
            ? new WrapperRunStreamEvent { Type = "status", Status = "Stopped" }
            : process.ExitCode == 0
                ? new WrapperRunStreamEvent { Type = "status", Status = "Completed" }
                : new WrapperRunStreamEvent { Type = "error", Text = $"Process exited with code {process.ExitCode}." });

        var finalState = FinalizeArtifacts(runId, template, runDirectory);
        CleanupRun(runId);
        return finalState;
    }

    private async Task<WrapperRunState> MonitorExternalContractRunAsync(
        string runId,
        string runDirectory,
        TemplateDefinition template,
        WrapperDefinition wrapper,
        Process process,
        string outputJsonPath,
        IProgress<WrapperRunStreamEvent>? progress)
    {
        var rawLogPath = Path.Combine(runDirectory, "stdout.ndjson");
        await using var rawLogWriter = new StreamWriter(rawLogPath, append: true);

        async Task ReadStreamAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                await rawLogWriter.WriteLineAsync(line);
                HandleProcessLine(runId, runDirectory, line, progress);
            }
        }

        var stdoutTask = ReadStreamAsync(process.StandardOutput);
        var stderrTask = ReadStreamAsync(process.StandardError);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync();

        var cancelled = false;
        lock (_lock)
        {
            cancelled = _cancelledRuns.Contains(runId);
        }

        ExternalContractResult? externalOutput = null;
        if (!cancelled && process.ExitCode == 0)
        {
            externalOutput = TryReadExternalOutput(outputJsonPath, runDirectory, wrapper);
            if (externalOutput is not null)
            {
                foreach (var log in externalOutput.Logs.Where(log => !string.IsNullOrWhiteSpace(log)))
                {
                    foreach (var logLine in log
                                 .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        HandleProcessLine(runId, runDirectory, logLine, progress);
                    }
                }

                UpdateState(runId, state =>
                {
                    state.TextOutputs = externalOutput.TextOutputs.Where(text => !string.IsNullOrWhiteSpace(text)).ToList();
                    state.PrimaryTextOutput = !string.IsNullOrWhiteSpace(externalOutput.PrimaryTextOutput)
                        ? externalOutput.PrimaryTextOutput
                        : state.TextOutputs.FirstOrDefault() ?? string.Empty;
                    state.FinalText = state.PrimaryTextOutput;
                    state.ContractStatus = string.IsNullOrWhiteSpace(externalOutput.ContractStatus) ? "completed" : externalOutput.ContractStatus;
                    if (!string.IsNullOrWhiteSpace(externalOutput.StatusMessage))
                    {
                        state.StatusMessage = externalOutput.StatusMessage;
                    }

                    foreach (var artifact in externalOutput.ArtifactOutputs)
                    {
                        if (!state.ArtifactOutputs.Any(existing => string.Equals(existing.Path, artifact.Path, StringComparison.OrdinalIgnoreCase)))
                        {
                            state.ArtifactOutputs.Add(artifact);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(externalOutput.Error))
                    {
                        state.Error = externalOutput.Error;
                    }
                }, runDirectory, progress, null);

                foreach (var artifact in externalOutput.ArtifactOutputs)
                {
                    progress?.Report(new WrapperRunStreamEvent
                    {
                        Type = "artifact",
                        Artifact = artifact,
                        Category = "artifact",
                        Section = "artifacts",
                        Level = "info"
                    });
                }

                if (!string.IsNullOrWhiteSpace(externalOutput.PrimaryTextOutput))
                {
                    progress?.Report(new WrapperRunStreamEvent
                    {
                        Type = "final",
                        Text = externalOutput.PrimaryTextOutput,
                        Category = "final_marker",
                        Section = "agent",
                        Level = "success"
                    });
                }
            }
        }

        UpdateState(runId, existing =>
        {
            existing.CompletedAtUtc = DateTimeOffset.UtcNow;
            existing.ExitCode = process.ExitCode;
            if (cancelled)
            {
                existing.Status = "stopped";
                existing.StatusMessage = "Stopped";
                existing.ContractStatus = "stopped";
            }
            else if (process.ExitCode == 0 && string.IsNullOrWhiteSpace(existing.Error) && !string.IsNullOrWhiteSpace(existing.FinalText))
            {
                existing.Status = "done";
                if (string.IsNullOrWhiteSpace(existing.StatusMessage) || string.Equals(existing.StatusMessage, "Running wrapper", StringComparison.OrdinalIgnoreCase))
                {
                    existing.StatusMessage = "Completed";
                }
                existing.ContractStatus = string.IsNullOrWhiteSpace(existing.ContractStatus) ? "completed" : existing.ContractStatus;
            }
            else
            {
                existing.Status = "error";
                existing.StatusMessage = "Wrapper failed";
                existing.ContractStatus = string.IsNullOrWhiteSpace(existing.ContractStatus) ? "error" : existing.ContractStatus;
                if (string.IsNullOrWhiteSpace(existing.Error))
                {
                    existing.Error = process.ExitCode == 0
                        ? $"External wrapper output '{outputJsonPath}' did not contain a primary text output."
                        : $"Process exited with code {process.ExitCode}.";
                }
            }
        }, runDirectory, progress, cancelled
            ? new WrapperRunStreamEvent { Type = "status", Status = "Stopped", Category = "system", Section = "system", Level = "warning" }
            : process.ExitCode == 0 && externalOutput is not null && string.IsNullOrWhiteSpace(externalOutput.Error)
                ? new WrapperRunStreamEvent { Type = "status", Status = "Completed", Category = "system", Section = "system", Level = "success" }
                : new WrapperRunStreamEvent
                {
                    Type = "error",
                    Text = !string.IsNullOrWhiteSpace(externalOutput?.Error)
                        ? externalOutput!.Error!
                        : $"Process exited with code {process.ExitCode}.",
                    Category = "system",
                    Section = "system",
                    Level = "error"
                });

        var finalState = FinalizeArtifacts(runId, template, runDirectory);
        CleanupRun(runId);
        return finalState;
    }

    private void HandleProcessLine(string runId, string runDirectory, string line, IProgress<WrapperRunStreamEvent>? progress)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeElement))
            {
                AppendLog(runId, runDirectory, line, progress);
                return;
            }

            var type = typeElement.GetString() ?? "log";
            var text = TryGetString(root, "text");
            var section = TryGetString(root, "section");
            var category = TryGetString(root, "category");
            var level = TryGetString(root, "level");
            var baseEvent = new WrapperRunStreamEvent
            {
                Section = section,
                Category = category,
                Level = string.IsNullOrWhiteSpace(level) ? "info" : level,
                EventId = TryGetString(root, "event_id"),
                ChainId = TryGetString(root, "chain_id"),
                ParentChainId = TryGetString(root, "parent_chain_id"),
                StepId = TryGetString(root, "step_id"),
                Phase = TryGetString(root, "phase"),
                Depth = TryGetInt32(root, "depth") ?? 0,
                Title = TryGetString(root, "title"),
                IsChainStart = TryGetBool(root, "is_chain_start"),
                IsChainEnd = TryGetBool(root, "is_chain_end"),
                IsTokenStream = TryGetBool(root, "is_token_stream"),
                ArtifactPath = TryGetString(root, "artifact_path"),
                ValidationStatus = TryGetString(root, "validation_status"),
                ApprovalId = TryGetString(root, "approval_id"),
                ToolName = TryGetString(root, "tool_name"),
                MemoryId = TryGetString(root, "memory_id")
            };
            switch (type)
            {
                case "log":
                    AppendLog(runId, runDirectory, string.IsNullOrWhiteSpace(text) ? line : text, progress, section, category, level, baseEvent);
                    break;
                case "status":
                    var status = text;
                    UpdateState(runId, state =>
                    {
                        state.StatusMessage = string.IsNullOrWhiteSpace(status) ? state.StatusMessage : status;
                    }, runDirectory, progress, CopyEvent(baseEvent, new WrapperRunStreamEvent
                    {
                        Type = "status",
                        Status = status,
                        Text = status,
                        Section = section,
                        Category = category,
                        Level = string.IsNullOrWhiteSpace(level) ? "info" : level
                    }));
                    break;
                case "artifact":
                    var artifact = new WrapperArtifact
                    {
                        Label = TryGetString(root, "label"),
                        Path = TryGetString(root, "path"),
                        Kind = string.IsNullOrWhiteSpace(TryGetString(root, "kind")) ? "file" : TryGetString(root, "kind")
                    };
                    UpdateState(runId, state =>
                    {
                        if (!state.ArtifactOutputs.Any(existing => string.Equals(existing.Path, artifact.Path, StringComparison.OrdinalIgnoreCase)))
                        {
                            state.ArtifactOutputs.Add(artifact);
                        }
                    }, runDirectory, progress, CopyEvent(baseEvent, new WrapperRunStreamEvent
                    {
                        Type = "artifact",
                        Artifact = artifact,
                        Section = string.IsNullOrWhiteSpace(section) ? "artifacts" : section,
                        Category = string.IsNullOrWhiteSpace(category) ? "artifact" : category,
                        Level = string.IsNullOrWhiteSpace(level) ? "info" : level
                    }));
                    break;
                case "final":
                    var finalText = text;
                    UpdateState(runId, state =>
                    {
                        state.FinalText = finalText;
                        state.PrimaryTextOutput = finalText;
                        state.ContractStatus = "completed";
                        state.TextOutputs = string.IsNullOrWhiteSpace(finalText) ? new List<string>() : new List<string> { finalText };
                    }, runDirectory, progress, CopyEvent(baseEvent, new WrapperRunStreamEvent
                    {
                        Type = "final",
                        Text = finalText,
                        Section = section,
                        Category = string.IsNullOrWhiteSpace(category) ? "final_marker" : category,
                        Level = string.IsNullOrWhiteSpace(level) ? "success" : level
                    }));
                    break;
                case "text_output":
                    var outputText = text;
                    var outputIndex = TryGetInt32(root, "index");
                    UpdateState(runId, state =>
                    {
                        if (outputIndex is >= 0)
                        {
                            while (state.TextOutputs.Count <= outputIndex.Value)
                            {
                                state.TextOutputs.Add(string.Empty);
                            }
                            state.TextOutputs[outputIndex.Value] = outputText;
                        }
                        else if (!string.IsNullOrWhiteSpace(outputText))
                        {
                            state.TextOutputs.Add(outputText);
                        }

                        if (state.TextOutputs.Count > 0)
                        {
                            state.PrimaryTextOutput = state.TextOutputs[0];
                            state.FinalText = state.PrimaryTextOutput;
                        }
                    }, runDirectory, progress, CopyEvent(baseEvent, new WrapperRunStreamEvent
                    {
                        Type = "text_output",
                        Text = outputText,
                        Section = section,
                        Category = category,
                        Level = string.IsNullOrWhiteSpace(level) ? "info" : level,
                        OutputIndex = outputIndex
                    }));
                    break;
                case "text_outputs":
                    var outputs = root.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array
                        ? itemsElement.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToList()
                        : new List<string>();
                    UpdateState(runId, state =>
                    {
                        state.TextOutputs = outputs;
                        state.PrimaryTextOutput = outputs.FirstOrDefault() ?? string.Empty;
                        state.FinalText = state.PrimaryTextOutput;
                    }, runDirectory, progress, CopyEvent(baseEvent, new WrapperRunStreamEvent
                    {
                        Type = "text_outputs",
                        Text = outputs.FirstOrDefault() ?? string.Empty,
                        Section = section,
                        Category = category,
                        Level = string.IsNullOrWhiteSpace(level) ? "info" : level
                    }));
                    break;
                case "contract_status":
                    var contractStatus = text;
                    UpdateState(runId, state =>
                    {
                        state.ContractStatus = contractStatus;
                    }, runDirectory, progress, CopyEvent(baseEvent, new WrapperRunStreamEvent
                    {
                        Type = "contract_status",
                        Text = contractStatus,
                        Section = section,
                        Category = category,
                        Level = string.IsNullOrWhiteSpace(level) ? "info" : level
                    }));
                    break;
                case "error":
                    var errorText = string.IsNullOrWhiteSpace(text) ? line : text;
                    UpdateState(runId, state =>
                    {
                        state.Error = errorText;
                        state.ContractStatus = "error";
                    }, runDirectory, progress, CopyEvent(baseEvent, new WrapperRunStreamEvent
                    {
                        Type = "error",
                        Text = errorText,
                        Section = section,
                        Category = category,
                        Level = string.IsNullOrWhiteSpace(level) ? "error" : level
                    }));
                    break;
                default:
                    AppendLog(runId, runDirectory, string.IsNullOrWhiteSpace(text) ? line : text, progress, section, category, level, baseEvent);
                    break;
            }
        }
        catch (JsonException)
        {
            AppendLog(runId, runDirectory, line, progress);
        }
    }

    private ExternalContractResult? TryReadExternalOutput(string outputJsonPath, string runDirectory, WrapperDefinition wrapper)
    {
        if (string.IsNullOrWhiteSpace(outputJsonPath) || !File.Exists(outputJsonPath))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<ExternalBatchedAgentRawOutput>(File.ReadAllText(outputJsonPath), _jsonOptions) ?? new ExternalBatchedAgentRawOutput();
            payload.TextOutputs ??= new List<string>();
            payload.Logs ??= new List<string>();
            payload.ArtifactOutputs ??= new List<ExternalWrappedArtifact>();
            payload.PrimaryTextOutput = string.IsNullOrWhiteSpace(payload.PrimaryTextOutput)
                ? payload.TextOutputs.FirstOrDefault() ?? string.Empty
                : payload.PrimaryTextOutput;

            return new ExternalContractResult
            {
                PrimaryTextOutput = payload.PrimaryTextOutput,
                TextOutputs = payload.TextOutputs.Where(text => text is not null).Select(text => text ?? string.Empty).ToList(),
                Logs = payload.Logs.Where(log => !string.IsNullOrWhiteSpace(log)).ToList(),
                Error = payload.Error ?? string.Empty,
                StatusMessage = payload.StatusMessage ?? string.Empty,
                ContractStatus = payload.ContractStatus ?? string.Empty,
                ArtifactOutputs = payload.ArtifactOutputs
                    .Where(artifact => artifact is not null && !string.IsNullOrWhiteSpace(artifact.Path))
                    .Select(artifact => new WrapperArtifact
                    {
                        Label = string.IsNullOrWhiteSpace(artifact.Label)
                            ? Path.GetFileName(ResolveExternalArtifactPath(artifact.Path ?? string.Empty, runDirectory, wrapper))
                            : artifact.Label ?? string.Empty,
                        Path = ResolveExternalArtifactPath(artifact.Path ?? string.Empty, runDirectory, wrapper),
                        Kind = string.IsNullOrWhiteSpace(artifact.Kind) ? "file" : artifact.Kind
                    })
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            _logs.Append("warning", "wrappers", $"Failed to read external wrapper output '{outputJsonPath}': {ex.Message}");
            return null;
        }
    }

    private static string ResolveExternalArtifactPath(string artifactPath, string runDirectory, WrapperDefinition wrapper)
    {
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(artifactPath))
        {
            return Path.GetFullPath(artifactPath);
        }

        var outputDirectory = Path.Combine(runDirectory, "output");
        var outputCandidate = Path.Combine(outputDirectory, artifactPath);
        if (File.Exists(outputCandidate) || Directory.Exists(Path.GetDirectoryName(outputCandidate) ?? string.Empty))
        {
            return Path.GetFullPath(outputCandidate);
        }

        if (!string.IsNullOrWhiteSpace(wrapper.SourceBundlePath))
        {
            return Path.GetFullPath(Path.Combine(wrapper.SourceBundlePath, artifactPath));
        }

        return Path.GetFullPath(Path.Combine(runDirectory, artifactPath));
    }

    private void AppendLog(
        string runId,
        string runDirectory,
        string text,
        IProgress<WrapperRunStreamEvent>? progress,
        string? section = null,
        string? category = null,
        string? level = null,
        WrapperRunStreamEvent? eventTemplate = null)
    {
        UpdateState(runId, state =>
        {
            state.Logs.Add(text);
            if (state.Logs.Count > 300)
            {
                state.Logs.RemoveAt(0);
            }
        }, runDirectory, progress, CopyEvent(eventTemplate, new WrapperRunStreamEvent
        {
            Type = "log",
            Text = text,
            Section = section ?? string.Empty,
            Category = category ?? string.Empty,
            Level = string.IsNullOrWhiteSpace(level) ? "info" : level
        }));
    }

    private static string TryGetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) ? property.GetString() ?? string.Empty : string.Empty;

    private static int? TryGetInt32(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value) ? value : null;

    private static bool TryGetBool(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;

    private static WrapperRunStreamEvent CopyEvent(WrapperRunStreamEvent? source, WrapperRunStreamEvent target)
    {
        if (source is null)
        {
            return target;
        }

        target.EventId = source.EventId;
        target.ChainId = source.ChainId;
        target.ParentChainId = source.ParentChainId;
        target.StepId = source.StepId;
        target.Phase = source.Phase;
        target.Depth = source.Depth;
        target.Title = source.Title;
        target.IsChainStart = source.IsChainStart;
        target.IsChainEnd = source.IsChainEnd;
        target.IsTokenStream = source.IsTokenStream;
        target.ArtifactPath = source.ArtifactPath;
        target.ValidationStatus = source.ValidationStatus;
        target.ApprovalId = source.ApprovalId;
        target.ToolName = source.ToolName;
        target.MemoryId = source.MemoryId;
        return target;
    }

    private WrapperRunState FinalizeArtifacts(string runId, TemplateDefinition template, string runDirectory)
    {
        var outputDirectory = Path.Combine(runDirectory, "output");
        var discoveredArtifacts = new List<WrapperArtifact>();
        if (template.ArtifactOutputs.Discover && Directory.Exists(outputDirectory))
        {
            foreach (var pattern in template.ArtifactOutputs.Globs)
            {
                foreach (var path in Directory.EnumerateFiles(outputDirectory, pattern, SearchOption.AllDirectories))
                {
                    if (discoveredArtifacts.Any(existing => string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    discoveredArtifacts.Add(new WrapperArtifact
                    {
                        Label = Path.GetFileName(path),
                        Path = path,
                        Kind = "file"
                    });
                }
            }
        }

        UpdateState(runId, state =>
        {
            if (state.TextOutputs.Count == 0 && !string.IsNullOrWhiteSpace(state.FinalText))
            {
                state.TextOutputs.Add(state.FinalText);
            }

            if (string.IsNullOrWhiteSpace(state.PrimaryTextOutput) && !string.IsNullOrWhiteSpace(state.FinalText))
            {
                state.PrimaryTextOutput = state.FinalText;
            }

            foreach (var artifact in discoveredArtifacts)
            {
                if (!state.ArtifactOutputs.Any(existing => string.Equals(existing.Path, artifact.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    state.ArtifactOutputs.Add(artifact);
                }
            }
        }, runDirectory, progress: null, streamEvent: null);

        return GetRun(runId) ?? new WrapperRunState
        {
            RunId = runId,
            TemplateId = template.Id,
            TemplateName = template.Name,
            Status = "error",
            StatusMessage = "Run missing after finalize"
        };
    }

    private void UpdateState(
        string runId,
        Action<WrapperRunState> updater,
        string runDirectory,
        IProgress<WrapperRunStreamEvent>? progress,
        WrapperRunStreamEvent? streamEvent)
    {
        WrapperRunState? clonedState = null;
        lock (_lock)
        {
            if (!_runs.TryGetValue(runId, out var state))
            {
                return;
            }

            updater(state);
            clonedState = state.Clone();
        }

        if (clonedState is null)
        {
            return;
        }

        WriteState(runDirectory, clonedState);
        if (streamEvent is not null)
        {
            progress?.Report(streamEvent);
        }
        RunUpdated?.Invoke(clonedState);
    }

    private void StoreState(WrapperRunState state)
    {
        lock (_lock)
        {
            _runs[state.RunId] = state.Clone();
        }

        RunUpdated?.Invoke(state.Clone());
    }

    private void WriteState(string runDirectory, WrapperRunState state)
    {
        File.WriteAllText(Path.Combine(runDirectory, "state.json"), JsonSerializer.Serialize(state, _jsonOptions));
    }

    private WrapperRunState CreateFailedRun(string errorCode, WrapperRunRequest request, TemplateDefinition? template = null)
    {
        var runId = $"wraprun-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..44];
        var state = new WrapperRunState
        {
            RunId = runId,
            TemplateId = template?.Id ?? request.TemplateId,
            TemplateName = template?.Name ?? request.TemplateId,
            WrapperId = request.WrapperId,
            SessionId = ResolveSessionId(request, runId),
            Status = "error",
            StatusMessage = errorCode,
            Error = errorCode,
            Prompt = request.Prompt,
            RootDirectory = ResolveRootDirectory(request),
            MaxTokens = request.MaxTokens,
            ModelId = request.ModelId,
            ContractStatus = "error",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            ExitCode = 1
        };
        StoreState(state);
        return state;
    }

    private void CleanupRun(string runId)
    {
        lock (_lock)
        {
            _runningProcesses.Remove(runId);
            _completionTasks.Remove(runId);
            _cancelledRuns.Remove(runId);
        }
    }

    private sealed class ExternalBatchedAgentInput
    {
        public string RunId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string WrapperId { get; set; } = string.Empty;
        public string WrapperName { get; set; } = string.Empty;
        public string IoContractId { get; set; } = string.Empty;
        public string MemoryStrategy { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string RootDirectory { get; set; } = string.Empty;
        public List<string> TextInputRoles { get; set; } = new();
        public List<string> TextInputs { get; set; } = new();
        public List<string> ArtifactInputs { get; set; } = new();
        public string ModelId { get; set; } = string.Empty;
        public int MaxTokens { get; set; }
        public string OutputDir { get; set; } = string.Empty;
        public string OutputJson { get; set; } = string.Empty;
        public string NexuscentralBaseUrl { get; set; } = string.Empty;
        public string LlmToolRoute { get; set; } = "/nexuscentral/thinking";
        public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ExternalWrappedArtifact
    {
        public string? Label { get; set; }
        public string? Path { get; set; }
        public string? Kind { get; set; }
    }

    private sealed class ExternalBatchedAgentRawOutput
    {
        public string? PrimaryTextOutput { get; set; }
        public List<string> TextOutputs { get; set; } = new();
        public List<string> Logs { get; set; } = new();
        public string? Error { get; set; }
        public string? StatusMessage { get; set; }
        public string? ContractStatus { get; set; }
        public List<ExternalWrappedArtifact> ArtifactOutputs { get; set; } = new();
    }

    private sealed class ExternalContractResult
    {
        public string? PrimaryTextOutput { get; set; }
        public List<string> TextOutputs { get; set; } = new();
        public List<string> Logs { get; set; } = new();
        public string? Error { get; set; }
        public string? StatusMessage { get; set; }
        public string? ContractStatus { get; set; }
        public List<WrapperArtifact> ArtifactOutputs { get; set; } = new();
    }

}
