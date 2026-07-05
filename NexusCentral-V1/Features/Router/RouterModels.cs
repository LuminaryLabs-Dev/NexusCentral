using System;
using System.Collections.Generic;

namespace NexusCentral.V1.Features.Router;

public enum RouterAvailability
{
    Inactive,
    Available,
    Unhealthy
}

public sealed record RouterRouteState(
    string Id,
    string Name,
    string TargetKind,
    RouterAvailability Availability,
    string EndpointId,
    string EndpointName,
    string TargetModelId,
    int QueueDepth,
    int Capacity,
    DateTimeOffset? LastCheckedUtc,
    string StatusMessage);

public sealed record RouterSnapshot(
    RouterRouteState? ActiveRoute,
    IReadOnlyList<RouterRouteState> Routes,
    string RouteStatusText,
    string QueueSummaryText,
    string EndpointStatusText,
    string HealthStatusText);

public sealed record RouterSmokeResult(
    bool Ok,
    string RouteId,
    string RouteName,
    string Message,
    string Answer,
    TimeSpan Duration);

internal sealed record RouterRegistry(string SelectedRouteId);
