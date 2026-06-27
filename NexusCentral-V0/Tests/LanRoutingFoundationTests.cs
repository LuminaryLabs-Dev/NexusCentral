using System.Text.Json;
using NexusCentral_Avalonia.Services;
using Xunit;

namespace NexusCentral_Triceratops.Tests;

public sealed class LanRoutingFoundationTests : IDisposable
{
    private readonly List<string> _temporaryDirectories = new();

    [Fact]
    public void LanRoutingSettingsService_PersistsStableLocalPeerId()
    {
        var paths = CreatePaths();
        var logs = new LogService();

        var first = new LanRoutingSettingsService(paths, logs).GetSnapshot();
        var second = new LanRoutingSettingsService(paths, logs).GetSnapshot();

        Assert.False(string.IsNullOrWhiteSpace(first.LocalPeerId));
        Assert.Equal(first.LocalPeerId, second.LocalPeerId);
        Assert.Equal(first.SharedSecret, second.SharedSecret);
    }

    [Fact]
    public void LanPeerRegistryService_LoadsMalformedStateWithoutThrowing()
    {
        var paths = CreatePaths();
        File.WriteAllText(paths.LanPeerRegistryPath, "{ not-json }");

        var lanRouting = new LanRoutingSettingsService(paths, new LogService());
        var service = new LanPeerRegistryService(paths, lanRouting, new LogService());

        var snapshot = service.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Peers);
        Assert.True(File.Exists(paths.LanPeerRegistryPath));

        using var document = JsonDocument.Parse(File.ReadAllText(paths.LanPeerRegistryPath));
        Assert.True(document.RootElement.TryGetProperty("SchemaVersion", out _));
    }

    [Fact]
    public void LanPeerRegistryService_PersistsTrustedPeerAcrossReload()
    {
        var paths = CreatePaths();
        var lanRouting = new LanRoutingSettingsService(paths, new LogService());
        var service = new LanPeerRegistryService(paths, lanRouting, new LogService());

        var peer = service.UpsertManualPeer("Office PC", "192.168.1.40", 3005, "TOKEN-123", isTrusted: true);
        service.UpdateCachedImportSummary(peer.PeerId, 3, "3 imported targets");
        service.MarkPeerOffline(peer.PeerId, "Peer not reachable during startup.");

        var reloaded = new LanPeerRegistryService(paths, lanRouting, new LogService());
        var snapshot = reloaded.GetSnapshot();
        var restored = Assert.Single(snapshot.Peers);

        Assert.Equal(peer.PeerId, restored.PeerId);
        Assert.Equal("Office PC", restored.DisplayName);
        Assert.Equal("offline", restored.TrustState);
        Assert.True(restored.HasSharedSecret);
        Assert.Equal(3, restored.CachedImportedTargetCount);
        Assert.Equal("3 imported targets", restored.CachedInventorySummary);
    }

    [Fact]
    public void LanPeerRegistryService_UpsertManualPeer_DoesNotClearExistingSecret()
    {
        var paths = CreatePaths();
        var lanRouting = new LanRoutingSettingsService(paths, new LogService());
        var service = new LanPeerRegistryService(paths, lanRouting, new LogService());

        var first = service.UpsertManualPeer("Office PC", "192.168.1.40", 3005, "TOKEN-123", isTrusted: true);
        var second = service.UpsertManualPeer("Office PC", "192.168.1.40", 3005, null, isTrusted: false);

        Assert.Equal(first.PeerId, second.PeerId);
        Assert.True(second.HasSharedSecret);
        Assert.True(second.IsTrusted);
    }

    [Fact]
    public void LanPeerRegistryService_AdoptsObservedPeerIdentity()
    {
        var paths = CreatePaths();
        var lanRouting = new LanRoutingSettingsService(paths, new LogService());
        var service = new LanPeerRegistryService(paths, lanRouting, new LogService());

        var first = service.UpsertManualPeer("Office PC", "192.168.1.40", 3005, "TOKEN-123", isTrusted: true);
        service.UpdateCachedImportSummary(first.PeerId, 2, "2 imported targets");

        var adopted = service.AdoptObservedPeerId(first.PeerId, "peer-office-stable");

        Assert.NotNull(adopted);
        Assert.Equal("peer-office-stable", adopted!.PeerId);
        Assert.True(adopted.IsTrusted);
        Assert.True(adopted.HasSharedSecret);
        Assert.Equal(2, adopted.CachedImportedTargetCount);

        var reloaded = new LanPeerRegistryService(paths, lanRouting, new LogService());
        var restored = Assert.Single(reloaded.GetPeers());

        Assert.Equal("peer-office-stable", restored.PeerId);
        Assert.Equal("Office PC", restored.DisplayName);
        Assert.True(restored.IsTrusted);
    }

    public void Dispose()
    {
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

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "nexuscentral-triceratops-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _temporaryDirectories.Add(path);
        return path;
    }
}