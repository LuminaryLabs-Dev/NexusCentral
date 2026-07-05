using System;
using System.Collections.Generic;
using NexusCentral.V1.Core.Execution;

namespace NexusCentral.V1.Features.Models;

public enum ModelEndpointHealth
{
    Inactive,
    Unknown,
    Healthy,
    Unhealthy
}

public sealed record ModelEndpointDefinition(
    string Id,
    string Name,
    ProviderKind ProviderKind,
    string BaseUrl,
    string SelectedModelId,
    ModelEndpointHealth Health,
    DateTimeOffset? LastProbeUtc,
    string StatusMessage,
    IReadOnlyList<string> AvailableModelIds);

public sealed record ModelEndpointCreateRequest(
    string? Id,
    string Name,
    ProviderKind ProviderKind,
    string BaseUrl,
    string SelectedModelId);

public sealed record ModelEndpointProbeResult(
    bool Ok,
    IReadOnlyList<string> ModelIds,
    string Message);

public sealed record ModelEndpointSnapshot(
    ModelEndpointDefinition? ActiveEndpoint,
    IReadOnlyList<ModelEndpointDefinition> Endpoints,
    string ModelStatusText,
    string RuntimeStatusText,
    string ContextStatusText);

internal sealed record ModelEndpointRegistry(
    string ActiveEndpointId,
    List<ModelEndpointDefinition> Endpoints);
