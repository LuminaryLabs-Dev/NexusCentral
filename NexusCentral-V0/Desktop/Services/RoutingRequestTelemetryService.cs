using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NexusCentral_Avalonia.Services;

public class RoutingRequestTelemetryService
{
    private readonly object _lock = new();
    private readonly Dictionary<string, ActiveRoutingRequestState> _activeRequests = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    public IDisposable BeginRequest(string endpointName, RouteExecutionTarget? target)
    {
        return TryBeginRequest(endpointName, target).Lease;
    }

    public RoutingRequestAdmissionResult TryBeginRequest(string endpointName, RouteExecutionTarget? target, int? maxParallel = null)
    {
        if (target is null || string.IsNullOrWhiteSpace(target.RouteTargetId))
        {
            return new RoutingRequestAdmissionResult(true, 0, maxParallel, NoopLease.Instance);
        }

        var normalizedEndpoint = string.IsNullOrWhiteSpace(endpointName) ? target.EndpointName : endpointName;
        var requestId = Guid.NewGuid().ToString("N");
        var accepted = false;
        var currentRequests = 0;
        lock (_lock)
        {
            currentRequests = _activeRequests.Values.Count(request =>
                string.Equals(request.RouteTargetId, target.RouteTargetId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.EndpointName, normalizedEndpoint, StringComparison.OrdinalIgnoreCase));

            if (maxParallel.HasValue && maxParallel.Value > 0 && currentRequests >= maxParallel.Value)
            {
                return new RoutingRequestAdmissionResult(false, currentRequests, maxParallel, NoopLease.Instance);
            }

            _activeRequests[requestId] = new ActiveRoutingRequestState(
                requestId,
                target.RouteTargetId,
                normalizedEndpoint,
                DateTimeOffset.UtcNow);
            accepted = true;
            currentRequests++;
        }

        if (accepted)
        {
            Changed?.Invoke();
        }

        return new RoutingRequestAdmissionResult(accepted, currentRequests, maxParallel, new RequestLease(this, requestId));
    }

    public IReadOnlyList<RoutingTargetLoadSnapshot> GetTargetLoads()
    {
        lock (_lock)
        {
            return _activeRequests.Values
                .GroupBy(request => request.RouteTargetId, StringComparer.OrdinalIgnoreCase)
                .Select(group => new RoutingTargetLoadSnapshot(
                    group.Key,
                    group.Count(),
                    string.Join(", ", group
                        .Select(request => $"/{request.EndpointName}")
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                    group.Max(request => request.StartedAtUtc)))
                .OrderByDescending(snapshot => snapshot.CurrentRequests)
                .ThenBy(snapshot => snapshot.RouteTargetId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public int GetActiveRequestCount(string routeTargetId, string? endpointName = null)
    {
        if (string.IsNullOrWhiteSpace(routeTargetId))
        {
            return 0;
        }

        lock (_lock)
        {
            return _activeRequests.Values.Count(request =>
                string.Equals(request.RouteTargetId, routeTargetId, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(endpointName) ||
                 string.Equals(request.EndpointName, endpointName, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private void CompleteRequest(string requestId)
    {
        var removed = false;
        lock (_lock)
        {
            removed = _activeRequests.Remove(requestId);
        }

        if (removed)
        {
            Changed?.Invoke();
        }
    }

    private sealed record ActiveRoutingRequestState(
        string RequestId,
        string RouteTargetId,
        string EndpointName,
        DateTimeOffset StartedAtUtc);

    private sealed class RequestLease : IDisposable
    {
        private readonly RoutingRequestTelemetryService _owner;
        private readonly string _requestId;
        private int _disposed;

        public RequestLease(RoutingRequestTelemetryService owner, string requestId)
        {
            _owner = owner;
            _requestId = requestId;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _owner.CompleteRequest(_requestId);
        }
    }

    private sealed class NoopLease : IDisposable
    {
        public static NoopLease Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

public record RoutingTargetLoadSnapshot(
    string RouteTargetId,
    int CurrentRequests,
    string ActiveEndpointsSummary,
    DateTimeOffset LastStartedAtUtc);

public readonly record struct RoutingRequestAdmissionResult(
    bool Accepted,
    int CurrentRequests,
    int? MaxParallel,
    IDisposable Lease);
