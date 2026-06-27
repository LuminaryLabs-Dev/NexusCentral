using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace NexusCentral_Avalonia.Services;

public sealed class LanDiscoveryAdvertisementService : IDisposable
{
    public const string DiscoveryServiceType = "_nexuscentral._tcp";
    public const string DiscoveryServiceDomain = "local";

    private readonly LanRoutingSettingsService _lanRouting;
    private readonly LogService _logs;
    private readonly object _lock = new();
    private Process? _advertisementProcess;
    private string _statusText = "LAN discovery advertisement idle.";

    public LanDiscoveryAdvertisementService(LanRoutingSettingsService lanRouting, LogService logs)
    {
        _lanRouting = lanRouting;
        _logs = logs;
        _lanRouting.Changed += Refresh;
    }

    public event Action? Changed;

    public string StatusText
    {
        get
        {
            lock (_lock)
            {
                return _statusText;
            }
        }
    }

    public bool IsAdvertising
    {
        get
        {
            lock (_lock)
            {
                return _advertisementProcess is not null && !_advertisementProcess.HasExited;
            }
        }
    }

    public void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        lock (_lock)
        {
            StopProcessUnsafe();
            var snapshot = _lanRouting.GetSnapshot();
            if (!snapshot.ExposeControlPlaneOnLan)
            {
                _statusText = "LAN discovery advertisement disabled.";
                Changed?.Invoke();
                return;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _statusText = "LAN discovery advertisement is only implemented on macOS in this slice.";
                Changed?.Invoke();
                return;
            }

            var dnsSdPath = ResolveDnsSdPath();
            if (string.IsNullOrWhiteSpace(dnsSdPath))
            {
                _statusText = "LAN discovery advertisement unavailable because dns-sd was not found.";
                Changed?.Invoke();
                return;
            }

            var instanceName = $"Nexus Central - {snapshot.DisplayName}";
            var psi = new ProcessStartInfo(dnsSdPath)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-R");
            psi.ArgumentList.Add(instanceName);
            psi.ArgumentList.Add(DiscoveryServiceType);
            psi.ArgumentList.Add(DiscoveryServiceDomain);
            psi.ArgumentList.Add(snapshot.ControlPlanePort.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("api=v1");
            psi.ArgumentList.Add($"display_name={snapshot.DisplayName}");
            psi.ArgumentList.Add($"instance_id={snapshot.LocalPeerId}");
            psi.ArgumentList.Add("auth=token");
            psi.ArgumentList.Add("router=1");
            psi.ArgumentList.Add($"control_port={snapshot.ControlPlanePort.ToString(CultureInfo.InvariantCulture)}");
            psi.ArgumentList.Add("version=desktop");

            try
            {
                _advertisementProcess = Process.Start(psi);
                _statusText = _advertisementProcess is null
                    ? "LAN discovery advertisement failed to start."
                    : $"Advertising {instanceName} on {DiscoveryServiceType}.{DiscoveryServiceDomain}.";
            }
            catch (Exception ex)
            {
                _logs.Append("warning", "router", $"Could not start LAN discovery advertisement. {ex.Message}");
                _advertisementProcess = null;
                _statusText = $"LAN discovery advertisement unavailable. {ex.Message}";
            }
        }

        Changed?.Invoke();
    }

    public void Stop()
    {
        lock (_lock)
        {
            StopProcessUnsafe();
            _statusText = "LAN discovery advertisement stopped.";
        }

        Changed?.Invoke();
    }

    private void StopProcessUnsafe()
    {
        if (_advertisementProcess is null)
        {
            return;
        }

        try
        {
            if (!_advertisementProcess.HasExited)
            {
                _advertisementProcess.Kill(entireProcessTree: true);
                _advertisementProcess.WaitForExit(1000);
            }
        }
        catch
        {
        }
        finally
        {
            _advertisementProcess.Dispose();
            _advertisementProcess = null;
        }
    }

    private static string ResolveDnsSdPath()
    {
        var candidates = new[]
        {
            "/usr/bin/dns-sd",
            "/bin/dns-sd"
        };

        return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    public void Dispose()
    {
        _lanRouting.Changed -= Refresh;
        Stop();
    }
}
