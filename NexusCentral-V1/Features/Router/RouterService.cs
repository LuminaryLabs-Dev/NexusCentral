using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V1.Core.Config;
using NexusCentral.V1.Core.Logging;
using NexusCentral.V1.Core.State;
using NexusCentral.V1.Features.Harnesses;
using NexusCentral.V1.Features.Models;

namespace NexusCentral.V1.Features.Router;

public sealed class RouterService : IRouterService
{
    private const string LocalProviderRouteId = "local-provider";
    private const string HostedHarnessRouteId = "hosted-harness";

    private static readonly JsonSerializerOptions RegistryJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppPathSettings _paths;
    private readonly JsonFileStore _json;
    private readonly IModelEndpointService _models;
    private readonly IHarnessImporterService? _harnesses;
    private readonly IHostedHarnessTurnRunner? _turnRunner;
    private readonly IAppLogService _logs;
    private readonly object _gate = new();
    private RouterRegistry _registry = new(string.Empty);

    public RouterService(AppPathSettings paths, JsonFileStore json, IModelEndpointService models, IAppLogService logs)
        : this(paths, json, models, null, logs)
    {
    }

    public RouterService(
        AppPathSettings paths,
        JsonFileStore json,
        IModelEndpointService models,
        IHarnessImporterService? harnesses,
        IAppLogService logs,
        IHostedHarnessTurnRunner? turnRunner = null)
    {
        _paths = paths;
        _json = json;
        _models = models;
        _harnesses = harnesses;
        _turnRunner = turnRunner;
        _logs = logs;
        _paths.EnsureDirectories();
        LoadRegistry();
        _models.StateChanged += _ => PublishStateChanged();
        if (_harnesses is not null)
        {
            _harnesses.StateChanged += _ => PublishStateChanged();
        }
    }

    public event Action<RouterSnapshot>? StateChanged;

    public RouterSnapshot GetSnapshot()
    {
        var routes = BuildRoutes(_models.GetSnapshot(), _harnesses?.GetSnapshot());
        RouterRouteState? selected;
        lock (_gate)
        {
            selected = routes.FirstOrDefault(route => string.Equals(route.Id, _registry.SelectedRouteId, StringComparison.OrdinalIgnoreCase))
                ?? routes.FirstOrDefault();
        }

        return CreateSnapshot(selected, routes);
    }

