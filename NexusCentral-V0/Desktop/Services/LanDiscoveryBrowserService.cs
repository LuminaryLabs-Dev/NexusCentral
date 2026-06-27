using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Zeroconf;

namespace NexusCentral_Avalonia.Services;

public sealed class LanDiscoveryBrowserService : IDisposable
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ScanDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxScanBackoff = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly AppPathsService _paths;
    private readonly LanRoutingSettingsService _lanRouting;
    private readonly LogService _logs;
    private readonly object _lock = new();

    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;
    private List<LanDiscoveryPeerObservation> _observations = new();
    private bool _isScanning;
    private bool _isAvailable = true;
    private string _statusText = "LAN discovery idle.";
    private int _consecutiveFailures;

    public LanDiscoveryBrowserService(AppPathsService paths, LanRoutingSettingsService lanRouting, LogService logs)
    {
        _paths = paths;
        _lanRouting = lanRouting;
        _logs = logs;
        LoadCache();
    }

    public event Action? Changed;

    public LanDiscoveryBrowserSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new LanDiscoveryBrowserSnapshot(_statusText, _isScanning, _isAvailable, _observations.ToList());
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_backgroundTask is not null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _backgroundTask = Task.Run(() => RunAsync(_cts.Token));
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? backgroundTask;
        lock (_lock)
        {
            cts = _cts;
            backgroundTask = _backgroundTask;
            _cts = null;
            _backgroundTask = null;
            _isScanning = false;
        }

        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }

        try
        {
            backgroundTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }

        Changed?.Invoke();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await RefreshAsync(cancellationToken);
            var delay = GetScanDelay();

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _isScanning = true;
            _statusText = _observations.Count == 0 ? "Scanning LAN for Nexus Central peers..." : "Refreshing LAN peer discovery...";
        }

        Changed?.Invoke();

        try
        {
            var hosts = await ZeroconfResolver.ResolveAsync(
                $"{LanDiscoveryAdvertisementService.DiscoveryServiceType}.{LanDiscoveryAdvertisementService.DiscoveryServiceDomain}.",
                ScanDuration,
                retries: 1,
                retryDelayMilliseconds: 750,
                cancellationToken: cancellationToken);

            var localPeerId = _lanRouting.GetSnapshot().LocalPeerId;
            var now = DateTimeOffset.UtcNow;
            var merged = hosts
                .SelectMany(BuildObservations)
                .Where(observation => !string.IsNullOrWhiteSpace(observation.PeerId))
                .Where(observation => !string.Equals(observation.PeerId, localPeerId, StringComparison.OrdinalIgnoreCase))
                .GroupBy(observation => observation.PeerId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(item => !string.IsNullOrWhiteSpace(item.Host))
                    .ThenByDescending(item => item.ControlPlanePort > 0)
                    .First() with { LastSeenUtc = now })
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            lock (_lock)
            {
                _observations = merged;
                _isAvailable = true;
                _isScanning = false;
                _consecutiveFailures = 0;
                _statusText = merged.Count == 0
                    ? "No LAN peers discovered yet. Manual add remains available."
                    : $"Discovered {merged.Count} LAN peer(s).";
                PersistCacheUnsafe();
            }

            Changed?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logs.Append("warning", "router", $"LAN discovery browse failed. {ex.Message}");
            lock (_lock)
            {
                _isScanning = false;
                _isAvailable = false;
                _consecutiveFailures = Math.Min(_consecutiveFailures + 1, 6);
                _statusText = _observations.Count == 0
                    ? $"LAN discovery unavailable. {ex.Message}"
                    : $"LAN discovery unavailable. Showing {_observations.Count} cached peer(s).";
            }

            Changed?.Invoke();
        }
    }

    private TimeSpan GetScanDelay()
    {
        lock (_lock)
        {
            if (_consecutiveFailures <= 0)
            {
                return ScanInterval;
            }

            var delaySeconds = ScanInterval.TotalSeconds * Math.Pow(2, _consecutiveFailures);
            return TimeSpan.FromSeconds(Math.Min(MaxScanBackoff.TotalSeconds, delaySeconds));
        }
    }

    private IEnumerable<LanDiscoveryPeerObservation> BuildObservations(IZeroconfHost host)
    {
        foreach (var service in host.Services.Values)
        {
            if (!service.Name.Contains(LanDiscoveryAdvertisementService.DiscoveryServiceType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var properties = service.Properties.FirstOrDefault();
            if (properties is null)
            {
                continue;
            }

            var peerId = GetProperty(properties, "instance_id");
            var displayName = GetProperty(properties, "display_name");
            var version = GetProperty(properties, "version");
            var controlPort = service.Port > 0
                ? service.Port
                : int.TryParse(GetProperty(properties, "control_port"), out var parsedPort)
                    ? parsedPort
                    : LanRoutingSettingsService.DefaultControlPlanePort;
            var hostAddress = host.IPAddresses
                .FirstOrDefault(address => !string.IsNullOrWhiteSpace(address) && !address.StartsWith("169.254.", StringComparison.Ordinal))
                ?? host.IPAddress
                ?? string.Empty;

            yield return new LanDiscoveryPeerObservation(
                peerId,
                string.IsNullOrWhiteSpace(displayName) ? host.DisplayName : displayName,
                hostAddress,
                controlPort,
                service.ServiceName,
                version,
                DateTimeOffset.UtcNow);
        }
    }

    private void LoadCache()
    {
        try
        {
            if (!File.Exists(_paths.LanDiscoveryCachePath))
            {
                return;
            }

            var parsed = JsonSerializer.Deserialize<LanDiscoveryCacheDocument>(File.ReadAllText(_paths.LanDiscoveryCachePath));
            if (parsed?.Peers is null)
            {
                return;
            }

            _observations = parsed.Peers
                .Where(peer => !string.IsNullOrWhiteSpace(peer.PeerId))
                .Select(peer => new LanDiscoveryPeerObservation(
                    peer.PeerId,
                    peer.DisplayName,
                    peer.Host,
                    peer.ControlPlanePort,
                    peer.ServiceName,
                    peer.Version,
                    peer.LastSeenUtc))
                .ToList();
            _statusText = _observations.Count == 0
                ? "LAN discovery cache empty."
                : $"Loaded {_observations.Count} cached LAN peer(s).";
        }
        catch (Exception ex)
        {
            _logs.Append("warning", "router", $"Could not load LAN discovery cache. {ex.Message}");
            _observations.Clear();
        }
    }

    private void PersistCacheUnsafe()
    {
        var document = new LanDiscoveryCacheDocument
        {
            Peers = _observations
                .Select(peer => new LanDiscoveryCachePeerDocument
                {
                    PeerId = peer.PeerId,
                    DisplayName = peer.DisplayName,
                    Host = peer.Host,
                    ControlPlanePort = peer.ControlPlanePort,
                    ServiceName = peer.ServiceName,
                    Version = peer.Version,
                    LastSeenUtc = peer.LastSeenUtc
                })
                .ToList()
        };

        File.WriteAllText(_paths.LanDiscoveryCachePath, JsonSerializer.Serialize(document, JsonOptions));
    }

    private static string GetProperty(IReadOnlyDictionary<string, string> properties, string key)
    {
        return properties.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
    }

    public void Dispose()
    {
        Stop();
    }
}

public record LanDiscoveryBrowserSnapshot(
    string StatusText,
    bool IsScanning,
    bool IsAvailable,
    IReadOnlyList<LanDiscoveryPeerObservation> Peers);

public class LanDiscoveryCacheDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<LanDiscoveryCachePeerDocument> Peers { get; set; } = new();
}

public class LanDiscoveryCachePeerDocument
{
    public string PeerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int ControlPlanePort { get; set; } = LanRoutingSettingsService.DefaultControlPlanePort;
    public string ServiceName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset LastSeenUtc { get; set; }
}
