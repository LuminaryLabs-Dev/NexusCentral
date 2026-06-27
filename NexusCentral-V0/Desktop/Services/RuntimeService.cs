using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class RuntimeService
{
    private static readonly HttpClient RemoteRuntimeHttpClient = new() { Timeout = TimeSpan.FromMinutes(15) };
    private readonly AppPathsService _paths;
    private readonly ModelCatalogService _catalog;
    private readonly ModelLibraryService _library;
    private readonly RoutingRequestTelemetryService _routingRequests;
    private readonly LogService _logService;

    public RuntimeService(AppPathsService paths, ModelCatalogService catalog, ModelLibraryService library, RoutingRequestTelemetryService routingRequests, LogService logService)
    {
        _paths = paths;
        _catalog = catalog;
        _library = library;
        _routingRequests = routingRequests;
        _logService = logService;
    }

    public Task<RuntimeInvocationResult> RunThinkingAsync(string prompt, string? modelId = null, int maxTokens = 1200, string? instanceId = null, int? maxThreadsOverride = null, int? maxMemoryMbOverride = null)
    {
        return RunEndpointAsync(prompt, "thinking", modelId, maxTokens, instanceId, null, maxThreadsOverride, maxMemoryMbOverride);
    }

    public Task<RuntimeInvocationResult> RunThinkingAsync(string prompt, string? modelId, int maxTokens, string? instanceId, IProgress<RuntimeStreamEvent>? streamProgress, int? maxThreadsOverride = null, int? maxMemoryMbOverride = null)
    {
        return RunEndpointAsync(prompt, "thinking", modelId, maxTokens, instanceId, streamProgress, maxThreadsOverride, maxMemoryMbOverride);
    }

    public Task<RuntimeInvocationResult> RunInstructAsync(string prompt, string? modelId = null, int maxTokens = 1200, string? instanceId = null, int? maxThreadsOverride = null, int? maxMemoryMbOverride = null)
    {
        return RunEndpointAsync(prompt, "instruct", modelId, maxTokens, instanceId, null, maxThreadsOverride, maxMemoryMbOverride);
    }

    public Task<RuntimeInvocationResult> RunInstructAsync(string prompt, string? modelId, int maxTokens, string? instanceId, IProgress<RuntimeStreamEvent>? streamProgress, int? maxThreadsOverride = null, int? maxMemoryMbOverride = null)
    {
        return RunEndpointAsync(prompt, "instruct", modelId, maxTokens, instanceId, streamProgress, maxThreadsOverride, maxMemoryMbOverride);
    }

    public Task<RuntimeInvocationResult> RunEndpointAsync(string prompt, string endpointName, string? modelId = null, int maxTokens = 1200, string? instanceId = null, int? maxThreadsOverride = null, int? maxMemoryMbOverride = null)
    {
        return RunEndpointAsync(prompt, endpointName, modelId, maxTokens, instanceId, null, maxThreadsOverride, maxMemoryMbOverride);
    }

    public async Task<RuntimeInvocationResult> RunEndpointAsync(string prompt, string endpointName, string? modelId = null, int maxTokens = 1200, string? instanceId = null, IProgress<RuntimeStreamEvent>? streamProgress = null, int? maxThreadsOverride = null, int? maxMemoryMbOverride = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var routeRole = _library.GetRole(endpointName);
        var targetInstance = SelectRouteTarget(endpointName, instanceId, modelId, routeRole);

        if (targetInstance is not null && !targetInstance.IsReady)
        {
            return new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = "INSTANCE_UNAVAILABLE",
                ModelId = targetInstance.ModelId,
                Backend = targetInstance.RuntimeBackend,
                ModelPath = targetInstance.LocalPath,
                Prompt = prompt,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Error = string.IsNullOrWhiteSpace(targetInstance.HealthReason)
                    ? $"Route target '{targetInstance.DisplayName}' is not ready for /{endpointName}. {targetInstance.HealthStatus}".Trim()
                    : targetInstance.HealthReason
            };
        }

        if (targetInstance?.IsRemote == true)
        {
            var trackedRemoteRequest = TryReserveRouteTarget(endpointName, targetInstance, routeRole, prompt, stopwatch.ElapsedMilliseconds);
            if (!trackedRemoteRequest.Accepted)
            {
                return trackedRemoteRequest.Result!;
            }

            using var remoteRequestLease = trackedRemoteRequest.Lease;
            _library.MarkRouteTargetUsed(targetInstance.RouteTargetId);
            return streamProgress is null
                ? await ForwardRemoteEndpointAsync(prompt, endpointName, modelId, maxTokens, targetInstance, maxThreadsOverride, maxMemoryMbOverride)
                : await ForwardRemoteStreamEndpointAsync(prompt, endpointName, modelId, maxTokens, targetInstance, streamProgress, maxThreadsOverride, maxMemoryMbOverride);
        }

        var model = !string.IsNullOrWhiteSpace(targetInstance?.ModelId)
            ? _catalog.FindById(targetInstance.ModelId)
            : _catalog.GetEntries().FirstOrDefault(entry => entry.IsRunnable && string.Equals(entry.RuntimeBackend, "bitnet.cpp", StringComparison.OrdinalIgnoreCase))
              ?? _catalog.GetEntries().FirstOrDefault(entry => entry.IsRunnable)
              ?? _catalog.GetEntries().FirstOrDefault();

        if (model is null)
        {
            return new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = "MODEL_NOT_FOUND",
                Prompt = prompt,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Error = "No model is available in the local catalog."
            };
        }

        if (!model.IsRunnable)
        {
            return new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = BuildModelBlockedErrorCode(model),
                IsModelHealthError = true,
                ModelId = model.ModelId,
                Backend = model.RuntimeBackend,
                ModelPath = model.LocalPath,
                Prompt = prompt,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Error = string.IsNullOrWhiteSpace(model.RunnableReason)
                    ? $"Model '{model.DisplayName}' is not runnable with the current runtime."
                    : model.RunnableReason
            };
        }

        if (targetInstance is null && (string.Equals(endpointName, "thinking", StringComparison.OrdinalIgnoreCase) || string.Equals(endpointName, "instruct", StringComparison.OrdinalIgnoreCase)))
        {
            return new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = "INSTANCE_NOT_FOUND",
                Prompt = prompt,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Error = $"No active loaded instance is available for /{endpointName}."
            };
        }

        var executable = _paths.GetRuntimeExecutablePath(model.RuntimeBackend);
        if (!File.Exists(executable))
        {
            return new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = "RUNTIME_NOT_FOUND",
                ModelId = model.ModelId,
                Backend = model.RuntimeBackend,
                ModelPath = model.LocalPath,
                Prompt = prompt,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Error = $"Runtime executable was not found at {executable}."
            };
        }

        var trackedLocalRequest = TryReserveRouteTarget(endpointName, targetInstance, routeRole, prompt, stopwatch.ElapsedMilliseconds);
        if (!trackedLocalRequest.Accepted)
        {
            return trackedLocalRequest.Result!;
        }

        using var localRequestLease = trackedLocalRequest.Lease;
        _library.MarkRouteTargetUsed(targetInstance?.RouteTargetId);
        var psi = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _paths.ResolvedRootDirectory,
        };
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(model.LocalPath);
        psi.ArgumentList.Add("--n-predict");
        psi.ArgumentList.Add(Math.Clamp(maxTokens, 1, 5000).ToString());
        psi.ArgumentList.Add("--ctx-size");
        psi.ArgumentList.Add(Math.Max(1, model.ContextSize).ToString());
        psi.ArgumentList.Add("--threads");
        var effectiveThreads = maxThreadsOverride is > 0
            ? Math.Max(1, Math.Min(model.MaxThreads, maxThreadsOverride.Value))
            : Math.Max(1, model.MaxThreads);
        psi.ArgumentList.Add(effectiveThreads.ToString());
        var memoryCapacityMb = targetInstance is not null && targetInstance.MemoryReservedMb > 0
            ? targetInstance.MemoryReservedMb
            : model.MaxMemoryMb;
        if (maxMemoryMbOverride is > 0)
        {
            memoryCapacityMb = Math.Max(1, Math.Min(memoryCapacityMb, maxMemoryMbOverride.Value));
        }
        psi.ArgumentList.Add("--max-memory-mb");
        psi.ArgumentList.Add(Math.Max(1, memoryCapacityMb).ToString());
        psi.ArgumentList.Add("--mode");
        psi.ArgumentList.Add(ResolveRuntimeMode(endpointName));
        psi.ArgumentList.Add("run-once");
        psi.ArgumentList.Add("--prompt");
        psi.ArgumentList.Add(prompt);
        if (streamProgress is not null)
        {
            psi.ArgumentList.Add("--stream-json");
        }
        else
        {
            psi.ArgumentList.Add("--json");
        }

        _logService.Append("info", "runtime", $"Launching runtime for endpoint={endpointName}, instance={targetInstance?.RouteTargetId ?? "<auto>"}, model={model.ModelId}, backend={model.RuntimeBackend}, max_tokens={maxTokens}, threads={effectiveThreads}, memory_capacity_mb={memoryCapacityMb}.", model.ModelId);
        if (!string.IsNullOrWhiteSpace(targetInstance?.RouteTargetId))
        {
            _library.AppendConsoleEvent(targetInstance.RouteTargetId, "input", prompt);
        }
        using var process = Process.Start(psi);
        if (process is null)
        {
            return new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = "RUNTIME_START_FAILED",
                ModelId = model.ModelId,
                Backend = model.RuntimeBackend,
                ModelPath = model.LocalPath,
                Prompt = prompt,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Error = "Failed to start the runtime process."
            };
        }

        string stdout;
        string streamTranscript;
        string stderr;

        if (streamProgress is null)
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            stopwatch.Stop();

            stdout = await stdoutTask;
            streamTranscript = stdout;
            stderr = await stderrTask;
        }
        else
        {
            var stderrTask = process.StandardError.ReadToEndAsync();
            var streamCapture = await ReadStreamedRuntimeOutputAsync(process, streamProgress, targetInstance?.RouteTargetId);
            stdout = streamCapture.FinalResponseJson;
            streamTranscript = streamCapture.RawTranscript;
            await process.WaitForExitAsync();
            stopwatch.Stop();
            stderr = await stderrTask;
        }
        var runtimeLogPath = EnsureRuntimeLogPath(model.ModelId);
        WriteRuntimeLog(runtimeLogPath, model.ModelId, model.LocalPath, prompt, maxTokens, stopwatch.ElapsedMilliseconds, process.ExitCode, streamTranscript, stderr);

        if (process.ExitCode != 0)
        {
            if (TryParseLocalRuntimeResult(stdout, model, prompt, stopwatch.ElapsedMilliseconds, out var parsedFailure))
            {
                var normalizedFailure = NormalizeRuntimeResult(endpointName, parsedFailure);
                _logService.Append(
                    normalizedFailure.Ok ? "info" : "error",
                    "runtime",
                    $"finish_reason={normalizedFailure.FinishReason}; tokens_generated={normalizedFailure.TokensGenerated}; elapsed_ms={normalizedFailure.ElapsedMs}; output={BuildPreview(normalizedFailure.Output)}; raw_output={BuildPreview(normalizedFailure.RawOutput)}",
                    model.ModelId);
                return normalizedFailure;
            }

            _logService.Append("error", "runtime", stderr, model.ModelId);
            if (!string.IsNullOrWhiteSpace(targetInstance?.RouteTargetId))
            {
                _library.AppendConsoleEvent(targetInstance.RouteTargetId, "error", string.IsNullOrWhiteSpace(stderr) ? $"Runtime exited with code {process.ExitCode}." : stderr.Trim());
            }
            if (TryCreateRuntimeCompatibilityFailure(stderr.Trim(), executable, model, prompt, stopwatch.ElapsedMilliseconds, out var compatibilityFailure))
            {
                return compatibilityFailure;
            }

            var catalogEntry = _catalog.MarkRuntimeFailure(model.ModelId, stderr.Trim());
            return new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = catalogEntry is not null && string.Equals(catalogEntry.HealthStatus, "quarantined", StringComparison.OrdinalIgnoreCase)
                    ? "MODEL_QUARANTINED"
                    : catalogEntry is not null && string.Equals(catalogEntry.HealthStatus, "blocked_by_memory_budget", StringComparison.OrdinalIgnoreCase)
                        ? "BLOCKED_BY_MEMORY_BUDGET"
                    : catalogEntry is not null && string.Equals(catalogEntry.HealthStatus, "runtime_unavailable", StringComparison.OrdinalIgnoreCase)
                        ? string.Equals(model.RuntimeBackend, "llama.cpp", StringComparison.OrdinalIgnoreCase) ? "LLAMA_CPP_PYTHON_MISSING" : "BITNET_RUNTIME_NOT_CONFIGURED"
                    : catalogEntry is not null && string.Equals(catalogEntry.HealthStatus, "unhealthy", StringComparison.OrdinalIgnoreCase)
                        ? "MODEL_UNHEALTHY"
                        : "RUNTIME_PROCESS_ERROR",
                IsModelHealthError = catalogEntry is not null && (string.Equals(catalogEntry.HealthStatus, "quarantined", StringComparison.OrdinalIgnoreCase) || string.Equals(catalogEntry.HealthStatus, "unhealthy", StringComparison.OrdinalIgnoreCase)),
                ModelId = model.ModelId,
                Backend = model.RuntimeBackend,
                ModelPath = model.LocalPath,
                Prompt = prompt,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Error = catalogEntry is not null && !string.IsNullOrWhiteSpace(catalogEntry.HealthReason)
                    ? catalogEntry.HealthReason
                    : string.IsNullOrWhiteSpace(stderr) ? $"Runtime exited with code {process.ExitCode}." : stderr.Trim()
            };
        }

        try
        {
            if (!TryParseLocalRuntimeResult(stdout, model, prompt, stopwatch.ElapsedMilliseconds, out var result))
            {
                throw new JsonException("Runtime output was not valid JSON.");
            }

            var normalizedResult = NormalizeRuntimeResult(endpointName, result);

            _logService.Append(
                normalizedResult.Ok ? "info" : "error",
                "runtime",
                $"finish_reason={normalizedResult.FinishReason}; tokens_generated={normalizedResult.TokensGenerated}; elapsed_ms={normalizedResult.ElapsedMs}; output={BuildPreview(normalizedResult.Output)}; raw_output={BuildPreview(normalizedResult.RawOutput)}",
                model.ModelId);
            if (!string.IsNullOrWhiteSpace(targetInstance?.RouteTargetId))
            {
                _library.AppendConsoleEvent(targetInstance.RouteTargetId, "output", normalizedResult.Output);
                if (!string.IsNullOrWhiteSpace(normalizedResult.RawOutput) && !string.Equals(normalizedResult.RawOutput, normalizedResult.Output, StringComparison.Ordinal))
                {
                    _library.AppendConsoleEvent(targetInstance.RouteTargetId, "thinking", BuildPreview(normalizedResult.RawOutput));
                }
            }
            return normalizedResult;
        }
        catch (Exception ex)
        {
            return new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = "RUNTIME_JSON_INVALID",
                ModelId = model.ModelId,
                Backend = model.RuntimeBackend,
                ModelPath = model.LocalPath,
                Prompt = prompt,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Error = $"Runtime output was not valid JSON. {ex.Message}"
            };
        }
    }

    private static bool TryParseLocalRuntimeResult(string stdout, ModelCatalogEntry model, string prompt, long elapsedMs, out RuntimeInvocationResult result)
    {
        result = new RuntimeInvocationResult();
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            result = new RuntimeInvocationResult
            {
                Ok = root.GetProperty("ok").GetBoolean(),
                ModelId = model.ModelId,
                Backend = model.RuntimeBackend,
                ModelPath = root.GetProperty("model_path").GetString() ?? model.LocalPath,
                Prompt = root.GetProperty("prompt").GetString() ?? prompt,
                Output = root.GetProperty("output").GetString() ?? string.Empty,
                RawOutput = root.TryGetProperty("raw_output", out var rawOutputElement)
                    ? rawOutputElement.GetString() ?? string.Empty
                    : string.Empty,
                FinishReason = root.TryGetProperty("finish_reason", out var finishReasonElement)
                    ? finishReasonElement.GetString() ?? string.Empty
                    : string.Empty,
                TokensGenerated = root.TryGetProperty("tokens_generated", out var tokensElement) && tokensElement.TryGetInt32(out var tokensGenerated)
                    ? tokensGenerated
                    : 0,
                ErrorCode = root.TryGetProperty("error_code", out var errorCodeElement) && errorCodeElement.ValueKind != JsonValueKind.Null
                    ? errorCodeElement.GetString() ?? string.Empty
                    : string.Empty,
                ElapsedMs = elapsedMs,
                Error = root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind != JsonValueKind.Null
                    ? errorElement.GetString() ?? string.Empty
                    : string.Empty
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<RuntimeInvocationResult> ForwardRemoteEndpointAsync(string prompt, string endpointName, string? modelId, int maxTokens, RouteExecutionTarget targetInstance, int? maxThreadsOverride, int? maxMemoryMbOverride)
    {
        var requestUrl = BuildRemoteEndpointUrl(targetInstance, endpointName);
        var requestBody = BuildRemoteRequestBody(prompt, modelId, maxTokens, targetInstance, stream: false, maxThreadsOverride: maxThreadsOverride, maxMemoryMbOverride: maxMemoryMbOverride);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        AddRemoteAuthHeader(request.Headers, targetInstance.SharedSecret);

        try
        {
            _logService.Append("info", "runtime", $"Forwarding remote runtime request to {requestUrl} for route target {targetInstance.RouteTargetId}.", targetInstance.ModelId);
            using var response = await RemoteRuntimeHttpClient.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return new RuntimeInvocationResult
                {
                    Ok = false,
                    ErrorCode = "REMOTE_RUNTIME_EMPTY",
                    ModelId = targetInstance.ModelId,
                    Backend = targetInstance.RuntimeBackend,
                    Prompt = prompt,
                    Error = $"Remote runtime at {requestUrl} returned an empty response."
                };
            }

            using var document = JsonDocument.Parse(responseText);
            return NormalizeRuntimeResult(endpointName, ParseRemoteRuntimeResult(document.RootElement, prompt, targetInstance));
        }
        catch (Exception ex)
        {
            return new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = "REMOTE_RUNTIME_ERROR",
                ModelId = targetInstance.ModelId,
                Backend = targetInstance.RuntimeBackend,
                Prompt = prompt,
                Error = $"Remote runtime request failed. {ex.Message}"
            };
        }
    }

    private async Task<RuntimeInvocationResult> ForwardRemoteStreamEndpointAsync(string prompt, string endpointName, string? modelId, int maxTokens, RouteExecutionTarget targetInstance, IProgress<RuntimeStreamEvent> streamProgress, int? maxThreadsOverride, int? maxMemoryMbOverride)
    {
        var requestUrl = BuildRemoteEndpointUrl(targetInstance, endpointName);
        var requestBody = BuildRemoteRequestBody(prompt, modelId, maxTokens, targetInstance, stream: true, maxThreadsOverride: maxThreadsOverride, maxMemoryMbOverride: maxMemoryMbOverride);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        AddRemoteAuthHeader(request.Headers, targetInstance.SharedSecret);

        try
        {
            _logService.Append("info", "runtime", $"Forwarding remote streamed runtime request to {requestUrl} for route target {targetInstance.RouteTargetId}.", targetInstance.ModelId);
            using var response = await RemoteRuntimeHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            RuntimeInvocationResult? finalResult = null;
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var eventType = root.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString() ?? string.Empty
                    : string.Empty;

                if (string.Equals(eventType, "chunk", StringComparison.OrdinalIgnoreCase))
                {
                    var phase = root.TryGetProperty("phase", out var phaseElement) && string.Equals(phaseElement.GetString(), "thinking", StringComparison.OrdinalIgnoreCase)
                        ? RuntimeStreamPhase.Thinking
                        : RuntimeStreamPhase.Answer;
                    var text = root.TryGetProperty("text", out var textElement)
                        ? textElement.GetString() ?? string.Empty
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        streamProgress.Report(new RuntimeStreamEvent(phase, text));
                    }

                    continue;
                }

                if (string.Equals(eventType, "final", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("data", out var dataElement))
                {
                    finalResult = NormalizeRuntimeResult(endpointName, ParseRemoteRuntimeResult(dataElement, prompt, targetInstance));
                }
            }

            return finalResult ?? new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = "REMOTE_RUNTIME_EMPTY",
                ModelId = targetInstance.ModelId,
                Backend = targetInstance.RuntimeBackend,
                Prompt = prompt,
                Error = $"Remote runtime at {requestUrl} did not emit a final result."
            };
        }
        catch (Exception ex)
        {
            return new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = "REMOTE_RUNTIME_ERROR",
                ModelId = targetInstance.ModelId,
                Backend = targetInstance.RuntimeBackend,
                Prompt = prompt,
                Error = $"Remote streamed runtime request failed. {ex.Message}"
            };
        }
    }

    private static string BuildRemoteEndpointUrl(RouteExecutionTarget targetInstance, string endpointName)
    {
        var route = endpointName switch
        {
            "thinking" => "thinking",
            "instruct" => "instruct",
            _ => endpointName
        };

        return $"http://{targetInstance.Host}:{targetInstance.Port}/nexuscentral/{route}";
    }

    private static string ResolveRuntimeMode(string endpointName) =>
        string.Equals(endpointName, "instruct", StringComparison.OrdinalIgnoreCase)
            ? "instruct"
            : "thinking";

    private static string BuildModelBlockedErrorCode(ModelCatalogEntry model)
    {
        if (string.Equals(model.AdmissionStatus, "unsupported", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(model.ModelFamily, "unsupported", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(model.RuntimeBackend, "unsupported-gguf", StringComparison.OrdinalIgnoreCase))
        {
            return "MODEL_UNSUPPORTED";
        }

        return model.HealthStatus switch
        {
            "quarantined" => "MODEL_QUARANTINED",
            "blocked_by_memory_budget" => "BLOCKED_BY_MEMORY_BUDGET",
            "runtime_unavailable" => string.Equals(model.RuntimeBackend, "llama.cpp", StringComparison.OrdinalIgnoreCase) ? "LLAMA_CPP_PYTHON_MISSING" : "BITNET_RUNTIME_NOT_CONFIGURED",
            "unsupported" => "MODEL_UNSUPPORTED",
            _ => string.Equals(model.RuntimeBackend, "bitnet.cpp", StringComparison.OrdinalIgnoreCase)
                ? "MODEL_NOT_ADMITTED"
                : "MODEL_UNHEALTHY"
        };
    }

    private static RuntimeInvocationResult NormalizeRuntimeResult(string endpointName, RuntimeInvocationResult result)
    {
        if (!string.Equals(endpointName, "instruct", StringComparison.OrdinalIgnoreCase) || !result.Ok)
        {
            return result;
        }

        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            return result;
        }

        result.Ok = false;
        result.ErrorCode = string.IsNullOrWhiteSpace(result.ErrorCode) ? "MODEL_OUTPUT_INVALID" : result.ErrorCode;
        result.Error = string.IsNullOrWhiteSpace(result.Error)
            ? "Model returned no usable final answer."
            : result.Error;
        return result;
    }

    private static bool TryCreateRuntimeCompatibilityFailure(
        string stderr,
        string executable,
        ModelCatalogEntry model,
        string prompt,
        long elapsedMs,
        out RuntimeInvocationResult result)
    {
        if (LooksLikeModeFlagMismatch(stderr))
        {
            result = new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = "RUNTIME_VERSION_MISMATCH",
                ModelId = model.ModelId,
                Backend = model.RuntimeBackend,
                ModelPath = model.LocalPath,
                Prompt = prompt,
                ElapsedMs = elapsedMs,
                Error = $"The packaged runtime at '{executable}' is out of date and does not support the '--mode' flag. Rebuild or republish the app bundle so Desktop and the BitNet runtime adapter come from the same revision."
            };
            return true;
        }

        result = new RuntimeInvocationResult();
        return false;
    }

    private static bool LooksLikeModeFlagMismatch(string stderr) =>
        !string.IsNullOrWhiteSpace(stderr) &&
        stderr.Contains("unexpected argument '--mode' found", StringComparison.OrdinalIgnoreCase) &&
        stderr.Contains("similar argument exists: '--model'", StringComparison.OrdinalIgnoreCase);

    private static object BuildRemoteRequestBody(string prompt, string? modelId, int maxTokens, RouteExecutionTarget targetInstance, bool stream, int? maxThreadsOverride, int? maxMemoryMbOverride)
    {
        return new
        {
            prompt,
            model_id = string.IsNullOrWhiteSpace(modelId) ? targetInstance.ModelId : modelId,
            instance_id = string.IsNullOrWhiteSpace(targetInstance.InstanceId) ? null : targetInstance.InstanceId,
            max_tokens = Math.Clamp(maxTokens, 1, 5000),
            max_threads = maxThreadsOverride is > 0 ? maxThreadsOverride.Value : 0,
            max_memory_mb = maxMemoryMbOverride is > 0 ? maxMemoryMbOverride.Value : 0,
            stream
        };
    }

    private static void AddRemoteAuthHeader(HttpRequestHeaders headers, string? sharedSecret)
    {
        if (string.IsNullOrWhiteSpace(sharedSecret))
        {
            return;
        }

        headers.TryAddWithoutValidation(LanRoutingSettingsService.AuthHeaderName, sharedSecret.Trim());
    }

    private RoutingRequestReservationResult TryReserveRouteTarget(string endpointName, RouteExecutionTarget? targetInstance, RuntimeRoleSnapshot? routeRole, string prompt, long elapsedMs)
    {
        var admission = _routingRequests.TryBeginRequest(endpointName, targetInstance, routeRole?.MaxParallel);
        if (admission.Accepted)
        {
            return new RoutingRequestReservationResult(true, admission.Lease, null);
        }

        return new RoutingRequestReservationResult(
            false,
            admission.Lease,
            new RuntimeInvocationResult
            {
                Ok = false,
                ErrorCode = "ROUTE_TARGET_BUSY",
                ModelId = targetInstance?.ModelId ?? string.Empty,
                Backend = targetInstance?.RuntimeBackend ?? string.Empty,
                ModelPath = targetInstance?.LocalPath ?? string.Empty,
                Prompt = prompt,
                ElapsedMs = elapsedMs,
                Error = targetInstance is null
                    ? $"/{endpointName} is currently at its parallel limit."
                    : $"Route target '{targetInstance.DisplayName}' already has {admission.CurrentRequests} active /{endpointName} request(s), matching max parallel {admission.MaxParallel ?? 0}."
            });
    }

    private RouteExecutionTarget? SelectRouteTarget(string endpointName, string? instanceId, string? modelId, RuntimeRoleSnapshot? routeRole)
    {
        var candidates = _library.ResolveExecutionTargets(endpointName, instanceId, modelId);
        if (candidates.Count == 0)
        {
            return null;
        }

        var maxParallel = routeRole?.MaxParallel is > 0 ? routeRole.MaxParallel : 1;
        return candidates
            .Select((target, index) => new
            {
                Target = target,
                Index = index,
                ActiveRequests = _routingRequests.GetActiveRequestCount(target.RouteTargetId, endpointName)
            })
            .Where(item => item.Target.IsReady)
            .OrderBy(item => item.ActiveRequests >= maxParallel)
            .ThenBy(item => item.ActiveRequests)
            .ThenBy(item => item.Index)
            .Select(item => item.Target)
            .FirstOrDefault()
            ?? candidates.FirstOrDefault();
    }

    private static RuntimeInvocationResult ParseRemoteRuntimeResult(JsonElement root, string prompt, RouteExecutionTarget targetInstance)
    {
        var payload = root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind != JsonValueKind.Null
            ? dataElement
            : root;

        var ok = root.TryGetProperty("ok", out var rootOkElement)
            ? rootOkElement.GetBoolean()
            : payload.TryGetProperty("ok", out var payloadOkElement)
                ? payloadOkElement.GetBoolean()
                : true;

        string errorCode = string.Empty;
        string error = string.Empty;
        var rootError = root.TryGetProperty("error", out var rootErrorElement) ? rootErrorElement : default;
        if (rootError.ValueKind == JsonValueKind.Object)
        {
            errorCode = rootError.TryGetProperty("code", out var errorCodeElement) ? errorCodeElement.GetString() ?? string.Empty : string.Empty;
            error = rootError.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? string.Empty : string.Empty;
        }

        if (payload.TryGetProperty("error_code", out var payloadErrorCodeElement))
        {
            errorCode = payloadErrorCodeElement.GetString() ?? errorCode;
        }

        if (payload.TryGetProperty("error", out var payloadErrorElement) && payloadErrorElement.ValueKind == JsonValueKind.String)
        {
            error = payloadErrorElement.GetString() ?? error;
        }

        var output = payload.TryGetProperty("output", out var outputElement)
            ? outputElement.GetString() ?? string.Empty
            : string.Empty;
        var rawOutput = payload.TryGetProperty("raw_output", out var rawOutputElement)
            ? rawOutputElement.GetString() ?? output
            : output;

        return new RuntimeInvocationResult
        {
            Ok = ok,
            ErrorCode = errorCode,
            IsModelHealthError = payload.TryGetProperty("is_model_health_error", out var healthErrorElement) && healthErrorElement.ValueKind is JsonValueKind.True,
            ModelId = payload.TryGetProperty("model_id", out var modelIdElement) ? modelIdElement.GetString() ?? targetInstance.ModelId : targetInstance.ModelId,
            Backend = payload.TryGetProperty("backend", out var backendElement) ? backendElement.GetString() ?? targetInstance.RuntimeBackend : targetInstance.RuntimeBackend,
            Output = output,
            RawOutput = rawOutput,
            FinishReason = payload.TryGetProperty("finish_reason", out var finishReasonElement) ? finishReasonElement.GetString() ?? string.Empty : string.Empty,
            TokensGenerated = payload.TryGetProperty("tokens_generated", out var tokensElement) && tokensElement.TryGetInt32(out var tokensGenerated) ? tokensGenerated : 0,
            ElapsedMs = payload.TryGetProperty("elapsed_ms", out var elapsedElement) && elapsedElement.TryGetInt64(out var elapsedMs) ? elapsedMs : 0,
            ModelPath = payload.TryGetProperty("model_path", out var modelPathElement) ? modelPathElement.GetString() ?? string.Empty : string.Empty,
            Prompt = payload.TryGetProperty("prompt", out var promptElement) ? promptElement.GetString() ?? prompt : prompt,
            Error = error
        };
    }

    private async Task<StreamCapture> ReadStreamedRuntimeOutputAsync(
        Process process,
        IProgress<RuntimeStreamEvent> streamProgress,
        string? instanceId)
    {
        var transcript = new StringBuilder();
        var finalResponseJson = string.Empty;
        string? line;

        while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            transcript.AppendLine(line);

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeElement))
                {
                    continue;
                }

                var type = typeElement.GetString();
                if (string.Equals(type, "chunk", StringComparison.OrdinalIgnoreCase))
                {
                    var phase = root.TryGetProperty("phase", out var phaseElement)
                        ? phaseElement.GetString() ?? "answer"
                        : "answer";
                    var text = root.TryGetProperty("text", out var textElement)
                        ? textElement.GetString() ?? string.Empty
                        : string.Empty;
                    if (text.Length > 0)
                    {
                        var runtimePhase = string.Equals(phase, "thinking", StringComparison.OrdinalIgnoreCase)
                            ? RuntimeStreamPhase.Thinking
                            : RuntimeStreamPhase.Answer;
                        streamProgress.Report(new RuntimeStreamEvent(runtimePhase, text));
                    }
                }
                else if (string.Equals(type, "final", StringComparison.OrdinalIgnoreCase))
                {
                    finalResponseJson = root.TryGetProperty("data", out var dataElement)
                        ? dataElement.GetRawText()
                        : string.Empty;
                }
                else
                {
                    // Ignore unknown event types in stream mode.
                }
            }
            catch (JsonException)
            {
                // Preserve the original transcript for debugging if the runtime emits a non-JSON line.
            }
        }

        return new StreamCapture(finalResponseJson, transcript.ToString());
    }

    private sealed record StreamCapture(string FinalResponseJson, string RawTranscript);

    private string EnsureRuntimeLogPath(string modelId)
    {
        var runtimeDirectory = Path.Combine(_paths.LogsDirectory, "runtime");
        Directory.CreateDirectory(runtimeDirectory);
        return Path.Combine(runtimeDirectory, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}-{Sanitize(modelId)}.log");
    }

    private static string Sanitize(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalidCharacters.Contains(character) ? '_' : character));
    }

    private static void WriteRuntimeLog(
        string path,
        string modelId,
        string modelPath,
        string prompt,
        int maxTokens,
        long elapsedMs,
        int exitCode,
        string stdout,
        string stderr)
    {
        var contents =
            $"timestamp={DateTimeOffset.UtcNow:O}{Environment.NewLine}" +
            $"model_id={modelId}{Environment.NewLine}" +
            $"model_path={modelPath}{Environment.NewLine}" +
            $"max_tokens={maxTokens}{Environment.NewLine}" +
            $"elapsed_ms={elapsedMs}{Environment.NewLine}" +
            $"exit_code={exitCode}{Environment.NewLine}" +
            $"prompt={prompt}{Environment.NewLine}" +
            $"stdout={stdout}{Environment.NewLine}" +
            $"stderr={stderr}{Environment.NewLine}";
        File.WriteAllText(path, contents);
    }

    private static string BuildPreview(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        var singleLine = value.Replace(Environment.NewLine, " ").Replace("\n", " ").Replace("\r", " ").Trim();
        return singleLine.Length <= 160 ? singleLine : $"{singleLine[..160]}...";
    }
}

internal readonly record struct RoutingRequestReservationResult(
    bool Accepted,
    IDisposable Lease,
    RuntimeInvocationResult? Result);
