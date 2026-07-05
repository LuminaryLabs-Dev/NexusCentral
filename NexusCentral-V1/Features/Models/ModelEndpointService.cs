using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V1.Core.Config;
using NexusCentral.V1.Core.Execution;
using NexusCentral.V1.Core.Logging;
using NexusCentral.V1.Core.State;

namespace NexusCentral.V1.Features.Models;

public sealed class ModelEndpointService : IModelEndpointService
{
    private static readonly JsonSerializerOptions RegistryJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppPathSettings _paths;
    private readonly JsonFileStore _json;
    private readonly IModelEndpointProbe _probe;
    private readonly IAppLogService _logs;
    private readonly object _gate = new();
    private ModelEndpointRegistry _registry = new(string.Empty, new List<ModelEndpointDefinition>());

    public ModelEndpointService(AppPathSettings paths, JsonFileStore json, IModelEndpointProbe probe, IAppLogService logs)
    {
        _paths = paths;
        _json = json;
        _probe = probe;
        _logs = logs;
        _paths.EnsureDirectories();
        LoadRegistry();
        if (NormalizeDuplicateEndpoints())
        {
            SaveRegistryAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    public event Action<ModelEndpointSnapshot>? StateChanged;

    public ModelEndpointSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var endpoints = _registry.Endpoints.OrderBy(endpoint => endpoint.Name).ToList();
            var active = endpoints.FirstOrDefault(endpoint => string.Equals(endpoint.Id, _registry.ActiveEndpointId, StringComparison.OrdinalIgnoreCase));
            return CreateSnapshot(active, endpoints);
        }
    }

    public async Task<ModelEndpointDefinition> AddOrUpdateEndpointAsync(ModelEndpointCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Endpoint name is required.");
        }

        var baseUrl = NormalizeBaseUrl(request.BaseUrl);
        var id = string.IsNullOrWhiteSpace(request.Id)
            ? CreateEndpointId(request.Name, baseUrl)
            : request.Id.Trim();

        var endpoint = new ModelEndpointDefinition(
            id,
            request.Name.Trim(),
            request.ProviderKind,
            baseUrl,
            request.SelectedModelId.Trim(),
            ModelEndpointHealth.Unknown,
            null,
            "Not probed",
            Array.Empty<string>());

        lock (_gate)
        {
            var matchingEndpoint = _registry.Endpoints.FirstOrDefault(candidate =>
                !string.Equals(candidate.Id, endpoint.Id, StringComparison.OrdinalIgnoreCase)
                && IsSameTarget(candidate, endpoint));

            if (matchingEndpoint is not null)
            {
                endpoint = matchingEndpoint;
            }
            else
            {
                _registry.Endpoints.RemoveAll(candidate => string.Equals(candidate.Id, endpoint.Id, StringComparison.OrdinalIgnoreCase));
                _registry.Endpoints.Add(endpoint);
            }

            if (string.IsNullOrWhiteSpace(_registry.ActiveEndpointId))
            {
                _registry = _registry with { ActiveEndpointId = endpoint.Id };
            }
        }