    public async Task<bool> SelectRouteAsync(string routeId, CancellationToken cancellationToken = default)
    {
        var routes = BuildRoutes(_models.GetSnapshot(), _harnesses?.GetSnapshot());
        if (!routes.Any(route => string.Equals(route.Id, routeId, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        lock (_gate)
        {
            _registry = _registry with { SelectedRouteId = routeId };
        }

        await SaveRegistryAsync(cancellationToken);
        PublishStateChanged();
        return true;
    }

    public async Task<RouterSnapshot> SelectBestAvailableRouteAsync(CancellationToken cancellationToken = default)
    {
        var routes = BuildRoutes(_models.GetSnapshot(), _harnesses?.GetSnapshot());
        var selected = routes
            .Where(route => route.Availability == RouterAvailability.Available)
            .OrderByDescending(route => string.Equals(route.Id, HostedHarnessRouteId, StringComparison.OrdinalIgnoreCase))
            .ThenBy(route => route.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? routes.FirstOrDefault();

        if (selected is null)
        {
            var emptySnapshot = CreateSnapshot(null, routes);
            StateChanged?.Invoke(emptySnapshot);
            return emptySnapshot;
        }

        lock (_gate)
        {
            _registry = _registry with { SelectedRouteId = selected.Id };
        }

        await SaveRegistryAsync(cancellationToken);
        var snapshot = CreateSnapshot(selected, routes);
        _logs.Append("info", "router", $"Selected best route: {selected.Name}.");
        StateChanged?.Invoke(snapshot);
        return snapshot;
    }

    public Task<RouterSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = GetSnapshot();
        _logs.Append("info", "router", snapshot.ActiveRoute?.StatusMessage ?? "Router has no active route.");
        StateChanged?.Invoke(snapshot);
        return Task.FromResult(snapshot);
    }

    public async Task<RouterSmokeResult> SmokeSelectedRouteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var snapshot = GetSnapshot();
        var route = snapshot.ActiveRoute;
        if (route is null)
        {
            return CreateSmokeFailure(string.Empty, "No route", "No selected route.");
        }

        if (route.Availability != RouterAvailability.Available)
        {
            return CreateSmokeFailure(route.Id, route.Name, route.StatusMessage);
        }

        if (route.Id != HostedHarnessRouteId)
        {
            return CreateSmokeFailure(route.Id, route.Name, "Select the hosted harness route before running a route smoke.");
        }

        var harness = _harnesses?.GetSnapshot().ActiveHarness;
        if (harness is null || harness.Kind != HarnessKind.Manual)
        {
            return CreateSmokeFailure(route.Id, route.Name, "No active manual hosted harness is selected.");
        }

        if (_turnRunner is null)
        {
            return CreateSmokeFailure(route.Id, route.Name, "No hosted harness runner is wired.");
        }

        var started = DateTimeOffset.UtcNow;
        var turn = await _turnRunner.RunTurnAsync(
            harness,
            new HostedHarnessTurnRequest(
                string.IsNullOrWhiteSpace(prompt) ? "Reply with exactly: route ready" : prompt,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                TimeSpan.FromMinutes(4)),
            cancellationToken);
        var duration = DateTimeOffset.UtcNow - started;
        var message = turn.Ok
            ? $"Route smoke passed through {harness.Name}."
            : $"Route smoke failed: {turn.Message}";
        _logs.Append(turn.Ok ? "info" : "warn", "router", message);
        return new RouterSmokeResult(turn.Ok, route.Id, route.Name, message, turn.Assistant, duration);
    }

    private void LoadRegistry()
    {
        if (!File.Exists(_paths.RouterRegistryPath))
        {
            _registry = new RouterRegistry(string.Empty);
            return;
        }

        using var stream = File.OpenRead(_paths.RouterRegistryPath);
        _registry = JsonSerializer.Deserialize<RouterRegistry>(stream, RegistryJsonOptions) ?? new RouterRegistry(string.Empty);
    }

    private async Task SaveRegistryAsync(CancellationToken cancellationToken)
    {
        RouterRegistry snapshot;
        lock (_gate)
        {
            snapshot = _registry;
        }

        await _json.WriteAsync(_paths.RouterRegistryPath, snapshot, cancellationToken);
    }

    private void PublishStateChanged()
    {
        StateChanged?.Invoke(GetSnapshot());
    }

    private static RouterSmokeResult CreateSmokeFailure(string routeId, string routeName, string message)
    {
        return new RouterSmokeResult(false, routeId, routeName, message, string.Empty, TimeSpan.Zero);
    }

    private static IReadOnlyList<RouterRouteState> BuildRoutes(ModelEndpointSnapshot modelSnapshot, HarnessImporterSnapshot? harnessSnapshot)
    {
        var routes = new List<RouterRouteState>();
        var activeEndpoint = modelSnapshot.ActiveEndpoint;
        if (activeEndpoint is null)
        {
            routes.Add(new RouterRouteState(
                LocalProviderRouteId,
                "Local/provider route",
                "Model endpoint",
                RouterAvailability.Inactive,
                string.Empty,
                "No endpoint",
                string.Empty,
                0,
                0,
                null,
                "No selected model endpoint."));

            AddHostedHarnessRoute(routes, null, harnessSnapshot);
            return routes;
        }

        var availability = activeEndpoint.Health switch
        {
            ModelEndpointHealth.Healthy => RouterAvailability.Available,
            ModelEndpointHealth.Unhealthy => RouterAvailability.Unhealthy,
            _ => RouterAvailability.Inactive
        };

        var capacity = availability == RouterAvailability.Available ? 1 : 0;
        var status = availability switch
        {
            RouterAvailability.Available => $"Route available through {activeEndpoint.Name}.",
            RouterAvailability.Unhealthy => activeEndpoint.StatusMessage,
            _ => "Selected model endpoint has not been probed."
        };

        routes.Add(new RouterRouteState(
            LocalProviderRouteId,
            "Local/provider route",
            activeEndpoint.ProviderKind.ToString(),
            availability,
            activeEndpoint.Id,
            activeEndpoint.Name,
            activeEndpoint.SelectedModelId,
            0,
            capacity,
            activeEndpoint.LastProbeUtc,
            status));

        AddHostedHarnessRoute(routes, activeEndpoint, harnessSnapshot);
        return routes;
    }

    private static void AddHostedHarnessRoute(
        List<RouterRouteState> routes,
        ModelEndpointDefinition? activeEndpoint,
        HarnessImporterSnapshot? harnessSnapshot)
    {
        var activeHarness = harnessSnapshot?.ActiveHarness;
        if (activeHarness is null)
        {
            return;
        }

        var endpointIsHealthy = activeEndpoint?.Health == ModelEndpointHealth.Healthy;
        var harnessIsHosted = activeHarness.Kind == HarnessKind.Manual && activeHarness.Health == HarnessHealth.Discovered;
        var availability = endpointIsHealthy && harnessIsHosted
            ? RouterAvailability.Available
            : RouterAvailability.Inactive;
        var status = availability == RouterAvailability.Available
            ? $"Hosted harness route available through {activeHarness.Name} on {activeEndpoint!.Name}."
            : "Hosted harness route needs an active manual harness and healthy model endpoint.";

        routes.Add(new RouterRouteState(
            HostedHarnessRouteId,
            "Hosted harness route",
            "Hosted harness",
            availability,
            activeHarness.Id,
            activeHarness.Name,
            activeEndpoint?.SelectedModelId ?? string.Empty,
            0,
            availability == RouterAvailability.Available ? 1 : 0,
            activeHarness.LastDiscoveredUtc ?? activeEndpoint?.LastProbeUtc,
            status));
    }

    private static RouterSnapshot CreateSnapshot(RouterRouteState? selected, IReadOnlyList<RouterRouteState> routes)
    {
        if (selected is null)
        {
            return new RouterSnapshot(
                null,
                routes,
                "Route: inactive",
                "Queue inactive",
                "Endpoint: inactive",
                "Health: unavailable");
        }

        var routeStatus = selected.Availability switch
        {
            RouterAvailability.Available => $"Route: {selected.EndpointName}",
            RouterAvailability.Unhealthy => "Route: unhealthy",
            _ => "Route: inactive"
        };

        var queueStatus = selected.Availability == RouterAvailability.Available
            ? $"{selected.QueueDepth} waiting"
            : "Queue inactive";

        var endpoint = string.IsNullOrWhiteSpace(selected.EndpointName)
            ? "Endpoint: inactive"
            : $"Endpoint: {selected.EndpointName}";

        var health = selected.Availability switch
        {
            RouterAvailability.Available => "Health: available",
            RouterAvailability.Unhealthy => "Health: unhealthy",
            _ => "Health: inactive"
        };

        return new RouterSnapshot(selected, routes, routeStatus, queueStatus, endpoint, health);
    }
}
