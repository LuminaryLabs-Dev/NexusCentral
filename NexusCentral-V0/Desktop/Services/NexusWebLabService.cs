using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NexusCentral_Avalonia.Services;

public class NexusWebLabService
{
    private const int FrontendPortValue = 3015;
    private const int BackendPortValue = 3016;

    private readonly AppPathsService _paths;
    private readonly LogService _logs;
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(2) };
    private readonly object _lock = new();
    private Process? _frontendProcess;
    private Process? _backendProcess;
    private string _advertisedHost = "127.0.0.1";
    private string _sessionSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    private string _mobileAccessKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(18));

    public NexusWebLabService(AppPathsService paths, LogService logs)
    {
        _paths = paths;
        _logs = logs;
        _advertisedHost = ResolveAdvertisedHost();
    }

    public event Action? StatusChanged;

    public string BindHost => "0.0.0.0";
    public int FrontendPort => FrontendPortValue;
    public int BackendPort => BackendPortValue;
    public string LocalUrl => $"http://127.0.0.1:{FrontendPort}";
    public string ApiLocalUrl => $"http://127.0.0.1:{BackendPort}";
    public string AdvertisedUrl => $"http://{_advertisedHost}:{FrontendPort}";
    public string AdvertisedHost => _advertisedHost;
    public string MobileAccessKey => _mobileAccessKey;
    public string MobileUrl => $"{AdvertisedUrl}/?mobile=1&k={Uri.EscapeDataString(_mobileAccessKey)}";
    private static bool UseRailsBackend =>
        string.Equals(Environment.GetEnvironmentVariable("NEXUS_WEB_LAB_USE_RAILS_BACKEND"), "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Environment.GetEnvironmentVariable("NEXUS_WEB_LAB_USE_RAILS_BACKEND"), "1", StringComparison.OrdinalIgnoreCase);

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return IsAlive(_frontendProcess) && (!UseRailsBackend || IsAlive(_backendProcess));
            }
        }
    }

    public string StatusText
    {
        get
        {
            var frontendRunning = IsProcessRunning(_frontendProcess);
            var backendRunning = IsProcessRunning(_backendProcess);
            if (!UseRailsBackend)
            {
                return frontendRunning
                    ? $"Nexus Web Lab online at {AdvertisedUrl} (Next.js direct proxy)"
                    : "Nexus Web Lab offline";
            }

            return (frontendRunning, backendRunning) switch
            {
                (true, true) => $"Nexus Web Lab online at {AdvertisedUrl} (Next.js + Rails)",
                (true, false) => "Nexus Web Lab partial: frontend online, backend offline",
                (false, true) => "Nexus Web Lab partial: backend online, frontend offline",
                _ => "Nexus Web Lab offline"
            };
        }
    }

    public async Task<bool> StartAsync(bool openBrowser = false)
    {
        _advertisedHost = ResolveAdvertisedHost();
        _mobileAccessKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(18));

        if (!ValidateWorkspaceLayout())
        {
            StatusChanged?.Invoke();
            return false;
        }

        if (!await EnsureDependenciesAsync())
        {
            StatusChanged?.Invoke();
            return false;
        }

        if (UseRailsBackend && !IsProcessRunning(_backendProcess))
        {
            var started = TryStartProcess(BuildBackendStartInfo(), out _backendProcess, "rails");
            if (!started)
            {
                StatusChanged?.Invoke();
                return false;
            }
        }

        if (UseRailsBackend && !await WaitForUrlHealthyAsync($"{ApiLocalUrl}/health"))
        {
            _logs.Append("error", "web-lab", "Rails backend failed health check.");
            StatusChanged?.Invoke();
            return false;
        }

        if (!IsProcessRunning(_frontendProcess))
        {
            var started = TryStartProcess(BuildFrontendStartInfo(), out _frontendProcess, "next");
            if (!started)
            {
                StatusChanged?.Invoke();
                return false;
            }
        }

        var frontendHealthy = await WaitForUrlHealthyAsync($"{LocalUrl}/");
        StatusChanged?.Invoke();

        if (frontendHealthy && openBrowser)
        {
            OpenInBrowser(LocalUrl);
        }

        return frontendHealthy;
    }

    public void Stop()
    {
        Process? frontend;
        Process? backend;
        lock (_lock)
        {
            frontend = _frontendProcess;
            backend = _backendProcess;
            _frontendProcess = null;
            _backendProcess = null;
        }

        StopProcess(frontend);
        StopProcess(backend);
        StatusChanged?.Invoke();
    }

    public void OpenInBrowser(string? url = null)
    {
        var targetUrl = string.IsNullOrWhiteSpace(url) ? LocalUrl : url;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logs.Append("warning", "web-lab", $"Failed to open Nexus Web Lab URL: {ex.Message}");
        }
    }

    private bool ValidateWorkspaceLayout()
    {
        var frontendPackageJson = _paths.GetWebLabFrontendPackageJsonPath();
        var backendGemfile = _paths.GetWebLabBackendGemfilePath();
        if (File.Exists(frontendPackageJson) && (!UseRailsBackend || File.Exists(backendGemfile)))
        {
            return true;
        }

        _logs.Append("warning", "web-lab", UseRailsBackend
            ? "Nexus Web Lab frontend/backend manifests were not found."
            : "Nexus Web Lab frontend manifest was not found.");
        return false;
    }

    private async Task<bool> EnsureDependenciesAsync()
    {
        if (!Directory.Exists(_paths.GetWebLabFrontendNodeModulesDirectory()))
        {
            var installed = await RunSetupProcessAsync(
                "npm",
                _paths.GetWebLabFrontendDirectory(),
                "install",
                "frontend");
            if (!installed)
            {
                return false;
            }
        }

        if (UseRailsBackend && !Directory.Exists(_paths.GetWebLabBackendVendorBundleDirectory()))
        {
            var installed = await RunSetupProcessAsync(
                "bundle",
                _paths.GetWebLabBackendDirectory(),
                "install",
                "--path",
                "vendor/bundle",
                "backend");
            if (!installed)
            {
                return false;
            }
        }

        return true;
    }

    private ProcessStartInfo BuildBackendStartInfo()
    {
        var psi = new ProcessStartInfo("bundle")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _paths.GetWebLabBackendDirectory()
        };
        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add("rails");
        psi.ArgumentList.Add("server");
        psi.ArgumentList.Add("-b");
        psi.ArgumentList.Add(BindHost);
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(BackendPort.ToString());
        ApplyBackendEnvironment(psi);
        return psi;
    }

    private ProcessStartInfo BuildFrontendStartInfo()
    {
        var psi = new ProcessStartInfo("npm")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _paths.GetWebLabFrontendDirectory()
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("dev");
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add("--hostname");
        psi.ArgumentList.Add(BindHost);
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(FrontendPort.ToString());
        ApplyFrontendEnvironment(psi);
        return psi;
    }

    private void ApplyBackendEnvironment(ProcessStartInfo psi)
    {
        psi.Environment["RAILS_ENV"] = "development";
        psi.Environment["BUNDLE_PATH"] = "vendor/bundle";
        psi.Environment["PORT"] = BackendPort.ToString();
        psi.Environment["NEXUSCENTRAL_INTEGRATION_BASE_URL"] = "http://127.0.0.1:3005";
        psi.Environment["NEXUS_WEB_LAB_SESSION_SECRET"] = ResolveSessionSecret();
        psi.Environment["NEXUS_WEB_LAB_CONNECT_TIMEOUT_MS"] = Environment.GetEnvironmentVariable("NEXUS_WEB_LAB_CONNECT_TIMEOUT_MS") ?? "15000";
        psi.Environment["NEXUS_WEB_LAB_ATTEMPT_TTL_SECONDS"] = Environment.GetEnvironmentVariable("NEXUS_WEB_LAB_ATTEMPT_TTL_SECONDS") ?? "300";
    }

    private void ApplyFrontendEnvironment(ProcessStartInfo psi)
    {
        psi.Environment["HOSTNAME"] = BindHost;
        psi.Environment["PORT"] = FrontendPort.ToString();
        psi.Environment["NEXUS_WEB_LAB_API_ORIGIN"] = ApiLocalUrl;
        psi.Environment["NEXUSCENTRAL_INTEGRATION_BASE_URL"] = "http://127.0.0.1:3005";
        psi.Environment["NEXT_PUBLIC_NEXUS_WEB_LAB_BASE_URL"] = LocalUrl;
        psi.Environment["NEXUS_WEB_LAB_MOBILE_KEY"] = _mobileAccessKey;
    }

    private string ResolveSessionSecret()
    {
        var configured = Environment.GetEnvironmentVariable("NEXUS_WEB_LAB_SESSION_SECRET");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        return _sessionSecret;
    }

    private bool TryStartProcess(ProcessStartInfo psi, out Process? process, string processLabel)
    {
        try
        {
            process = Process.Start(psi);
            if (process is null)
            {
                _logs.Append("error", "web-lab", $"Failed to start {processLabel} process.");
                return false;
            }

            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => StatusChanged?.Invoke();
            _ = PumpProcessLogsAsync(process.StandardOutput, "info", processLabel);
            _ = PumpProcessLogsAsync(process.StandardError, "error", processLabel);
            _logs.Append("info", "web-lab", $"Launching {processLabel} on {BindHost}:{(processLabel == "next" ? FrontendPort : BackendPort)}.");
            return true;
        }
        catch (Exception ex)
        {
            process = null;
            _logs.Append("error", "web-lab", $"Failed to start {processLabel}: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> RunSetupProcessAsync(string fileName, string workingDirectory, params string[] args)
    {
        var scope = args.LastOrDefault() ?? "setup";
        var actualArgs = args.Take(args.Length - 1).ToArray();
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        foreach (var arg in actualArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                _logs.Append("error", "web-lab", $"Failed to start {scope} dependency install.");
                return false;
            }

            _ = PumpProcessLogsAsync(process.StandardOutput, "info", $"{scope}-setup");
            _ = PumpProcessLogsAsync(process.StandardError, "error", $"{scope}-setup");
            await process.WaitForExitAsync();
            if (process.ExitCode == 0)
            {
                return true;
            }

            _logs.Append("error", "web-lab", $"{scope} dependency install failed with exit code {process.ExitCode}.");
            return false;
        }
        catch (Exception ex)
        {
            _logs.Append("error", "web-lab", $"Failed to install {scope} dependencies: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> WaitForUrlHealthyAsync(string url)
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                using var response = await _client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
            }

            await Task.Delay(500);
        }

        return false;
    }

    private async Task PumpProcessLogsAsync(StreamReader reader, string level, string scopeId)
    {
        try
        {
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line is null)
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    _logs.Append(level, "web-lab", line, scopeId);
                }
            }
        }
        catch
        {
        }
    }

    private static bool IsAlive(Process? process) => process is { HasExited: false };

    private static bool IsProcessRunning(Process? process)
    {
        try
        {
            return process is { HasExited: false };
        }
        catch
        {
            return false;
        }
    }

    private static void StopProcess(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
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