        await SaveRegistryAsync(cancellationToken);
        _logs.Append("info", "models", $"Registered model endpoint '{endpoint.Name}'.", endpoint.Id);
        PublishStateChanged();
        return endpoint;
    }

    public async Task<bool> SelectEndpointAsync(string endpointId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            return false;
        }

        var selected = false;
        lock (_gate)
        {
            selected = _registry.Endpoints.Any(endpoint => string.Equals(endpoint.Id, endpointId, StringComparison.OrdinalIgnoreCase));
            if (selected)
            {
                _registry = _registry with { ActiveEndpointId = endpointId };
            }
        }

        if (!selected)
        {
            return false;
        }

        await SaveRegistryAsync(cancellationToken);
        PublishStateChanged();
        return true;
    }

    public async Task<ModelEndpointDefinition?> ProbeEndpointAsync(string endpointId, CancellationToken cancellationToken = default)
    {
        ModelEndpointDefinition? endpoint;
        lock (_gate)
        {
            endpoint = _registry.Endpoints.FirstOrDefault(candidate => string.Equals(candidate.Id, endpointId, StringComparison.OrdinalIgnoreCase));
        }

        if (endpoint is null)
        {
            return null;
        }

        var result = await _probe.ProbeAsync(endpoint, cancellationToken);
        var health = ResolveHealth(endpoint, result);
        var message = ResolveStatusMessage(endpoint, result, health);
        var updated = endpoint with
        {
            Health = health,
            LastProbeUtc = DateTimeOffset.UtcNow,
            StatusMessage = message,
            AvailableModelIds = result.ModelIds.OrderBy(id => id).ToList()
        };

        lock (_gate)
        {
            _registry.Endpoints.RemoveAll(candidate => string.Equals(candidate.Id, updated.Id, StringComparison.OrdinalIgnoreCase));
            _registry.Endpoints.Add(updated);
        }

        await SaveRegistryAsync(cancellationToken);
        _logs.Append(health == ModelEndpointHealth.Healthy ? "info" : "warn", "models", message, updated.Id);
        PublishStateChanged();
        return updated;
    }

    public async Task<bool> RemoveEndpointAsync(string endpointId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            return false;
        }

        ModelEndpointDefinition? removed = null;
        lock (_gate)
        {
            removed = _registry.Endpoints.FirstOrDefault(candidate => string.Equals(candidate.Id, endpointId, StringComparison.OrdinalIgnoreCase));
            if (removed is null)
            {
                return false;
            }

            _registry.Endpoints.RemoveAll(candidate => string.Equals(candidate.Id, endpointId, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(_registry.ActiveEndpointId, endpointId, StringComparison.OrdinalIgnoreCase))
            {
                _registry = _registry with { ActiveEndpointId = _registry.Endpoints.OrderBy(endpoint => endpoint.Name).FirstOrDefault()?.Id ?? string.Empty };
            }
        }

        await SaveRegistryAsync(cancellationToken);
        _logs.Append("info", "models", $"Removed model endpoint '{removed.Name}'.", removed.Id);
        PublishStateChanged();
        return true;
    }

    private void LoadRegistry()
    {
        if (!File.Exists(_paths.ModelEndpointRegistryPath))
        {
            _registry = new ModelEndpointRegistry(string.Empty, new List<ModelEndpointDefinition>());
            return;
        }

        using var stream = File.OpenRead(_paths.ModelEndpointRegistryPath);
        _registry = JsonSerializer.Deserialize<ModelEndpointRegistry>(stream, RegistryJsonOptions)
            ?? new ModelEndpointRegistry(string.Empty, new List<ModelEndpointDefinition>());
    }

    private bool NormalizeDuplicateEndpoints()
    {
        lock (_gate)
        {
            var deduped = _registry.Endpoints
                .GroupBy(CreateTargetKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => ChooseEndpointToKeep(group.ToList(), _registry.ActiveEndpointId))
                .OrderBy(endpoint => endpoint.Name)
                .ToList();

            if (deduped.Count == _registry.Endpoints.Count)
            {
                return false;
            }

            var activeStillExists = deduped.Any(endpoint => string.Equals(endpoint.Id, _registry.ActiveEndpointId, StringComparison.OrdinalIgnoreCase));
            _registry = _registry with
            {
                ActiveEndpointId = activeStillExists
                    ? _registry.ActiveEndpointId
                    : deduped.FirstOrDefault()?.Id ?? string.Empty,
                Endpoints = deduped
            };
            return true;
        }
    }

    private async Task SaveRegistryAsync(CancellationToken cancellationToken)
    {
        ModelEndpointRegistry snapshot;
        lock (_gate)
        {
            snapshot = _registry with { Endpoints = _registry.Endpoints.OrderBy(endpoint => endpoint.Name).ToList() };
        }

        await _json.WriteAsync(_paths.ModelEndpointRegistryPath, snapshot, cancellationToken);
    }

    private void PublishStateChanged()
    {
        StateChanged?.Invoke(GetSnapshot());
    }

    private static ModelEndpointSnapshot CreateSnapshot(ModelEndpointDefinition? active, IReadOnlyList<ModelEndpointDefinition> endpoints)
    {
        if (active is null)
        {
            return new ModelEndpointSnapshot(
                null,
                endpoints,
                "Model: inactive",
                "Runtime: inactive",
                "Context: unavailable");
        }

        var status = active.Health switch
        {
            ModelEndpointHealth.Healthy => $"Model: {active.SelectedModelId}",
            ModelEndpointHealth.Unhealthy => $"Model: unhealthy",
            ModelEndpointHealth.Unknown => "Model: unprobed",
            _ => "Model: inactive"
        };

        var runtime = active.ProviderKind switch
        {
            ProviderKind.LmStudio => "Runtime: LM Studio",
            ProviderKind.OpenAiCompatible => "Runtime: OpenAI-compatible",
            _ => $"Runtime: {active.ProviderKind}"
        };

        var context = active.Health == ModelEndpointHealth.Healthy
            ? $"{active.AvailableModelIds.Count} models available"
            : active.StatusMessage;

        return new ModelEndpointSnapshot(active, endpoints, status, runtime, $"Context: {context}");
    }

    private static ModelEndpointHealth ResolveHealth(ModelEndpointDefinition endpoint, ModelEndpointProbeResult result)
    {
        if (!result.Ok || result.ModelIds.Count == 0)
        {
            return ModelEndpointHealth.Unhealthy;
        }

        if (!string.IsNullOrWhiteSpace(endpoint.SelectedModelId)
            && !result.ModelIds.Any(id => string.Equals(id, endpoint.SelectedModelId, StringComparison.OrdinalIgnoreCase)))
        {
            return ModelEndpointHealth.Unhealthy;
        }

        return ModelEndpointHealth.Healthy;
    }

    private static string ResolveStatusMessage(ModelEndpointDefinition endpoint, ModelEndpointProbeResult result, ModelEndpointHealth health)
    {
        if (health == ModelEndpointHealth.Healthy)
        {
            return $"Healthy: {result.ModelIds.Count} models found.";
        }

        if (result.Ok && !string.IsNullOrWhiteSpace(endpoint.SelectedModelId))
        {
            return $"Selected model '{endpoint.SelectedModelId}' was not found.";
        }

        return string.IsNullOrWhiteSpace(result.Message) ? "Endpoint probe failed." : result.Message;
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Endpoint base URL is required.");
        }

        if (!Uri.TryCreate(baseUrl.Trim().TrimEnd('/'), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Endpoint base URL must be an absolute HTTP or HTTPS URL.");
        }

        return uri.ToString().TrimEnd('/');
    }

    private static string CreateEndpointId(string name, string baseUrl)
    {
        var input = $"{name.Trim().ToLowerInvariant()}|{baseUrl.ToLowerInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"model-{Convert.ToHexString(hash)[..10].ToLowerInvariant()}";
    }

    private static bool IsSameTarget(ModelEndpointDefinition left, ModelEndpointDefinition right)
    {
        return left.ProviderKind == right.ProviderKind
            && string.Equals(left.BaseUrl, right.BaseUrl, StringComparison.OrdinalIgnoreCase)
            && string.Equals(left.SelectedModelId, right.SelectedModelId, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTargetKey(ModelEndpointDefinition endpoint)
    {
        return $"{endpoint.ProviderKind}|{endpoint.BaseUrl.TrimEnd('/').ToLowerInvariant()}|{endpoint.SelectedModelId.Trim().ToLowerInvariant()}";
    }

    private static ModelEndpointDefinition ChooseEndpointToKeep(IReadOnlyList<ModelEndpointDefinition> endpoints, string activeEndpointId)
    {
        return endpoints
            .OrderByDescending(endpoint => string.Equals(endpoint.Id, activeEndpointId, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(endpoint => endpoint.Health == ModelEndpointHealth.Healthy)
            .ThenBy(endpoint => endpoint.Name, StringComparer.OrdinalIgnoreCase)
            .First();
    }
}
