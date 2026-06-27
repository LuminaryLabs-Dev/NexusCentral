using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using NexusCentral_Avalonia.Services;
using Xunit;

namespace NexusCentral_Triceratops.Tests;

public sealed class LanRoutingImplementationTests : IDisposable
{
    private readonly List<IDisposable> _disposables = new();
    private readonly List<string> _temporaryDirectories = new();

    [Fact]
    public void LanDiscoveryBrowserService_LoadsMalformedCacheWithoutThrowing()
    {
        var paths = CreatePaths();
        File.WriteAllText(paths.LanDiscoveryCachePath, "{ not-json }");

        var logs = new LogService();
        var lanRouting = new LanRoutingSettingsService(paths, logs);
        var browser = new LanDiscoveryBrowserService(paths, lanRouting, logs);
        _disposables.Add(browser);

        var snapshot = browser.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Peers);
        Assert.Contains(logs.GetEvents(), entry => entry.Message.Contains("Could not load LAN discovery cache", StringComparison.Ordinal));
    }

    [Fact]
    public void LanPeerRegistryService_DiscoveryObservation_MergesIntoExistingManualPeer()
    {
        var paths = CreatePaths();
        var logs = new LogService();
        var lanRouting = new LanRoutingSettingsService(paths, logs);
        var registry = new LanPeerRegistryService(paths, lanRouting, logs);

        var manual = registry.UpsertManualPeer("Office PC", "192.168.1.40", 3005, "TOKEN-123", isTrusted: true);

        registry.ApplyDiscoveryObservations(new[]
        {
            new LanDiscoveryPeerObservation(
                "peer-office-stable",
                "Office PC",
                "192.168.1.40",
                3005,
                "_nexuscentral._tcp.local",
                "1.0.0",
                DateTimeOffset.UtcNow)
        });

        var peers = registry.GetPeers();
        var merged = Assert.Single(peers);
        Assert.Equal("peer-office-stable", merged.PeerId);
        Assert.Equal("both", merged.SourceKind);
        Assert.True(merged.IsTrusted);
        Assert.True(merged.IsManual);
        Assert.True(merged.IsDiscovered);
        Assert.NotEqual(manual.PeerId, merged.PeerId);
    }

    [Fact]
    public void LanPeerRegistryService_ApplyDiscoveryObservations_IgnoresLocalPeerId()
    {
        var paths = CreatePaths();
        var logs = new LogService();
        var lanRouting = new LanRoutingSettingsService(paths, logs);
        var registry = new LanPeerRegistryService(paths, lanRouting, logs);

        registry.ApplyDiscoveryObservations(new[]
        {
            new LanDiscoveryPeerObservation(
                lanRouting.GetSnapshot().LocalPeerId,
                "This Machine",
                "127.0.0.1",
                3005,
                "_nexuscentral._tcp.local",
                "1.0.0",
                DateTimeOffset.UtcNow)
        });

        Assert.Empty(registry.GetPeers());
    }

    [Fact]
    public async Task ModelLibraryService_ImportsPersistsAndCachesPeerOwnedTargets()
    {
        var paths = CreatePaths();
        using var remotePeer = new FakeRemotePeerServer("TOKEN-123");
        var context = CreateLibraryContext(paths);
        _disposables.Add(context.Library);

        var primary = await context.Library.ConnectRemoteInstanceAsync("Office PC", "127.0.0.1", remotePeer.Port, true, "TOKEN-123");
        Assert.NotNull(primary);

        var trustedPeer = Assert.Single(context.PeerRegistry.GetPeers());
        Assert.Equal("peer-office-stable", trustedPeer.PeerId);
        Assert.Equal("Connected", trustedPeer.PresentationState);

        var importedTargets = context.Library.GetImportedTargets(trustedPeer.PeerId);
        Assert.Equal(2, importedTargets.Count);
        Assert.All(importedTargets, target => Assert.StartsWith($"{trustedPeer.PeerId}/", target.RouteTargetId, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(importedTargets, target => target.RemoteRoles.Contains("chat", StringComparer.OrdinalIgnoreCase));

        remotePeer.InventoryAvailable = false;
        var partialResult = await context.Library.RefreshPeerAsync(trustedPeer.PeerId);
        Assert.NotNull(partialResult);

        var partialPeer = context.PeerRegistry.GetPeer(trustedPeer.PeerId);
        Assert.NotNull(partialPeer);
        Assert.Equal("PartialImport", partialPeer!.PresentationState);
        Assert.Equal(2, context.Library.GetImportedTargets(trustedPeer.PeerId).Count);
        Assert.All(context.Library.GetImportedTargets(trustedPeer.PeerId), target => Assert.True(target.IsCached));

        context.Library.Dispose();
        _disposables.Remove(context.Library);

        var reloadedContext = CreateLibraryContext(paths);
        _disposables.Add(reloadedContext.Library);

        var reloadedPeer = Assert.Single(reloadedContext.PeerRegistry.GetPeers());
        Assert.Equal(trustedPeer.PeerId, reloadedPeer.PeerId);
        Assert.Equal(2, reloadedPeer.CachedImportedTargetCount);
        Assert.Equal(2, reloadedContext.Library.GetImportedTargets(reloadedPeer.PeerId).Count);
    }

    [Fact]
    public async Task ModelLibraryService_PreservesPeerAndImportedTargetsAcrossAuthFailureAndRepair()
    {
        var paths = CreatePaths();
        using var remotePeer = new FakeRemotePeerServer("TOKEN-123");
        var context = CreateLibraryContext(paths);
        _disposables.Add(context.Library);

        await context.Library.ConnectRemoteInstanceAsync("Office PC", "127.0.0.1", remotePeer.Port, true, "TOKEN-123");
        var trustedPeer = Assert.Single(context.PeerRegistry.GetPeers());
        Assert.Equal(2, context.Library.GetImportedTargets(trustedPeer.PeerId).Count);

        remotePeer.ExpectedToken = "TOKEN-456";
        await context.Library.RefreshPeerAsync(trustedPeer.PeerId);

        var failedPeer = context.PeerRegistry.GetPeer(trustedPeer.PeerId);
        Assert.NotNull(failedPeer);
        Assert.True(failedPeer!.IsAuthFailed);
        Assert.Equal(2, context.Library.GetImportedTargets(trustedPeer.PeerId).Count);

        var repaired = await context.Library.RetryPeerAuthAsync(trustedPeer.PeerId, "TOKEN-456");
        Assert.NotNull(repaired);

        var repairedPeer = context.PeerRegistry.GetPeer(trustedPeer.PeerId);
        Assert.NotNull(repairedPeer);
        Assert.True(repairedPeer!.IsConnected);
        Assert.False(repairedPeer.IsAuthFailed);
        Assert.Equal(2, context.Library.GetImportedTargets(trustedPeer.PeerId).Count);
    }

    [Fact]
    public async Task RuntimeService_BlocksOfflineImportedTargets()
    {
        var paths = CreatePaths();
        using var remotePeer = new FakeRemotePeerServer("TOKEN-123");
        var context = CreateLibraryContext(paths);
        _disposables.Add(context.Library);

        await context.Library.ConnectRemoteInstanceAsync("Office PC", "127.0.0.1", remotePeer.Port, true, "TOKEN-123");
        var trustedPeer = Assert.Single(context.PeerRegistry.GetPeers());
        var importedTarget = Assert.Single(context.Library.GetImportedTargets(trustedPeer.PeerId), target => target.RemoteInstanceId == "remote-alpha");

        context.PeerRegistry.MarkPeerOffline(trustedPeer.PeerId, "Peer offline for test.");
        var runtime = new RuntimeService(paths, context.Catalog, context.Library, new RoutingRequestTelemetryService(), context.Logs);

        var result = await runtime.RunThinkingAsync("hello", instanceId: importedTarget.RouteTargetId);

        Assert.False(result.Ok);
        Assert.Equal("INSTANCE_UNAVAILABLE", result.ErrorCode);
        Assert.Contains("stale", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        foreach (var disposable in Enumerable.Reverse(_disposables))
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
            }
        }

        foreach (var directory in _temporaryDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private AppPathsService CreatePaths()
    {
        return new AppPathsService(
            baseDirectory: CreateTempDirectory(),
            stateRootOverridePath: CreateTempDirectory(),
            legacyDocumentsModelsDirectory: string.Empty);
    }

    private TestLibraryContext CreateLibraryContext(AppPathsService paths)
    {
        var logs = new LogService();
        var lanRouting = new LanRoutingSettingsService(paths, logs);
        var peerRegistry = new LanPeerRegistryService(paths, lanRouting, logs);
        var catalog = new ModelCatalogService(paths, logs);
        var library = new ModelLibraryService(paths, catalog, peerRegistry, lanRouting, logs);
        return new TestLibraryContext(logs, lanRouting, peerRegistry, catalog, library);
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "nexuscentral-triceratops-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _temporaryDirectories.Add(path);
        return path;
    }

    private sealed record TestLibraryContext(
        LogService Logs,
        LanRoutingSettingsService LanRouting,
        LanPeerRegistryService PeerRegistry,
        ModelCatalogService Catalog,
        ModelLibraryService Library);

    private sealed class FakeRemotePeerServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public FakeRemotePeerServer(string expectedToken)
        {
            ExpectedToken = expectedToken;
            Port = GetFreePort();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _serverTask = Task.Run(RunAsync);
        }

        public int Port { get; }
        public string ExpectedToken { get; set; }
        public bool InventoryAvailable { get; set; } = true;

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch
            {
            }

            try
            {
                _serverTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }

            _cts.Dispose();
        }

        private async Task RunAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch
                {
                    break;
                }

                try
                {
                    await HandleAsync(context);
                }
                catch
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            if (!string.Equals(request.Headers[LanRoutingSettingsService.AuthHeaderName], ExpectedToken, StringComparison.Ordinal))
            {
                response.StatusCode = 401;
                response.Close();
                return;
            }

            object payload = request.Url?.AbsolutePath switch
            {
                "/nexuscentral/status" => new
                {
                    data = new
                    {
                        status_text = "ready",
                        summary_text = "2 imported targets",
                        instance_id = "remote-alpha",
                        model_id = "model-alpha",
                        display_name = "Office PC",
                        local_peer_id = "peer-office-stable",
                        runtime_backend = "llama.cpp",
                        primary_mode = "thinking",
                        family = "general",
                        health_status = "healthy",
                        capabilities = new[] { "text", "vision" }
                    }
                },
                "/nexuscentral/get_runtime_inventory" when InventoryAvailable => new
                {
                    data = new
                    {
                        inventory = new object[]
                        {
                            new
                            {
                                instance_id = "remote-alpha",
                                model_id = "model-alpha",
                                display_name = "Alpha",
                                family = "general",
                                load_state = "resident",
                                is_active = true,
                                primary_mode = "thinking",
                                capabilities = new[] { "text", "vision" },
                                health_status = "healthy",
                                allow_lan_access = true
                            },
                            new
                            {
                                instance_id = "remote-beta",
                                model_id = "model-beta",
                                display_name = "Beta",
                                family = "code",
                                load_state = "resident",
                                is_active = true,
                                primary_mode = "instruct",
                                capabilities = new[] { "text" },
                                health_status = "healthy",
                                allow_lan_access = true
                            }
                        }
                    }
                },
                "/nexuscentral/get_runtime_inventory" => new
                {
                    error = "inventory unavailable"
                },
                "/nexuscentral/get_runtime_roles" => new
                {
                    data = new
                    {
                        roles = new object[]
                        {
                            new { role = "chat", assigned_instance_id = "remote-alpha" },
                            new { role = "coding", assigned_instance_id = "remote-beta" }
                        }
                    }
                },
                _ => new { error = "not found" }
            };

            if (request.Url?.AbsolutePath == "/nexuscentral/get_runtime_inventory" && !InventoryAvailable)
            {
                response.StatusCode = 500;
            }
            else if (request.Url?.AbsolutePath is not ("/nexuscentral/status" or "/nexuscentral/get_runtime_inventory" or "/nexuscentral/get_runtime_roles"))
            {
                response.StatusCode = 404;
            }
            else
            {
                response.StatusCode = 200;
            }

            var body = JsonSerializer.Serialize(payload, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(body);
            response.ContentType = "application/json";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();
        }

        private static int GetFreePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}
