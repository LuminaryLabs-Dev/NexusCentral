using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

internal sealed class HostedInstanceListener : IDisposable
{
    private readonly AppServices _services;
    private readonly string _instanceId;
    private readonly int _port;
    private readonly string _advertisedHost;
    private readonly HttpListener _listener = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private bool _isRunning;

    public HostedInstanceListener(AppServices services, string instanceId, int port, string advertisedHost)
    {
        _services = services;
        _instanceId = instanceId;
        _port = port;
        _advertisedHost = string.IsNullOrWhiteSpace(advertisedHost) ? "127.0.0.1" : advertisedHost.Trim();
    }

    public string InstanceId => _instanceId;
    public int Port => _port;
    public string AdvertisedHost => _advertisedHost;
    public string LoopbackBaseUrl => $"http://127.0.0.1:{_port}";
    public string AdvertisedBaseUrl => $"http://{_advertisedHost}:{_port}";

    public void Start()
    {
        foreach (var prefix in GetPrefixes())
        {
            _listener.Prefixes.Add(prefix);
        }

        _listener.Start();
        _isRunning = true;
        Task.Run(RunLoopAsync);
    }

    public void Stop()
    {
        _isRunning = false;

        try
        {
            _listener.Stop();
        }
        catch
        {
        }

        try
        {
            _listener.Close();
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private string[] GetPrefixes()
    {
        if (string.Equals(_advertisedHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_advertisedHost, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                $"http://127.0.0.1:{_port}/",
                $"http://localhost:{_port}/"
            };
        }

        return new[]
        {
            $"http://127.0.0.1:{_port}/",
            $"http://localhost:{_port}/",
            $"http://{_advertisedHost}:{_port}/"
        };
    }

    private async Task RunLoopAsync()
    {
        while (_isRunning)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = ProcessRequestAsync(context);
            }
            catch (HttpListenerException) when (!_isRunning)
            {
                break;
            }
            catch (ObjectDisposedException) when (!_isRunning)
            {
                break;
            }
            catch (Exception ex)
            {
                _services.Logs.Append("error", "integration", $"Hosted listener loop failed for {_instanceId}:{_port}. {ex.Message}", _instanceId);
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", $"Content-Type, {LanRoutingSettingsService.AuthHeaderName}");

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 200;
            response.Close();
            return;
        }

        var path = request.Url?.AbsolutePath ?? string.Empty;

        try
        {
            if (!IsAuthorizedRequest(request))
            {
                await WriteJsonAsync(response, 401, new
                {
                    ok = false,
                    error_code = "UNAUTHORIZED",
                    error = "Missing or invalid LAN routing token."
                });
                return;
            }

            switch ((request.HttpMethod, path))
            {
                case ("GET", "/nexuscentral/status"):
                    await WriteJsonAsync(response, 200, BuildStatusPayload());
                    break;
                case ("POST", "/nexuscentral/thinking"):
                    await HandleRuntimeRequestAsync(request, response, "thinking", false);
                    break;
                case ("POST", "/nexuscentral/thinking-raw"):
                    await HandleRuntimeRequestAsync(request, response, "thinking", true);
                    break;
                case ("POST", "/nexuscentral/instruct"):
                    await HandleRuntimeRequestAsync(request, response, "instruct", false);
                    break;
                default:
                    await WriteJsonAsync(response, 404, new
                    {
                        ok = false,
                        error_code = "NOT_FOUND",
                        error = $"No hosted runtime endpoint matches '{path}'."
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            await WriteJsonAsync(response, 500, new
            {
                ok = false,
                error_code = "HOSTED_RUNTIME_ERROR",
                error = ex.Message
            });
        }
    }

    private object BuildStatusPayload()
    {
        var instance = _services.Library.FindInstance(_instanceId);
        if (instance is null)
        {
            return new
            {
                ok = false,
                error_code = "INSTANCE_NOT_FOUND",
                error = $"Hosted instance '{_instanceId}' is no longer available."
            };
        }

        return new
        {
            ok = true,
            status_text = instance.IsActive && string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase)
                ? "ready"
                : instance.LoadState,
            summary_text = $"{instance.DisplayName} hosted on {_advertisedHost}:{_port}",
            instance_id = instance.InstanceId,
            model_id = instance.ModelId,
            display_name = instance.DisplayName,
            runtime_backend = instance.RuntimeBackend,
            primary_mode = instance.PrimaryMode,
            family = instance.Family,
            capabilities = instance.Capabilities,
            health_status = instance.HealthStatus,
            health_reason = instance.HealthReason,
            allow_lan_access = instance.AllowLanAccess,
            hosted_port = _port,
            loopback_base_url = LoopbackBaseUrl,
            advertised_base_url = AdvertisedBaseUrl
        };
    }

    private async Task HandleRuntimeRequestAsync(HttpListenerRequest request, HttpListenerResponse response, string endpointName, bool rawThinkingOutput)
    {
        var body = await ReadBodyAsJsonAsync(request);
        var prompt = body.TryGetProperty("prompt", out var promptElement) ? promptElement.GetString() ?? string.Empty : string.Empty;
        var modelId = body.TryGetProperty("model_id", out var modelElement) ? modelElement.GetString() : null;
        var maxTokens = body.TryGetProperty("max_tokens", out var tokensElement) && tokensElement.TryGetInt32(out var parsedTokens)
            ? parsedTokens
            : 1200;
        var maxThreads = body.TryGetProperty("max_threads", out var threadsElement) && threadsElement.TryGetInt32(out var parsedThreads) && parsedThreads > 0
            ? parsedThreads
            : (int?)null;
        var maxMemoryMb = body.TryGetProperty("max_memory_mb", out var memoryElement) && memoryElement.TryGetInt32(out var parsedMemory) && parsedMemory > 0
            ? parsedMemory
            : (int?)null;
        var stream = body.TryGetProperty("stream", out var streamElement) && streamElement.ValueKind is JsonValueKind.True;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            await WriteJsonAsync(response, 400, new
            {
                ok = false,
                error_code = "INVALID_INPUT",
                error = "Missing 'prompt' parameter."
            });
            return;
        }

        if (stream)
        {
            await HandleStreamingRuntimeRequestAsync(response, endpointName, prompt, modelId, maxTokens, rawThinkingOutput, maxThreads, maxMemoryMb);
            return;
        }

        var result = endpointName == "instruct"
            ? await _services.Runtime.RunInstructAsync(prompt, modelId, maxTokens, _instanceId, maxThreads, maxMemoryMb)
            : await _services.Runtime.RunThinkingAsync(prompt, modelId, maxTokens, _instanceId, maxThreads, maxMemoryMb);

        await WriteJsonAsync(response, 200, BuildRuntimePayload(result, rawThinkingOutput));
    }

    private async Task HandleStreamingRuntimeRequestAsync(HttpListenerResponse response, string endpointName, string prompt, string? modelId, int maxTokens, bool rawThinkingOutput, int? maxThreads, int? maxMemoryMb)
    {
        response.StatusCode = 200;
        response.ContentType = "application/x-ndjson";
        response.SendChunked = true;

        await using var writer = new StreamWriter(response.OutputStream);
        var progress = new HostedRuntimeStreamProgress(writer, _jsonOptions);

        var result = endpointName == "instruct"
            ? await _services.Runtime.RunInstructAsync(prompt, modelId, maxTokens, _instanceId, progress, maxThreads, maxMemoryMb)
            : await _services.Runtime.RunThinkingAsync(prompt, modelId, maxTokens, _instanceId, progress, maxThreads, maxMemoryMb);

        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            type = "final",
            data = BuildRuntimePayload(result, rawThinkingOutput)
        }, _jsonOptions));
        await writer.FlushAsync();
        response.Close();
    }

    private object BuildRuntimePayload(RuntimeInvocationResult result, bool rawThinkingOutput)
    {
        var output = rawThinkingOutput ? ThinkingOutputParser.GetRawOutput(result.RawOutput, result.Output) : result.Output;
        var rawOutput = rawThinkingOutput ? output : result.RawOutput;
        return new
        {
            ok = result.Ok,
            error_code = result.ErrorCode,
            is_model_health_error = result.IsModelHealthError,
            model_id = result.ModelId,
            backend = result.Backend,
            output,
            raw_output = rawOutput,
            finish_reason = result.FinishReason,
            tokens_generated = result.TokensGenerated,
            elapsed_ms = result.ElapsedMs,
            model_path = result.ModelPath,
            prompt = result.Prompt,
            error = result.Error
        };
    }

    private async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, object payload)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, _jsonOptions);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private static async Task<JsonElement> ReadBodyAsJsonAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            body = "{}";
        }

        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private bool IsAuthorizedRequest(HttpListenerRequest request)
    {
        if (IsLoopbackRequest(request))
        {
            return true;
        }

        var lanSettings = _services.LanRouting.GetSnapshot();
        var providedToken = request.Headers[LanRoutingSettingsService.AuthHeaderName];
        return LanRoutingSettingsService.SecureEquals(providedToken, lanSettings.SharedSecret);
    }

    private static bool IsLoopbackRequest(HttpListenerRequest request)
    {
        var address = request.RemoteEndPoint?.Address;
        return address is null || IPAddress.IsLoopback(address);
    }

    private sealed class HostedRuntimeStreamProgress : IProgress<RuntimeStreamEvent>
    {
        private readonly StreamWriter _writer;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly object _gate = new();

        public HostedRuntimeStreamProgress(StreamWriter writer, JsonSerializerOptions jsonOptions)
        {
            _writer = writer;
            _jsonOptions = jsonOptions;
        }

        public void Report(RuntimeStreamEvent value)
        {
            lock (_gate)
            {
                _writer.WriteLine(JsonSerializer.Serialize(new
                {
                    type = "chunk",
                    phase = value.Phase == RuntimeStreamPhase.Thinking ? "thinking" : "answer",
                    text = value.Text
                }, _jsonOptions));
                _writer.Flush();
            }
        }
    }
}
