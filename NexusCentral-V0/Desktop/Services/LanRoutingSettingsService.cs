using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NexusCentral_Avalonia.Services;

public class LanRoutingSettingsService
{
    public const string AuthHeaderName = "X-NexusCentral-Token";
    public const int DefaultControlPlanePort = 3005;

    private readonly AppPathsService _paths;
    private readonly LogService _logs;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private LanRoutingSettingsDocument _settings = new();

    public LanRoutingSettingsService(AppPathsService paths, LogService logs)
    {
        _paths = paths;
        _logs = logs;
        Load();
    }

    public event Action? Changed;

    public LanRoutingSettingsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return BuildSnapshotUnsafe();
        }
    }

    public LanRoutingSettingsSnapshot SetExposeControlPlaneOnLan(bool exposeControlPlaneOnLan)
    {
        LanRoutingSettingsSnapshot snapshot;
        lock (_lock)
        {
            _settings.ExposeControlPlaneOnLan = exposeControlPlaneOnLan;
            PersistUnsafe();
            snapshot = BuildSnapshotUnsafe();
        }

        Changed?.Invoke();
        return snapshot;
    }

    public LanRoutingSettingsSnapshot RotateSharedSecret()
    {
        LanRoutingSettingsSnapshot snapshot;
        lock (_lock)
        {
            _settings.SharedSecret = GenerateSharedSecret();
            PersistUnsafe();
            snapshot = BuildSnapshotUnsafe();
        }

        Changed?.Invoke();
        return snapshot;
    }

    public static bool SecureEquals(string? candidate, string? expected)
    {
        var normalizedCandidate = candidate?.Trim() ?? string.Empty;
        var normalizedExpected = expected?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedCandidate) || string.IsNullOrWhiteSpace(normalizedExpected))
        {
            return false;
        }

        var candidateBytes = Encoding.UTF8.GetBytes(normalizedCandidate);
        var expectedBytes = Encoding.UTF8.GetBytes(normalizedExpected);
        return CryptographicOperations.FixedTimeEquals(candidateBytes, expectedBytes);
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_paths.LanRoutingSettingsPath))
            {
                var parsed = JsonSerializer.Deserialize<LanRoutingSettingsDocument>(File.ReadAllText(_paths.LanRoutingSettingsPath));
                if (parsed is not null)
                {
                    _settings = parsed;
                }
            }
        }
        catch (Exception ex)
        {
            _logs.Append("warning", "router", $"Could not load LAN routing settings. {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(_settings.SharedSecret))
        {
            _settings.SharedSecret = GenerateSharedSecret();
        }

        if (string.IsNullOrWhiteSpace(_settings.LocalPeerId))
        {
            _settings.LocalPeerId = GenerateLocalPeerId();
        }

        PersistUnsafe();
    }

    private void PersistUnsafe()
    {
        File.WriteAllText(_paths.LanRoutingSettingsPath, JsonSerializer.Serialize(_settings, _jsonOptions));
    }

    private LanRoutingSettingsSnapshot BuildSnapshotUnsafe()
    {
        var advertisedHost = ResolveAdvertisedHost();
        return new LanRoutingSettingsSnapshot(
            _settings.ExposeControlPlaneOnLan,
            _settings.SharedSecret,
            _settings.LocalPeerId,
            advertisedHost,
            DefaultControlPlanePort,
            Environment.MachineName);
    }

    private static string GenerateSharedSecret()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    }

    private static string GenerateLocalPeerId()
    {
        return $"peer-{Guid.NewGuid():N}";
    }

    private static string ResolveAdvertisedHost()
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up &&
                    adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var adapter in networkInterfaces)
            {
                var address = adapter.GetIPProperties()
                    .UnicastAddresses
                    .Select(item => item.Address)
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("169.254.", StringComparison.Ordinal));

                if (address is not null)
                {
                    return address.ToString();
                }
            }
        }
        catch
        {
        }

        return "127.0.0.1";
    }
}

public record LanRoutingSettingsSnapshot(
    bool ExposeControlPlaneOnLan,
    string SharedSecret,
    string LocalPeerId,
    string AdvertisedHost,
    int ControlPlanePort,
    string DisplayName);

public class LanRoutingSettingsDocument
{
    public bool ExposeControlPlaneOnLan { get; set; }
    public string SharedSecret { get; set; } = string.Empty;
    public string LocalPeerId { get; set; } = string.Empty;
}