using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class ModelLibraryService : IDisposable
{
    private const long MinEndpointMemoryMb = 50;
    private const long MaxEndpointMemoryMb = 2048;
    private static readonly HttpClient RoutingHttpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
    private static readonly TimeSpan RemotePeerHeartbeatInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RemotePeerStaleAfter = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan RemotePeerOfflineAfter = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RemotePeerMaxRefreshBackoff = TimeSpan.FromMinutes(2);
    private readonly AppPathsService _paths;
    private readonly ModelCatalogService _catalog;
    private readonly LanPeerRegistryService _peerRegistry;
    private readonly LanRoutingSettingsService _lanRouting;
    private readonly LogService _logs;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _remoteRefreshLock = new(1, 1);
    private readonly CancellationTokenSource _lifecycleCts = new();
    private LibraryStateDocument _state = new();

    public event Action? Changed;
    public event Action? LibraryInventoryChanged;
    public event Action<string>? InstanceChanged;
    public event Action<string>? InstanceConsoleChanged;
    public event Action? ChatInstancesChanged;

    public ModelLibraryService(AppPathsService paths, ModelCatalogService catalog, LanPeerRegistryService peerRegistry, LanRoutingSettingsService lanRouting, LogService logs)
    {
        _paths = paths;
        _catalog = catalog;
        _peerRegistry = peerRegistry;
        _lanRouting = lanRouting;
        _logs = logs;
        Load();
        _catalog.CatalogChanged += ReconcileWithCatalog;
        ReconcileWithCatalog();
        ReconcilePeerRegistryWithRemoteInstances();
        _ = Task.Run(() => TryRefreshAllRemoteInstancesAsync(_lifecycleCts.Token));
        _ = Task.Run(() => MonitorRemotePeersAsync(_lifecycleCts.Token));
    }

    public IReadOnlyList<ModelLibraryItemSnapshot> GetLibraryItems()
    {
        lock (_lock)
        {
            return _catalog.GetEntries()
                .Select(entry =>
                {
                    var instances = GetInstancesForModelUnsafe(entry.ModelId);
                    return new ModelLibraryItemSnapshot(
                        entry.ModelId,
                        entry.DisplayName,
                        entry.LocalPath,
                        entry.FileSizeBytes,
                        string.IsNullOrWhiteSpace(entry.HealthStatus) ? entry.Status : entry.HealthStatus,
                        GetLibraryLoadState(instances),
                        instances.Any(instance => instance.IsActive),
                        instances.Any(instance => instance.IsPinned),
                        entry.RuntimeBackend,
                        entry.IsRunnable,
                        entry.HealthReason,
                        instances.Select(instance => instance.PrimaryMode).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                        GetFamily(entry.DisplayName),
                        instances.Count());
                })
                .OrderByDescending(item => item.IsPinned)
                .ThenByDescending(item => item.IsLoaded)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public IReadOnlyList<LoadedInstanceSnapshot> GetLoadedInstances()
    {
        lock (_lock)
        {
            return _state.Instances
                .Where(instance => string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase) || string.Equals(instance.LoadState, "loading", StringComparison.OrdinalIgnoreCase))
                .Select(instance => BuildInstanceSnapshotUnsafe(instance))
                .OrderByDescending(instance => instance.IsPinned)
                .ThenByDescending(instance => instance.IsActive)
                .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public IReadOnlyList<LoadedInstanceSnapshot> GetChatAvailableInstances()
    {
        lock (_lock)
        {
            return _state.Instances
                .Where(instance =>
                    instance.IsActive &&
                    string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase) &&
                    instance.Capabilities.Contains("text", StringComparer.OrdinalIgnoreCase))
                .Select(instance => BuildInstanceSnapshotUnsafe(instance))
                .OrderByDescending(instance => instance.IsPinned)
                .ThenByDescending(instance => instance.LastLoadedUtc)
                .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public LoadedInstanceSnapshot? ResolveDirectChatAutoInstance()
    {
        lock (_lock)
        {
            var instance = ResolveDirectChatAutoInstanceUnsafe();
            return instance is null ? null : BuildInstanceSnapshotUnsafe(instance);
        }
    }

    public LoadedInstanceSnapshot? FindInstance(string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        lock (_lock)
        {
            var instance = _state.Instances.FirstOrDefault(candidate =>
                string.Equals(candidate.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));
            return instance is null ? null : BuildInstanceSnapshotUnsafe(instance);
        }
    }

    public IReadOnlyList<RuntimeRoleSnapshot> GetRoles()
    {
        lock (_lock)
        {
            EnsureRoutingStateUnsafe();
            return _state.Roles
                .Select(BuildRoleSnapshotUnsafe)
                .ToList();
        }
    }

    public RuntimeRoleSnapshot? GetRole(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return null;
        }

        lock (_lock)
        {
            EnsureRoutingStateUnsafe();
            var role = _state.Roles.FirstOrDefault(candidate => string.Equals(candidate.RoleName, roleName, StringComparison.OrdinalIgnoreCase));
            return role is null ? null : BuildRoleSnapshotUnsafe(role);
        }
    }

    public IReadOnlyList<RuntimeInventorySnapshot> GetInventory()
    {
        return GetLoadedInstances()
            .Select(instance => new RuntimeInventorySnapshot(
                instance.InstanceId,
                instance.ModelId,
                instance.DisplayName,
                instance.Family,
                instance.LoadState,
                instance.IsPinned ? "pinned" : "warm",
                instance.MemoryReservedMb,
                instance.MemoryActualMb,
                0,
                0,
                instance.PrimaryMode,
                instance.IsActive ? "Active instance" : "Inactive instance"))
            .ToList();
    }

    public IReadOnlyList<RoutingInstanceSnapshot> GetRoutingInstances()
    {
        lock (_lock)
        {
            EnsureRoutingStateUnsafe();
            var rolesByInstanceId = _state.Roles
                .Where(role => !string.IsNullOrWhiteSpace(role.AssignedInstanceId))
                .GroupBy(role => role.AssignedInstanceId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(role => role.RoleName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            var localTargets = _state.Instances
                .Where(instance => string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase) || string.Equals(instance.LoadState, "loading", StringComparison.OrdinalIgnoreCase))
                .Select(instance => BuildRoutingInstanceSnapshotUnsafe(instance, rolesByInstanceId.TryGetValue(instance.InstanceId, out var roles) ? roles : Array.Empty<string>()))
                .ToList();

            var remoteTargets = _state.RemoteInstances
                .Select(remote => BuildRoutingInstanceSnapshotUnsafe(remote, rolesByInstanceId.TryGetValue(remote.InstanceId, out var roles) ? roles : Array.Empty<string>()))
                .ToList();

            return localTargets
                .Concat(remoteTargets)
                .OrderBy(target => target.IsRemote)
                .ThenByDescending(target => target.IsEnabled)
                .ThenBy(target => target.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public RoutingInstanceSnapshot? UpdateInstanceNetwork(string instanceId, bool allowLanAccess, int hostedPort)
    {
        RoutingInstanceSnapshot? snapshot;
        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return null;
            }

            instance.AllowLanAccess = allowLanAccess;
            instance.HostedPort = NormalizeHostedPortUnsafe(hostedPort, instance.InstanceId);
            PersistUnsafe();
            snapshot = BuildRoutingInstanceSnapshotUnsafe(instance, GetAssignedRoleNamesUnsafe(instance.InstanceId));
        }

        NotifyLibraryInventoryChanged();
        NotifyInstanceChanged(instanceId);
        return snapshot;
    }

    public RoutingInstanceSnapshot? UpdateHostedInstanceStatus(string instanceId, string hostedStatus, string hostedStatusMessage)
    {
        RoutingInstanceSnapshot? snapshot;
        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return null;
            }

            var normalizedStatus = string.IsNullOrWhiteSpace(hostedStatus) ? "disabled" : hostedStatus.Trim();
            var normalizedMessage = hostedStatusMessage?.Trim() ?? string.Empty;
            if (string.Equals(instance.HostedStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(instance.HostedStatusMessage, normalizedMessage, StringComparison.Ordinal))
            {
                return BuildRoutingInstanceSnapshotUnsafe(instance, GetAssignedRoleNamesUnsafe(instance.InstanceId));
            }

            instance.HostedStatus = normalizedStatus;
            instance.HostedStatusMessage = normalizedMessage;
            PersistUnsafe();
            snapshot = BuildRoutingInstanceSnapshotUnsafe(instance, GetAssignedRoleNamesUnsafe(instance.InstanceId));
        }

        NotifyLibraryInventoryChanged();
        NotifyInstanceChanged(instanceId);
        return snapshot;
    }

    public async Task<RoutingInstanceSnapshot?> ConnectRemoteInstanceAsync(string displayName, string host, int port, bool allowLanAccess, string? peerToken)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0)
        {
            return null;
        }

        var peer = _peerRegistry.UpsertManualPeer(
            displayName,
            host.Trim(),
            port,
            peerToken,
            isTrusted: !string.IsNullOrWhiteSpace(peerToken));
        _peerRegistry.MarkPeerInspecting(peer.PeerId);
        var result = await ImportPeerInventoryAsync(peer.PeerId, allowLanAccess);
        return result?.PrimaryTarget;
    }

    public async Task<RoutingInstanceSnapshot?> RefreshRemoteInstanceAsync(string instanceId)
    {
        string? peerId;
        lock (_lock)
        {
            peerId = FindRemoteInstanceUnsafe(instanceId)?.PeerId;
        }

        if (string.IsNullOrWhiteSpace(peerId))
        {
            return GetRoutingInstances().FirstOrDefault(snapshot => string.Equals(snapshot.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));
        }

        var result = await ImportPeerInventoryAsync(peerId);
        if (result?.Targets.FirstOrDefault(snapshot => string.Equals(snapshot.RouteTargetId, instanceId, StringComparison.OrdinalIgnoreCase)) is not null)
        {
            return GetRoutingInstances().FirstOrDefault(snapshot => string.Equals(snapshot.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));
        }

        return result?.PrimaryTarget;
    }

    public async Task RefreshAllRemoteInstancesAsync()
    {
        await _remoteRefreshLock.WaitAsync();
        try
        {
            await RefreshAllRemoteInstancesCoreAsync(CancellationToken.None);
        }
        finally
        {
            _remoteRefreshLock.Release();
        }
    }

    public async Task<PeerImportResult?> RefreshPeerAsync(string peerId)
    {
        return await ImportPeerInventoryAsync(peerId);
    }

    public bool ForgetPeer(string peerId)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return false;
        }

        var changed = false;
        lock (_lock)
        {
            var removedTargetIds = _state.RemoteInstances
                .Where(remote => string.Equals(remote.PeerId, peerId, StringComparison.OrdinalIgnoreCase))
                .Select(remote => remote.InstanceId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (removedTargetIds.Count > 0)
            {
                _state.RemoteInstances.RemoveAll(remote => removedTargetIds.Contains(remote.InstanceId));
                foreach (var role in _state.Roles.Where(role => removedTargetIds.Contains(role.AssignedInstanceId)).ToList())
                {
                    role.AssignedInstanceId = string.Empty;
                    role.AssignedModelId = string.Empty;
                }

                PersistUnsafe();
                changed = true;
            }
        }

        if (_peerRegistry.RemovePeer(peerId))
        {
            changed = true;
        }

        if (changed)
        {
            NotifyLibraryInventoryChanged();
        }

        return changed;
    }

    public async Task<PeerImportResult?> RetryPeerAuthAsync(string peerId, string sharedSecret)
    {
        var peer = _peerRegistry.MarkPeerTrusted(peerId, sharedSecret);
        if (peer is null)
        {
            return null;
        }

        return await ImportPeerInventoryAsync(peer.PeerId);
    }

    public IReadOnlyList<ImportedRouteTargetSnapshot> GetImportedTargets(string? peerId = null)
    {
        lock (_lock)
        {
            EnsureRoutingStateUnsafe();
            return _state.RemoteInstances
                .Where(remote => string.IsNullOrWhiteSpace(peerId) || string.Equals(remote.PeerId, peerId, StringComparison.OrdinalIgnoreCase))
                .Select(remote => BuildImportedRouteTargetSnapshotUnsafe(remote, GetAssignedRoleNamesUnsafe(remote.InstanceId)))
                .OrderBy(snapshot => snapshot.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private async Task<PeerImportResult?> ImportPeerInventoryAsync(string peerId, bool? allowLanAccessOverride = null)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return null;
        }

        var peer = _peerRegistry.GetPeer(peerId);
        if (peer is null || string.IsNullOrWhiteSpace(peer.LastKnownHost) || peer.ControlPlanePort <= 0)
        {
            return null;
        }

        _peerRegistry.MarkPeerInspecting(peer.PeerId);
        var sharedSecret = peer.SharedSecret;
        var probe = await ProbeRemoteStatusAsync(peer.LastKnownHost, peer.ControlPlanePort, sharedSecret);
        var isSelfPeer = probe.IsReachable && IsLocalPeerId(probe.PeerId);
        var synchronizedPeer = SynchronizePeerAfterPeerProbe(peer, probe, isSelfPeer);
        if (synchronizedPeer is null)
        {
            return null;
        }

        var activePeerId = synchronizedPeer.PeerId;
        if (!probe.IsReachable || isSelfPeer)
        {
            lock (_lock)
            {
                MarkPeerTargetsUnavailableUnsafe(activePeerId, synchronizedPeer.IsAuthFailed ? "auth_failed" : synchronizedPeer.IsOffline ? "offline" : "blocked", synchronizedPeer.LastAuthFailureReason);
                PersistUnsafe();
            }

            NotifyLibraryInventoryChanged();
            return BuildPeerImportResult(activePeerId);
        }

        var inventoryResult = await FetchRemoteInventoryAsync(synchronizedPeer.LastKnownHost, synchronizedPeer.ControlPlanePort, synchronizedPeer.SharedSecret);
        var roleResult = await FetchRemoteRolesAsync(synchronizedPeer.LastKnownHost, synchronizedPeer.ControlPlanePort, synchronizedPeer.SharedSecret);

        var now = DateTimeOffset.UtcNow;
        lock (_lock)
        {
            MergeImportedTargetsUnsafe(
                synchronizedPeer,
                probe,
                inventoryResult.Inventory,
                roleResult.RoleAssignments,
                inventoryResult.IsSuccess,
                allowLanAccessOverride,
                now);
            PersistUnsafe();
        }

        if (inventoryResult.IsSuccess)
        {
            _peerRegistry.UpdateCachedImportSummary(activePeerId, inventoryResult.Inventory.Count, $"{inventoryResult.Inventory.Count} imported targets", now, "complete");
        }
        else
        {
            var count = GetImportedTargets(activePeerId).Count;
            _peerRegistry.MarkImportPartial(activePeerId, count, string.IsNullOrWhiteSpace(inventoryResult.ErrorMessage) ? "Imported inventory incomplete." : inventoryResult.ErrorMessage, now);
        }

        NotifyLibraryInventoryChanged();
        return BuildPeerImportResult(activePeerId);
    }

    private void ReconcilePeerRegistryWithRemoteInstances()
    {
        lock (_lock)
        {
            foreach (var remote in _state.RemoteInstances.Where(remote => !string.IsNullOrWhiteSpace(remote.PeerId)).ToList())
            {
                if (_peerRegistry.GetPeer(remote.PeerId) is not null)
                {
                    continue;
                }

                _peerRegistry.UpsertManualPeer(remote.DisplayName, remote.Host, remote.Port, remote.PeerToken, isTrusted: !string.IsNullOrWhiteSpace(remote.PeerToken));
            }
        }
    }

    private async Task<int> RefreshAllRemoteInstancesCoreAsync(CancellationToken cancellationToken)
    {
        var peerIds = _peerRegistry.GetPeers()
            .Where(peer => peer.IsTrusted)
            .Select(peer => peer.PeerId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var failureCount = 0;

        foreach (var peerId in peerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ImportPeerInventoryAsync(peerId);
            var peer = _peerRegistry.GetPeer(result?.PeerId ?? peerId);
            if (ShouldBackoffPeerRefresh(peer, result))
            {
                failureCount++;
            }
        }

        return failureCount;
    }

    public bool RemoveRemoteInstance(string instanceId)
    {
        var removed = false;
        string peerId = string.Empty;
        var removePeerRegistration = false;
        lock (_lock)
        {
            EnsureRoutingStateUnsafe();
            var remote = FindRemoteInstanceUnsafe(instanceId);
            if (remote is null)
            {
                return false;
            }

            peerId = remote.PeerId;
            _state.RemoteInstances.Remove(remote);
            removePeerRegistration = !string.IsNullOrWhiteSpace(peerId) && !_state.RemoteInstances.Any(candidate =>
                string.Equals(candidate.PeerId, peerId, StringComparison.OrdinalIgnoreCase));
            foreach (var role in _state.Roles.Where(role => string.Equals(role.AssignedInstanceId, instanceId, StringComparison.OrdinalIgnoreCase)))
            {
                role.AssignedInstanceId = string.Empty;
                role.AssignedModelId = string.Empty;
            }

            PersistUnsafe();
            removed = true;
        }

        if (removed)
        {
            if (removePeerRegistration)
            {
                _peerRegistry.RemovePeer(peerId);
            }

            NotifyLibraryInventoryChanged();
        }

        return removed;
    }

    public async Task<LoadedInstanceSnapshot?> ActivateModelAsync(string modelId, string primaryMode)
    {
        var entry = _catalog.FindById(modelId);
        if (entry is null)
        {
            return null;
        }

        if (!entry.IsRunnable || !entry.ChatCapable)
        {
            _logs.Append("warn", "model-library", $"Activation blocked because model '{modelId}' is not runnable.", modelId);
            return null;
        }

        LoadedInstanceSnapshot? snapshot;
        var normalizedMode = NormalizeMode(primaryMode, entry);
        var instanceAction = "reused";
        var instanceId = string.Empty;
        lock (_lock)
        {
            var existing = FindReusableInstanceUnsafe(entry.ModelId, normalizedMode);
            if (existing is null && entry.MaxLoadedInstances > 0 && _state.Instances.Count(instance => string.Equals(instance.ModelId, entry.ModelId, StringComparison.OrdinalIgnoreCase)) >= entry.MaxLoadedInstances)
            {
                _logs.Append("warn", "model-library", $"Activation blocked because model '{modelId}' is already at max loaded instances {entry.MaxLoadedInstances}.", modelId);
                return null;
            }

            if (existing is null)
            {
                existing = new LibraryInstanceState
                {
                    InstanceId = $"inst-{Guid.NewGuid():N}"[..17],
                    ModelId = entry.ModelId,
                    DisplayName = entry.DisplayName,
                    PrimaryMode = normalizedMode,
                    Capabilities = DetectCapabilities(entry),
                    MemoryReservedMb = DefaultEndpointMemory(entry, normalizedMode),
                    MemoryActualMb = Math.Max(1, entry.EstimatedMemoryMb),
                    MaxTokens = 1200,
                    HostedPort = AllocateHostedPortUnsafe(),
                    AllowLanAccess = false
                };
                _state.Instances.Add(existing);
                instanceAction = "created";
            }

            existing.DisplayName = entry.DisplayName;
            existing.ModelId = entry.ModelId;
            existing.PrimaryMode = normalizedMode;
            existing.Capabilities = DetectCapabilities(entry);
            existing.LoadState = "resident";
            existing.IsActive = true;
            existing.LastLoadedUtc = DateTimeOffset.UtcNow;
            existing.MemoryActualMb = Math.Max(1, entry.EstimatedMemoryMb);
            existing.MemoryReservedMb = ClampEndpointMemory(existing.MemoryReservedMb > 0 ? existing.MemoryReservedMb : DefaultEndpointMemory(entry, existing.PrimaryMode));
            AppendConsoleUnsafe(existing, "system", $"Activated for /{existing.PrimaryMode}.");
            PersistUnsafe();
            snapshot = BuildInstanceSnapshotUnsafe(existing);
            instanceId = existing.InstanceId;
        }

        _logs.Append("info", "model-library", $"Activation request {instanceAction} instance {instanceId} for model '{modelId}' in mode '{normalizedMode}'.", modelId);
        NotifyLibraryInventoryChanged();
        NotifyInstanceChanged(snapshot!.InstanceId);
        NotifyChatInstancesChanged();
        return snapshot;
    }

    public async Task<LoadedInstanceSnapshot?> PreloadModelAsync(string modelId)
    {
        var entry = _catalog.FindById(modelId);
        if (entry is null)
        {
            return null;
        }

        string instanceId;
        var instanceAction = "reused";
        lock (_lock)
        {
            var instance = FindReusableInstanceUnsafe(entry.ModelId, "thinking");
            if (instance is null && entry.MaxLoadedInstances > 0 && _state.Instances.Count(existing => string.Equals(existing.ModelId, entry.ModelId, StringComparison.OrdinalIgnoreCase)) >= entry.MaxLoadedInstances)
            {
                _logs.Append("warn", "model-library", $"Preload blocked because model '{modelId}' is already at max loaded instances {entry.MaxLoadedInstances}.", modelId);
                return null;
            }

            if (instance is null)
            {
                instance = new LibraryInstanceState
                {
                    InstanceId = $"inst-{Guid.NewGuid():N}"[..17],
                    ModelId = entry.ModelId,
                    DisplayName = entry.DisplayName,
                    PrimaryMode = "thinking",
                    Capabilities = DetectCapabilities(entry),
                    MemoryReservedMb = DefaultEndpointMemory(entry, "thinking"),
                    MemoryActualMb = Math.Max(1, entry.EstimatedMemoryMb),
                    MaxTokens = 1200,
                    HostedPort = AllocateHostedPortUnsafe(),
                    AllowLanAccess = false
                };
                _state.Instances.Add(instance);
                instanceAction = "created";
            }
            instanceId = instance.InstanceId;

            instance.DisplayName = entry.DisplayName;
            instance.ModelId = entry.ModelId;
            instance.PrimaryMode = "thinking";
            instance.Capabilities = DetectCapabilities(entry);
            instance.MemoryActualMb = Math.Max(1, entry.EstimatedMemoryMb);
            instance.MemoryReservedMb = ClampEndpointMemory(instance.MemoryReservedMb > 0 ? instance.MemoryReservedMb : DefaultEndpointMemory(entry, "thinking"));
            instance.LoadState = "loading";
            AppendConsoleUnsafe(instance, "system", "Preload requested.");
            PersistUnsafe();
        }

        NotifyLibraryInventoryChanged();
        NotifyInstanceChanged(instanceId);
        _logs.Append("info", "model-library", $"Preloading {instanceAction} instance {instanceId} for model '{modelId}'.", modelId);

        if (!entry.IsRunnable || !entry.ChatCapable)
        {
            lock (_lock)
            {
                var instance = FindInstanceUnsafe(instanceId);
                if (instance is not null)
                {
                    instance.LoadState = "unavailable";
                    AppendConsoleUnsafe(instance, "error", "Preload blocked because model is not runnable.");
                    PersistUnsafe();
                }
            }

            NotifyLibraryInventoryChanged();
            NotifyInstanceChanged(instanceId);
            return FindInstance(instanceId);
        }

        LoadedInstanceSnapshot? completedSnapshot;
        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return null;
            }

            instance.LoadState = "resident";
            instance.LastLoadedUtc = DateTimeOffset.UtcNow;
            AppendConsoleUnsafe(instance, "system", "Preload completed.");
            PersistUnsafe();
            completedSnapshot = BuildInstanceSnapshotUnsafe(instance);
        }

        NotifyLibraryInventoryChanged();
        NotifyInstanceChanged(completedSnapshot!.InstanceId);
        NotifyChatInstancesChanged();
        return completedSnapshot;
    }

    public LoadedInstanceSnapshot? DeactivateInstance(string instanceId)
    {
        LoadedInstanceSnapshot? snapshot;
        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return null;
            }

            instance.IsActive = false;
            instance.LoadState = "resident";
            AppendConsoleUnsafe(instance, "system", "Instance deactivated.");
            PersistUnsafe();
            snapshot = BuildInstanceSnapshotUnsafe(instance);
        }

        NotifyLibraryInventoryChanged();
        NotifyInstanceChanged(snapshot!.InstanceId);
        NotifyChatInstancesChanged();
        return snapshot;
    }

    public LoadedInstanceSnapshot? SetInstanceActive(string instanceId, bool isActive)
    {
        LoadedInstanceSnapshot? snapshot;
        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return null;
            }

            instance.IsActive = isActive;
            instance.LoadState = "resident";
            instance.LastLoadedUtc = DateTimeOffset.UtcNow;
            AppendConsoleUnsafe(instance, "system", isActive ? $"Activated for /{instance.PrimaryMode}." : "Instance deactivated.");
            PersistUnsafe();
            snapshot = BuildInstanceSnapshotUnsafe(instance);
        }

        NotifyLibraryInventoryChanged();
        NotifyInstanceChanged(snapshot!.InstanceId);
        NotifyChatInstancesChanged();
        return snapshot;
    }

    public async Task<LoadedInstanceSnapshot?> ReloadInstanceAsync(string instanceId)
    {
        LoadedInstanceSnapshot? snapshot;
        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return null;
            }

            instance.LoadState = "loading";
            AppendConsoleUnsafe(instance, "system", "Reload requested.");
            PersistUnsafe();
        }

        NotifyLibraryInventoryChanged();
        NotifyInstanceChanged(instanceId);

        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return null;
            }

            instance.LoadState = "resident";
            instance.IsActive = true;
            instance.LastLoadedUtc = DateTimeOffset.UtcNow;
            AppendConsoleUnsafe(instance, "system", "Reload completed.");
            PersistUnsafe();
            snapshot = BuildInstanceSnapshotUnsafe(instance);
        }

        NotifyLibraryInventoryChanged();
        NotifyInstanceChanged(snapshot!.InstanceId);
        NotifyChatInstancesChanged();
        return snapshot;
    }

    public LoadedInstanceSnapshot? DeleteInstance(string instanceId)
    {
        var removed = false;
        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return null;
            }

            _state.Instances.Remove(instance);
            PersistUnsafe();
            removed = true;
        }

        if (removed)
        {
            NotifyLibraryInventoryChanged();
            NotifyChatInstancesChanged();
        }

        return null;
    }

    public LoadedInstanceSnapshot? PinInstance(string instanceId, bool pinned)
    {
        LoadedInstanceSnapshot? snapshot;
        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return null;
            }

            instance.IsPinned = pinned;
            AppendConsoleUnsafe(instance, "system", pinned ? "Pinned instance." : "Unpinned instance.");
            PersistUnsafe();
            snapshot = BuildInstanceSnapshotUnsafe(instance);
        }

        NotifyLibraryInventoryChanged();
        NotifyInstanceChanged(snapshot!.InstanceId);
        NotifyChatInstancesChanged();
        return snapshot;
    }

    public LoadedInstanceSnapshot? UpdateInstanceMode(string instanceId, string primaryMode)
    {
        LoadedInstanceSnapshot? snapshot;
        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return null;
            }

            var entry = _catalog.FindById(instance.ModelId);
            if (entry is null)
            {
                return null;
            }

            instance.PrimaryMode = NormalizeMode(primaryMode, entry);
            instance.MemoryReservedMb = ClampEndpointMemory(instance.MemoryReservedMb > 0 ? instance.MemoryReservedMb : DefaultEndpointMemory(entry, instance.PrimaryMode));
            AppendConsoleUnsafe(instance, "system", $"Primary mode changed to {instance.PrimaryMode}.");
            PersistUnsafe();
            snapshot = BuildInstanceSnapshotUnsafe(instance);
        }

        NotifyLibraryInventoryChanged();
        NotifyInstanceChanged(snapshot!.InstanceId);
        NotifyChatInstancesChanged();
        return snapshot;
    }

    public LoadedInstanceSnapshot? UpdateInstanceTokens(string instanceId, int maxTokens)
    {
        LoadedInstanceSnapshot? snapshot;
        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return null;
            }

            instance.MaxTokens = Math.Clamp(maxTokens, 100, 5000);
            PersistUnsafe();
            snapshot = BuildInstanceSnapshotUnsafe(instance);
        }

        NotifyInstanceChanged(snapshot!.InstanceId);
        return snapshot;
    }

    public LoadedInstanceSnapshot? UpdateInstanceMemoryCapacity(string instanceId, long memoryCapacityMb)
    {
        LoadedInstanceSnapshot? snapshot;
        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return null;
            }

            instance.MemoryReservedMb = ClampEndpointMemory(memoryCapacityMb);
            PersistUnsafe();
            snapshot = BuildInstanceSnapshotUnsafe(instance);
        }

        NotifyLibraryInventoryChanged();
        NotifyInstanceChanged(snapshot!.InstanceId);
        NotifyChatInstancesChanged();
        return snapshot;
    }

    public void AppendConsoleEvent(string? instanceId, string source, string text)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lock (_lock)
        {
            var instance = FindInstanceUnsafe(instanceId);
            if (instance is null)
            {
                return;
            }

            AppendConsoleUnsafe(instance, source, text);
            PersistUnsafe();
        }

        NotifyInstanceConsoleChanged(instanceId);
    }

    public LoadedInstanceSnapshot? ResolveInstanceForEndpoint(string endpointName, string? explicitInstanceId = null, string? explicitModelId = null)
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(explicitInstanceId))
            {
                var explicitInstance = FindInstanceUnsafe(explicitInstanceId);
                return explicitInstance is null ? null : BuildInstanceSnapshotUnsafe(explicitInstance);
            }

            if (!string.IsNullOrWhiteSpace(explicitModelId))
            {
                var byModel = _state.Instances
                    .Where(instance => string.Equals(instance.ModelId, explicitModelId, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(instance => instance.IsPinned)
                    .ThenByDescending(instance => instance.LastLoadedUtc)
                    .FirstOrDefault();
                return byModel is null ? null : BuildInstanceSnapshotUnsafe(byModel);
            }

            if (string.Equals(endpointName, "direct_chat", StringComparison.OrdinalIgnoreCase))
            {
                var direct = ResolveDirectChatAutoInstanceUnsafe();
                return direct is null ? null : BuildInstanceSnapshotUnsafe(direct);
            }

            var resolved = ResolveInstanceUnsafe(endpointName);
            return resolved is null ? null : BuildInstanceSnapshotUnsafe(resolved);
        }
    }

    public RouteExecutionTarget? ResolveExecutionTarget(string endpointName, string? explicitInstanceId = null, string? explicitModelId = null)
    {
        return ResolveExecutionTargets(endpointName, explicitInstanceId, explicitModelId).FirstOrDefault();
    }

    public IReadOnlyList<RouteExecutionTarget> ResolveExecutionTargets(string endpointName, string? explicitInstanceId = null, string? explicitModelId = null)
    {
        lock (_lock)
        {
            EnsureRoutingStateUnsafe();

            if (!string.IsNullOrWhiteSpace(explicitInstanceId))
            {
                if (FindInstanceUnsafe(explicitInstanceId) is { } explicitLocal)
                {
                    return new[] { BuildExecutionTargetUnsafe(explicitLocal, endpointName) };
                }

                if (FindRemoteInstanceUnsafe(explicitInstanceId) is { } explicitRemote)
                {
                    return new[] { BuildExecutionTargetUnsafe(explicitRemote, endpointName) };
                }

                return Array.Empty<RouteExecutionTarget>();
            }

            if (!string.IsNullOrWhiteSpace(explicitModelId))
            {
                var localMatches = _state.Instances
                    .Where(instance => string.Equals(instance.ModelId, explicitModelId, StringComparison.OrdinalIgnoreCase))
                    .Where(IsReadyTextInstanceUnsafe)
                    .OrderByDescending(instance => instance.IsPinned)
                    .ThenBy(instance => instance.LastRoutedUtc ?? DateTimeOffset.MinValue)
                    .ThenByDescending(instance => instance.LastLoadedUtc)
                    .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(instance => BuildExecutionTargetUnsafe(instance, endpointName));

                var remoteMatches = _state.RemoteInstances
                    .Where(remote => string.Equals(remote.ModelId, explicitModelId, StringComparison.OrdinalIgnoreCase))
                    .Where(remote => IsRemoteTargetReady(remote, GetEffectiveRemoteStatus(remote)))
                    .OrderBy(remote => remote.LastRoutedUtc ?? DateTimeOffset.MinValue)
                    .ThenBy(remote => remote.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(remote => BuildExecutionTargetUnsafe(remote, endpointName));

                return localMatches.Concat(remoteMatches).ToList();
            }

            if (string.Equals(endpointName, "direct_chat", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveDirectChatAutoInstancesUnsafe()
                    .Select(instance => BuildExecutionTargetUnsafe(instance, endpointName))
                    .ToList();
            }

            var targets = new List<RouteExecutionTarget>();
            var configuredRole = _state.Roles.FirstOrDefault(role => string.Equals(role.RoleName, endpointName, StringComparison.OrdinalIgnoreCase));
            if (configuredRole is not null && !string.IsNullOrWhiteSpace(configuredRole.AssignedInstanceId))
            {
                if (FindInstanceUnsafe(configuredRole.AssignedInstanceId) is { } assignedLocal)
                {
                    targets.Add(BuildExecutionTargetUnsafe(assignedLocal, endpointName));
                }

                if (FindRemoteInstanceUnsafe(configuredRole.AssignedInstanceId) is { } assignedRemote)
                {
                    targets.Add(BuildExecutionTargetUnsafe(assignedRemote, endpointName));
                }
            }

            targets.AddRange(ResolveInstancesUnsafe(endpointName).Select(instance => BuildExecutionTargetUnsafe(instance, endpointName)));
            return targets
                .GroupBy(target => target.RouteTargetId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }
    }

    public void MarkRouteTargetUsed(string? routeTargetId)
    {
        if (string.IsNullOrWhiteSpace(routeTargetId))
        {
            return;
        }

        lock (_lock)
        {
            var timestamp = DateTimeOffset.UtcNow;
            if (FindInstanceUnsafe(routeTargetId) is { } local)
            {
                local.LastRoutedUtc = timestamp;
                PersistUnsafe();
                return;
            }

            if (FindRemoteInstanceUnsafe(routeTargetId) is { } remote)
            {
                remote.LastRoutedUtc = timestamp;
                PersistUnsafe();
            }
        }
    }

    public ModelLibraryItemSnapshot? PinModel(string modelId, bool pinned)
    {
        lock (_lock)
        {
            var affected = false;
            foreach (var instance in _state.Instances.Where(instance => string.Equals(instance.ModelId, modelId, StringComparison.OrdinalIgnoreCase)))
            {
                instance.IsPinned = pinned;
                affected = true;
            }

            if (!affected)
            {
                return null;
            }

            PersistUnsafe();
        }

        NotifyLibraryInventoryChanged();
        NotifyChatInstancesChanged();
        return GetLibraryItems().FirstOrDefault(item => string.Equals(item.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<RuntimeRoleSnapshot?> AssignModelToRoleAsync(string roleName, string modelId)
    {
        var instance = await ActivateModelAsync(modelId, roleName);
        if (instance is null)
        {
            return null;
        }

        return UpdateRoleSettings(roleName, modelId, roleName, null, null, null, instance.InstanceId);
    }

    public RuntimeRoleSnapshot? UpdateRoleSettings(string roleName, string? modelId, string? routingMode, int? reservedMemory, int? maxParallel, int? queueLimit, string? instanceId = null)
    {
        LoadedInstanceSnapshot? instance = null;
        RemoteInstanceConnectionState? remoteInstance = null;
        RoutingRoleState? roleState;
        lock (_lock)
        {
            EnsureRoutingStateUnsafe();
            roleState = _state.Roles.FirstOrDefault(role => string.Equals(role.RoleName, roleName, StringComparison.OrdinalIgnoreCase));
            if (roleState is null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                roleState.AssignedInstanceId = instanceId.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(modelId))
            {
                var resolved = ResolveInstanceForEndpoint(roleName, explicitModelId: modelId);
                roleState.AssignedInstanceId = resolved?.InstanceId ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(routingMode))
            {
                roleState.RoutingMode = routingMode.Trim();
            }

            if (reservedMemory.HasValue)
            {
                roleState.ReservedMemory = ClampEndpointMemory(reservedMemory.Value);
            }

            if (maxParallel.HasValue)
            {
                roleState.MaxParallel = Math.Max(1, maxParallel.Value);
            }

            if (queueLimit.HasValue)
            {
                roleState.QueueLimit = Math.Max(1, queueLimit.Value);
            }

            instance = !string.IsNullOrWhiteSpace(roleState.AssignedInstanceId)
                ? FindInstanceUnsafe(roleState.AssignedInstanceId) is { } assignedLocal ? BuildInstanceSnapshotUnsafe(assignedLocal) : null
                : null;

            remoteInstance = instance is null && !string.IsNullOrWhiteSpace(roleState.AssignedInstanceId)
                ? FindRemoteInstanceUnsafe(roleState.AssignedInstanceId)
                : null;

            roleState.AssignedModelId = instance?.ModelId ?? remoteInstance?.ModelId ?? string.Empty;
            PersistUnsafe();
        }

        if (instance is not null)
        {
            if (!string.IsNullOrWhiteSpace(routingMode))
            {
                _ = UpdateInstanceMode(instance.InstanceId, routingMode!);
            }

            if (reservedMemory.HasValue)
            {
                lock (_lock)
                {
                    var editable = FindInstanceUnsafe(instance.InstanceId);
                    if (editable is not null)
                    {
                        editable.MemoryReservedMb = ClampEndpointMemory(reservedMemory.Value);
                        PersistUnsafe();
                    }
                }
                NotifyInstanceChanged(instance.InstanceId);
            }
        }

        NotifyLibraryInventoryChanged();
        return GetRoles().FirstOrDefault(role => string.Equals(role.RoleName, roleName, StringComparison.OrdinalIgnoreCase));
    }

    private void Load()
    {
        if (File.Exists(_paths.LibraryStatePath))
        {
            try
            {
                _state = JsonSerializer.Deserialize<LibraryStateDocument>(File.ReadAllText(_paths.LibraryStatePath)) ?? new LibraryStateDocument();
            }
            catch
            {
                _state = new LibraryStateDocument();
            }
        }

        NormalizeRoutingStateUnsafe();
        NormalizeConsoleHistoryUnsafe();
        PersistUnsafe();
    }

    private void ReconcileWithCatalog()
    {
        var noLoadedInstances = false;
        lock (_lock)
        {
            var validModelIds = _catalog.GetEntries().Select(entry => entry.ModelId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _state.Instances.RemoveAll(instance => !validModelIds.Contains(instance.ModelId));
            NormalizeRoutingStateUnsafe();
            PersistUnsafe();
            noLoadedInstances = !_state.Instances.Any(instance =>
                string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(instance.LoadState, "loading", StringComparison.OrdinalIgnoreCase));
        }

        if (noLoadedInstances)
        {
            _logs.Append("info", "model-library", "Catalog reconciled with zero loaded instances.", "reconcile");
        }

        NotifyLibraryInventoryChanged();
        NotifyChatInstancesChanged();
    }

    private async Task<bool> VerifyBeforeActivationAsync(ModelCatalogEntry entry)
    {
        if (!entry.IsRunnable || !entry.ChatCapable)
        {
            return false;
        }

        var verified = _catalog.VerifyModel(entry.ModelId);
        if (verified is not null)
        {
            return verified.IsRunnable;
        }

        await Task.CompletedTask;
        return false;
    }

    private LibraryInstanceState? FindReusableInstanceUnsafe(string modelId, string primaryMode)
    {
        var normalizedMode = string.IsNullOrWhiteSpace(primaryMode) ? "thinking" : primaryMode.Trim().ToLowerInvariant();
        return _state.Instances
            .Where(instance => string.Equals(instance.ModelId, modelId, StringComparison.OrdinalIgnoreCase))
            .Where(instance => string.Equals(instance.PrimaryMode, normalizedMode, StringComparison.OrdinalIgnoreCase))
            .Where(instance => !string.Equals(instance.LoadState, "unavailable", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(instance => instance.IsPinned)
            .ThenByDescending(instance => instance.IsActive)
            .ThenByDescending(instance => GetReusePriority(instance))
            .ThenByDescending(instance => instance.LastLoadedUtc)
            .FirstOrDefault();
    }

    private static int GetReusePriority(LibraryInstanceState instance)
    {
        if (string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (string.Equals(instance.LoadState, "loading", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }

    private LoadedInstanceSnapshot BuildInstanceSnapshotUnsafe(LibraryInstanceState instance)
    {
        var entry = _catalog.FindById(instance.ModelId);
        return new LoadedInstanceSnapshot(
            instance.InstanceId,
            instance.ModelId,
            instance.DisplayName,
            entry?.LocalPath ?? string.Empty,
            entry?.RuntimeBackend ?? string.Empty,
            instance.LoadState,
            instance.IsActive,
            instance.IsPinned,
            instance.PrimaryMode,
            instance.Capabilities.ToList(),
            instance.MemoryReservedMb,
            instance.MemoryActualMb,
            instance.MaxTokens,
            instance.AllowLanAccess,
            instance.HostedPort,
            instance.HostedStatus,
            instance.HostedStatusMessage,
            entry?.HealthStatus ?? string.Empty,
            entry?.HealthReason ?? string.Empty,
            instance.LastLoadedUtc,
            instance.ConsoleEntries.Select(entryItem => new InstanceConsoleEntrySnapshot(entryItem.TimestampUtc, entryItem.Source, entryItem.Text)).ToList(),
            GetFamily(instance.DisplayName));
    }

    private string GetLibraryLoadState(IEnumerable<LibraryInstanceState> instances)
    {
        if (instances.Any(instance => string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase)))
        {
            return "resident";
        }

        if (instances.Any(instance => string.Equals(instance.LoadState, "loading", StringComparison.OrdinalIgnoreCase)))
        {
            return "loading";
        }

        if (instances.Any(instance => string.Equals(instance.LoadState, "inactive", StringComparison.OrdinalIgnoreCase)))
        {
            return "inactive";
        }

        return "unloaded";
    }

    private static string GetFamily(string displayName)
    {
        var index = displayName.IndexOfAny(new[] { '-', '_', '.' });
        return index > 0 ? displayName[..index] : displayName;
    }

    private static List<string> DetectCapabilities(ModelCatalogEntry entry)
    {
        var capabilities = new List<string> { "text" };
        var lowerName = $"{entry.DisplayName} {entry.Filename}".ToLowerInvariant();
        if (lowerName.Contains("vision", StringComparison.OrdinalIgnoreCase))
        {
            capabilities.Add("vision");
        }

        if (lowerName.Contains("audio", StringComparison.OrdinalIgnoreCase) || lowerName.Contains("speech", StringComparison.OrdinalIgnoreCase))
        {
            capabilities.Add("audio");
        }

        return capabilities;
    }

    private static int DefaultReservedMemory(string mode) =>
        string.Equals(mode, "instruct", StringComparison.OrdinalIgnoreCase) ? 2048 : 1024;

    private static long DefaultEndpointMemory(ModelCatalogEntry entry, string mode)
    {
        var preferred = entry.EstimatedMemoryMb > 0 ? entry.EstimatedMemoryMb : DefaultReservedMemory(mode);
        return ClampEndpointMemory(preferred);
    }

    private static long ClampEndpointMemory(long value) =>
        Math.Clamp(value, MinEndpointMemoryMb, MaxEndpointMemoryMb);

    private LibraryInstanceState? ResolveDirectChatAutoInstanceUnsafe()
    {
        return ResolveDirectChatAutoInstancesUnsafe().FirstOrDefault();
    }

    private IReadOnlyList<LibraryInstanceState> ResolveDirectChatAutoInstancesUnsafe()
    {
        EnsureRoutingStateUnsafe();

        return _state.Instances
            .Where(IsReadyTextInstanceUnsafe)
            .Select(instance => new
            {
                Instance = instance,
                Catalog = _catalog.FindById(instance.ModelId),
                AssignedRoles = GetAssignedRoleNamesUnsafe(instance.InstanceId)
            })
            .Where(item => item.Catalog?.IsRunnable != false)
            .Select(item => new
            {
                item.Instance,
                DirectTier = ClassifyDirectChatTier(item.Catalog),
                IsInstructPreferred = string.Equals(item.Instance.PrimaryMode, "instruct", StringComparison.OrdinalIgnoreCase)
                    || item.AssignedRoles.Contains("instruct", StringComparer.OrdinalIgnoreCase),
                item.Instance.IsPinned,
                item.Instance.LastRoutedUtc,
                item.Instance.LastLoadedUtc,
                item.Instance.DisplayName
            })
            .OrderBy(item => item.DirectTier)
            .ThenByDescending(item => item.IsInstructPreferred)
            .ThenByDescending(item => item.IsPinned)
            .ThenBy(item => item.LastRoutedUtc ?? DateTimeOffset.MinValue)
            .ThenByDescending(item => item.LastLoadedUtc)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Instance)
            .ToList();
    }

    private static int ClassifyDirectChatTier(ModelCatalogEntry? entry)
    {
        if (entry is null)
        {
            return 1;
        }

        if (string.Equals(entry.RuntimeBackend, "bitnet.cpp", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (!IsQwen35Model(entry))
        {
            return 1;
        }

        return IsConstrainedQwen35(entry) ? 2 : 0;
    }

    private static bool IsQwen35Model(ModelCatalogEntry entry)
    {
        var haystack = $"{entry.ModelId} {entry.DisplayName} {entry.Filename}".ToLowerInvariant();
        return haystack.Contains("qwen3.5", StringComparison.OrdinalIgnoreCase)
               || haystack.Contains("qwen35", StringComparison.OrdinalIgnoreCase)
               || haystack.Contains("qwen3_5", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConstrainedQwen35(ModelCatalogEntry entry)
    {
        var haystack = $" {entry.ModelId} {entry.DisplayName} {entry.Filename} {entry.Quantization} ".ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ");

        if (haystack.Contains(" iq2 ", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains(" iq2x ", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains(" iq2xs ", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains(" iq3 ", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains(" iq3xxs ", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains(" iq3xs ", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains(" q2 ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return haystack.Contains("0.5b", StringComparison.OrdinalIgnoreCase)
               || haystack.Contains("0.6b", StringComparison.OrdinalIgnoreCase)
               || haystack.Contains("0.8b", StringComparison.OrdinalIgnoreCase)
               || haystack.Contains("1.5b", StringComparison.OrdinalIgnoreCase)
               || haystack.Contains("1.7b", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMode(string requestedMode, ModelCatalogEntry entry)
    {
        var normalized = string.IsNullOrWhiteSpace(requestedMode) ? "thinking" : requestedMode.Trim().ToLowerInvariant();
        var allowed = new[] { "thinking", "instruct" };
        return allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase) ? normalized : "thinking";
    }

    private void EnsureRoutingStateUnsafe() => NormalizeRoutingStateUnsafe();

    private LibraryInstanceState? ResolveInstanceUnsafe(string endpointName)
    {
        return ResolveInstancesUnsafe(endpointName).FirstOrDefault();
    }

    private IReadOnlyList<LibraryInstanceState> ResolveInstancesUnsafe(string endpointName)
    {
        EnsureRoutingStateUnsafe();
        var resolved = new List<LibraryInstanceState>();

        var configuredRole = _state.Roles.FirstOrDefault(role => string.Equals(role.RoleName, endpointName, StringComparison.OrdinalIgnoreCase));
        if (configuredRole is not null && !string.IsNullOrWhiteSpace(configuredRole.AssignedInstanceId))
        {
            var assigned = FindInstanceUnsafe(configuredRole.AssignedInstanceId);
            if (assigned is not null &&
                assigned.IsActive &&
                string.Equals(assigned.LoadState, "resident", StringComparison.OrdinalIgnoreCase))
            {
                resolved.Add(assigned);
            }
        }

        resolved.AddRange(_state.Instances
            .Where(instance =>
                instance.IsActive &&
                string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(instance.PrimaryMode, endpointName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(instance => instance.IsPinned)
            .ThenBy(instance => instance.LastRoutedUtc ?? DateTimeOffset.MinValue)
            .ThenByDescending(instance => instance.LastLoadedUtc)
            .ThenBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase));

        return resolved
            .GroupBy(instance => instance.InstanceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static bool IsReadyTextInstanceUnsafe(LibraryInstanceState instance) =>
        instance.IsActive &&
        string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase) &&
        instance.Capabilities.Contains("text", StringComparer.OrdinalIgnoreCase);

    private IEnumerable<LibraryInstanceState> GetInstancesForModelUnsafe(string modelId) =>
        _state.Instances.Where(instance => string.Equals(instance.ModelId, modelId, StringComparison.OrdinalIgnoreCase));

    private LibraryInstanceState? FindInstanceUnsafe(string instanceId) =>
        _state.Instances.FirstOrDefault(instance => string.Equals(instance.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));

    private RemoteInstanceConnectionState? FindRemoteInstanceUnsafe(string instanceId) =>
        _state.RemoteInstances.FirstOrDefault(instance => string.Equals(instance.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));

    private LibraryInstanceState? FindInstanceUnsafeByModelId(string modelId) =>
        _state.Instances.LastOrDefault(instance => string.Equals(instance.ModelId, modelId, StringComparison.OrdinalIgnoreCase));

    private LoadedInstanceSnapshot? FindInstanceByModelId(string modelId)
    {
        lock (_lock)
        {
            var instance = FindInstanceUnsafeByModelId(modelId);
            return instance is null ? null : BuildInstanceSnapshotUnsafe(instance);
        }
    }

    private static void AppendConsoleUnsafe(LibraryInstanceState instance, string source, string text)
    {
        var last = instance.ConsoleEntries.LastOrDefault();
        if (last is not null &&
            string.Equals(last.Source, source, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(last.Text, text, StringComparison.Ordinal))
        {
            last.TimestampUtc = DateTimeOffset.UtcNow;
            return;
        }

        instance.ConsoleEntries.Add(new LibraryInstanceConsoleEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Source = source,
            Text = text
        });

        if (instance.ConsoleEntries.Count > 40)
        {
            instance.ConsoleEntries.RemoveRange(0, instance.ConsoleEntries.Count - 40);
        }
    }

    private void NormalizeConsoleHistoryUnsafe()
    {
        foreach (var instance in _state.Instances)
        {
            instance.ConsoleEntries = instance.ConsoleEntries
                .Where(entry => !entry.Text.StartsWith("Token budget set to", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (instance.ConsoleEntries.Count <= 1)
            {
                continue;
            }

            var normalized = new List<LibraryInstanceConsoleEntry>();
            foreach (var entry in instance.ConsoleEntries)
            {
                var last = normalized.LastOrDefault();
                if (last is not null &&
                    string.Equals(last.Source, entry.Source, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(last.Text, entry.Text, StringComparison.Ordinal))
                {
                    last.TimestampUtc = entry.TimestampUtc;
                    continue;
                }

                normalized.Add(entry);
            }

            instance.ConsoleEntries = normalized.TakeLast(40).ToList();
        }
    }

    private void NormalizeRoutingStateUnsafe()
    {
        EnsureRoleStatesUnsafe();

        var usedPorts = new HashSet<int>();
        foreach (var instance in _state.Instances)
        {
            instance.HostedPort = NormalizeHostedPortUnsafe(instance.HostedPort <= 0 ? AllocateHostedPortUnsafe() : instance.HostedPort, instance.InstanceId, usedPorts);
            usedPorts.Add(instance.HostedPort);
        }

        foreach (var remote in _state.RemoteInstances)
        {
            remote.PeerId = remote.PeerId?.Trim() ?? string.Empty;
            remote.DisplayName = string.IsNullOrWhiteSpace(remote.DisplayName) ? $"{remote.Host}:{remote.Port}" : remote.DisplayName.Trim();
            remote.Host = remote.Host?.Trim() ?? string.Empty;
            remote.Port = remote.Port <= 0 ? 3005 : remote.Port;
            remote.Status = string.IsNullOrWhiteSpace(remote.Status) ? "unknown" : remote.Status;
            remote.RemoteInstanceId = remote.RemoteInstanceId?.Trim() ?? string.Empty;
            remote.ModelId = remote.ModelId?.Trim() ?? string.Empty;
            remote.RuntimeBackend = remote.RuntimeBackend?.Trim() ?? string.Empty;
            remote.PrimaryMode = remote.PrimaryMode?.Trim() ?? string.Empty;
            remote.Family = remote.Family?.Trim() ?? string.Empty;
            remote.HealthStatus = remote.HealthStatus?.Trim() ?? string.Empty;
            remote.PeerToken = remote.PeerToken?.Trim() ?? string.Empty;
            remote.Capabilities ??= new List<string>();
            remote.RemoteRoleNames ??= new List<string>();
        }

        var validRoleIds = _state.Instances.Select(instance => instance.InstanceId)
            .Concat(_state.RemoteInstances.Select(remote => remote.InstanceId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var role in _state.Roles)
        {
            if (!string.IsNullOrWhiteSpace(role.AssignedInstanceId) && !validRoleIds.Contains(role.AssignedInstanceId))
            {
                role.AssignedInstanceId = string.Empty;
                role.AssignedModelId = string.Empty;
            }

            role.RoleName = role.RoleName.Trim().ToLowerInvariant();
            role.RoutingMode = string.IsNullOrWhiteSpace(role.RoutingMode) ? role.RoleName : role.RoutingMode.Trim().ToLowerInvariant();
            role.MaxParallel = Math.Max(1, role.MaxParallel);
            role.QueueLimit = Math.Max(1, role.QueueLimit);
        }
    }

    private void EnsureRoleStatesUnsafe()
    {
        var roleNames = new[] { "instruct", "thinking", "audio", "vision" };
        foreach (var roleName in roleNames)
        {
            var existing = _state.Roles.FirstOrDefault(role => string.Equals(role.RoleName, roleName, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                var fallback = _state.Instances
                    .Where(instance => instance.IsActive && string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase) && string.Equals(instance.PrimaryMode, roleName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(instance => instance.IsPinned)
                    .ThenByDescending(instance => instance.LastLoadedUtc)
                    .FirstOrDefault();
                _state.Roles.Add(new RoutingRoleState
                {
                    RoleName = roleName,
                    AssignedInstanceId = fallback?.InstanceId ?? string.Empty,
                    AssignedModelId = fallback?.ModelId ?? string.Empty,
                    RoutingMode = roleName,
                    ReservedMemory = fallback?.MemoryReservedMb ?? DefaultReservedMemory(roleName),
                    MaxParallel = 1,
                    QueueLimit = 4
                });
            }
        }

        _state.Roles = _state.Roles
            .Where(role => roleNames.Contains(role.RoleName, StringComparer.OrdinalIgnoreCase))
            .OrderBy(role => Array.IndexOf(roleNames, role.RoleName))
            .ToList();
    }

    private RuntimeRoleSnapshot BuildRoleSnapshotUnsafe(RoutingRoleState roleState)
    {
        var local = string.IsNullOrWhiteSpace(roleState.AssignedInstanceId) ? null : FindInstanceUnsafe(roleState.AssignedInstanceId);
        var remote = local is null && !string.IsNullOrWhiteSpace(roleState.AssignedInstanceId)
            ? FindRemoteInstanceUnsafe(roleState.AssignedInstanceId)
            : null;
        var remoteLiveStatus = remote is null ? string.Empty : GetEffectiveRemoteStatus(remote);

        var liveStatus = local is not null
            ? local.IsActive && string.Equals(local.LoadState, "resident", StringComparison.OrdinalIgnoreCase)
                ? "ready"
                : local.LoadState
            : remote is not null
                ? remoteLiveStatus
                : roleState.RoleName is "audio" or "vision"
                    ? "unavailable"
                    : "unassigned";

        return new RuntimeRoleSnapshot(
            roleState.RoleName,
            local?.InstanceId ?? remote?.InstanceId ?? string.Empty,
            local?.ModelId ?? remote?.ModelId ?? roleState.AssignedModelId,
            local?.DisplayName ?? remote?.DisplayName ?? string.Empty,
            string.IsNullOrWhiteSpace(roleState.RoutingMode) ? roleState.RoleName : roleState.RoutingMode,
            roleState.ReservedMemory > 0 ? roleState.ReservedMemory : local?.MemoryReservedMb ?? 0,
            Math.Max(1, roleState.MaxParallel),
            Math.Max(1, roleState.QueueLimit),
            liveStatus,
            remote is not null,
            local?.AllowLanAccess ?? remote?.AllowLanAccess ?? false,
            local?.HostedPort ?? remote?.Port ?? 0);
    }

    private RoutingInstanceSnapshot BuildRoutingInstanceSnapshotUnsafe(LibraryInstanceState instance, IReadOnlyList<string> assignedRoles)
    {
        var entry = _catalog.FindById(instance.ModelId);
        var statusText = instance.IsActive
            ? string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase) ? "ready" : instance.LoadState
            : "inactive";
        var baseHealthSummary = string.IsNullOrWhiteSpace(entry?.HealthReason)
            ? entry?.HealthStatus ?? instance.LoadState
            : $"{entry?.HealthStatus} | {entry?.HealthReason}";
        var networkSummary = instance.AllowLanAccess
            ? string.IsNullOrWhiteSpace(instance.HostedStatusMessage)
                ? $"Hosted on port {instance.HostedPort}"
                : instance.HostedStatusMessage
            : "LAN exposure off";
        return new RoutingInstanceSnapshot(
            instance.InstanceId,
            instance.DisplayName,
            false,
            "Local",
            $":{instance.HostedPort}",
            statusText,
            assignedRoles.Count == 0 ? "No route roles" : string.Join(", ", assignedRoles),
            instance.IsActive,
            instance.AllowLanAccess,
            instance.HostedPort,
            entry?.RuntimeBackend ?? string.Empty,
            string.Join(" | ", new[] { baseHealthSummary, networkSummary }.Where(value => !string.IsNullOrWhiteSpace(value))),
            instance.MemoryReservedMb,
            instance.MemoryActualMb,
            instance.PrimaryMode,
            string.Join(" | ", new[] { string.IsNullOrWhiteSpace(entry?.LocalPath) ? string.Empty : entry.LocalPath, networkSummary }.Where(value => !string.IsNullOrWhiteSpace(value))),
            true);
    }

    private RoutingInstanceSnapshot BuildRoutingInstanceSnapshotUnsafe(RemoteInstanceConnectionState remote, IReadOnlyList<string> assignedRoles)
    {
        var effectiveStatus = GetEffectiveRemoteStatus(remote);
        var isReachable = IsRemoteTargetReady(remote, effectiveStatus);
        var peer = string.IsNullOrWhiteSpace(remote.PeerId) ? null : _peerRegistry.GetPeer(remote.PeerId);
        var peerSummary = peer is null ? string.Empty : $"{peer.DisplayName} | {peer.PresentationState}";
        return new RoutingInstanceSnapshot(
            remote.InstanceId,
            remote.DisplayName,
            true,
            "LAN",
            $"{remote.Host}:{remote.Port}",
            effectiveStatus,
            assignedRoles.Count == 0 ? "Unassigned peer" : string.Join(", ", assignedRoles),
            isReachable,
            remote.AllowLanAccess,
            remote.Port,
            string.IsNullOrWhiteSpace(remote.RuntimeBackend) ? "remote nexus central" : remote.RuntimeBackend,
            string.Join(" | ", new[] { peerSummary, BuildRemoteHealthSummary(remote, effectiveStatus) }.Where(value => !string.IsNullOrWhiteSpace(value))),
            0,
            0,
            string.IsNullOrWhiteSpace(remote.PrimaryMode) ? string.Empty : remote.PrimaryMode,
            BuildRemoteDetailText(remote),
            isReachable);
    }

    private RouteExecutionTarget BuildExecutionTargetUnsafe(LibraryInstanceState instance, string endpointName)
    {
        var entry = _catalog.FindById(instance.ModelId);
        return new RouteExecutionTarget(
            instance.InstanceId,
            instance.InstanceId,
            instance.ModelId,
            instance.DisplayName,
            endpointName,
            false,
            "127.0.0.1",
            instance.HostedPort,
            entry?.RuntimeBackend ?? string.Empty,
            instance.PrimaryMode,
            entry?.LocalPath ?? string.Empty,
            instance.AllowLanAccess,
            instance.IsActive && string.Equals(instance.LoadState, "resident", StringComparison.OrdinalIgnoreCase),
            entry?.HealthStatus ?? instance.LoadState,
            entry?.HealthReason ?? string.Empty,
            instance.Capabilities.ToList(),
            entry is null ? string.Empty : GetFamily(entry.DisplayName),
            instance.MemoryReservedMb,
            string.Empty);
    }

    private RouteExecutionTarget BuildExecutionTargetUnsafe(RemoteInstanceConnectionState remote, string endpointName)
    {
        var effectiveStatus = GetEffectiveRemoteStatus(remote);
        var peer = string.IsNullOrWhiteSpace(remote.PeerId) ? null : _peerRegistry.GetPeer(remote.PeerId);
        return new RouteExecutionTarget(
            remote.InstanceId,
            remote.RemoteInstanceId,
            remote.ModelId,
            remote.DisplayName,
            endpointName,
            true,
            remote.Host,
            remote.Port,
            remote.RuntimeBackend,
            remote.PrimaryMode,
            string.Empty,
            remote.AllowLanAccess,
            IsRemoteTargetReady(remote, effectiveStatus),
            string.IsNullOrWhiteSpace(remote.HealthStatus) ? effectiveStatus : remote.HealthStatus,
            BuildRemoteHealthSummary(remote, effectiveStatus),
            remote.Capabilities.ToList(),
            remote.Family,
            0,
            peer?.SharedSecret ?? remote.PeerToken);
    }

    private ImportedRouteTargetSnapshot BuildImportedRouteTargetSnapshotUnsafe(RemoteInstanceConnectionState remote, IReadOnlyList<string> assignedRoles)
    {
        var effectiveStatus = GetEffectiveRemoteStatus(remote);
        var blockedReason = IsRemoteTargetReady(remote, effectiveStatus)
            ? string.Empty
            : string.IsNullOrWhiteSpace(remote.StatusMessage)
                ? BuildRemoteHealthSummary(remote, effectiveStatus)
                : remote.StatusMessage;
        return new ImportedRouteTargetSnapshot(
            remote.InstanceId,
            remote.PeerId,
            remote.RemoteInstanceId,
            remote.ModelId,
            remote.DisplayName,
            effectiveStatus,
            remote.AllowLanAccess,
            IsRemoteTargetReady(remote, effectiveStatus),
            remote.IsCached || string.Equals(effectiveStatus, "cached", StringComparison.OrdinalIgnoreCase),
            string.IsNullOrWhiteSpace(remote.PrimaryMode) ? string.Empty : remote.PrimaryMode,
            string.IsNullOrWhiteSpace(remote.HealthStatus) ? effectiveStatus : remote.HealthStatus,
            blockedReason,
            assignedRoles,
            remote.RemoteRoleNames.ToList(),
            remote.Capabilities.ToList(),
            remote.LastHeartbeatUtc,
            remote.LastImportUtc);
    }

    private static string BuildRemoteDetailText(RemoteInstanceConnectionState remote)
    {
        var parts = new List<string>
        {
            $"Peer control plane {remote.Host}:{remote.Port}"
        };

        if (!string.IsNullOrWhiteSpace(remote.PeerId))
        {
            parts.Add($"peer {remote.PeerId}");
        }

        if (!string.IsNullOrWhiteSpace(remote.ModelId))
        {
            parts.Add($"model {remote.ModelId}");
        }

        if (!string.IsNullOrWhiteSpace(remote.RemoteInstanceId))
        {
            parts.Add($"instance {remote.RemoteInstanceId}");
        }

        parts.Add(FormatHeartbeatText(remote.LastHeartbeatUtc));

        return string.Join(" | ", parts);
    }

    private static string BuildRemoteHealthSummary(RemoteInstanceConnectionState remote, string effectiveStatus)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(remote.StatusMessage))
        {
            parts.Add(remote.StatusMessage);
        }
        else if (!string.IsNullOrWhiteSpace(remote.HealthStatus))
        {
            parts.Add(remote.HealthStatus);
        }

        if (string.Equals(effectiveStatus, "stale", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("Peer heartbeat is stale.");
        }

        parts.Add(FormatHeartbeatText(remote.LastHeartbeatUtc));
        return string.Join(" | ", parts.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private LanPeerSnapshot? SynchronizePeerAfterPeerProbe(LanPeerSnapshot peer, RemoteProbeResult probe, bool isSelfPeer)
    {
        var currentPeerId = peer.PeerId;
        var resolvedDisplayName = string.IsNullOrWhiteSpace(probe.DisplayName) ? peer.DisplayName : probe.DisplayName;
        if (isSelfPeer)
        {
            _peerRegistry.MarkPeerOffline(currentPeerId, "Remote peer id matches this machine. Use the local instance directly instead of routing back through LAN.");
            return _peerRegistry.GetPeer(currentPeerId);
        }

        var updated = _peerRegistry.UpdatePeerObservation(currentPeerId, resolvedDisplayName, peer.LastKnownHost, peer.ControlPlanePort, probe.IsReachable ? DateTimeOffset.UtcNow : null)
            ?? peer;
        if (probe.IsReachable && !string.IsNullOrWhiteSpace(probe.PeerId))
        {
            var priorPeerId = updated.PeerId;
            updated = _peerRegistry.AdoptObservedPeerId(updated.PeerId, probe.PeerId) ?? updated;
            if (!string.Equals(priorPeerId, updated.PeerId, StringComparison.OrdinalIgnoreCase))
            {
                lock (_lock)
                {
                    MoveImportedTargetsToPeerIdUnsafe(priorPeerId, updated.PeerId);
                    PersistUnsafe();
                }
            }
        }

        updated = _peerRegistry.UpdatePeerObservation(updated.PeerId, resolvedDisplayName, peer.LastKnownHost, peer.ControlPlanePort, probe.IsReachable ? DateTimeOffset.UtcNow : null)
            ?? updated;
        if (probe.IsReachable)
        {
            updated = _peerRegistry.MarkPeerConnected(updated.PeerId, DateTimeOffset.UtcNow) ?? updated;
            if (!string.IsNullOrWhiteSpace(peer.SharedSecret))
            {
                updated = _peerRegistry.MarkPeerTrusted(updated.PeerId, peer.SharedSecret) ?? updated;
            }

            return updated;
        }

        return IsAuthenticationFailure(probe.Message)
            ? _peerRegistry.MarkPeerAuthFailed(updated.PeerId, probe.Message)
            : _peerRegistry.MarkPeerOffline(updated.PeerId, probe.Message);
    }

    private bool IsLocalPeerId(string? peerId)
    {
        return !string.IsNullOrWhiteSpace(peerId) && string.Equals(peerId.Trim(), _lanRouting.GetSnapshot().LocalPeerId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAuthenticationFailure(string message)
    {
        return message.StartsWith("HTTP 401", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("HTTP 403", StringComparison.OrdinalIgnoreCase);
    }

    private void MoveImportedTargetsToPeerIdUnsafe(string priorPeerId, string updatedPeerId)
    {
        foreach (var remote in _state.RemoteInstances.Where(item => string.Equals(item.PeerId, priorPeerId, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var oldRouteTargetId = remote.InstanceId;
            remote.PeerId = updatedPeerId;
            remote.InstanceId = BuildImportedTargetId(updatedPeerId, ExtractTargetTail(remote.RemoteInstanceId, oldRouteTargetId));
            foreach (var role in _state.Roles.Where(role => string.Equals(role.AssignedInstanceId, oldRouteTargetId, StringComparison.OrdinalIgnoreCase)))
            {
                role.AssignedInstanceId = remote.InstanceId;
            }
        }
    }

    private void MarkPeerTargetsUnavailableUnsafe(string peerId, string status, string? statusMessage)
    {
        foreach (var remote in _state.RemoteInstances.Where(item => string.Equals(item.PeerId, peerId, StringComparison.OrdinalIgnoreCase)))
        {
            remote.Status = status;
            remote.StatusMessage = string.IsNullOrWhiteSpace(statusMessage) ? remote.StatusMessage : statusMessage.Trim();
            remote.IsCached = true;
        }
    }

    private string GetEffectiveRemoteStatus(RemoteInstanceConnectionState remote)
    {
        var peer = string.IsNullOrWhiteSpace(remote.PeerId) ? null : _peerRegistry.GetPeer(remote.PeerId);
        if (peer?.IsAuthFailed == true)
        {
            return "auth_failed";
        }

        if (peer?.IsStale == true)
        {
            return "stale";
        }

        if (peer?.IsOffline == true && remote.IsCached)
        {
            return "offline";
        }

        if (string.Equals(remote.Status, "connected", StringComparison.OrdinalIgnoreCase))
        {
            if (IsRemotePeerStale(remote.LastHeartbeatUtc))
            {
                return "stale";
            }

            return "ready";
        }

        if (string.Equals(remote.Status, "partial", StringComparison.OrdinalIgnoreCase))
        {
            if (IsRemotePeerStale(remote.LastHeartbeatUtc))
            {
                return "stale";
            }

            return "partial";
        }

        if (string.Equals(remote.Status, "cached", StringComparison.OrdinalIgnoreCase))
        {
            return peer?.IsConnected == true ? "cached" : "offline";
        }

        return string.IsNullOrWhiteSpace(remote.Status) ? "unknown" : remote.Status;
    }

    private static bool IsRemotePeerStale(DateTimeOffset? lastHeartbeatUtc)
    {
        return lastHeartbeatUtc.HasValue && DateTimeOffset.UtcNow - lastHeartbeatUtc.Value > RemotePeerStaleAfter;
    }

    private static bool IsRemoteTargetReady(RemoteInstanceConnectionState remote, string effectiveStatus)
    {
        return remote.AllowLanAccess && (string.Equals(effectiveStatus, "ready", StringComparison.OrdinalIgnoreCase) || string.Equals(effectiveStatus, "partial", StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatHeartbeatText(DateTimeOffset? lastHeartbeatUtc)
    {
        if (!lastHeartbeatUtc.HasValue)
        {
            return "No heartbeat yet.";
        }

        var elapsed = DateTimeOffset.UtcNow - lastHeartbeatUtc.Value;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return $"Last seen {Math.Max(1, (int)elapsed.TotalSeconds)}s ago.";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"Last seen {Math.Max(1, (int)elapsed.TotalMinutes)}m ago.";
        }

        return $"Last seen {Math.Max(1, (int)elapsed.TotalHours)}h ago.";
    }

    private void MergeImportedTargetsUnsafe(
        LanPeerSnapshot peer,
        RemoteProbeResult probe,
        IReadOnlyList<RemoteInventoryItem> inventory,
        IReadOnlyDictionary<string, IReadOnlyList<string>> remoteRolesByInstanceId,
        bool inventorySucceeded,
        bool? allowLanAccessOverride,
        DateTimeOffset importUtc)
    {
        var peerTargets = _state.RemoteInstances
            .Where(remote => string.Equals(remote.PeerId, peer.PeerId, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(remote => remote.InstanceId, StringComparer.OrdinalIgnoreCase);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importItems = inventory.ToList();

        if (importItems.Count == 0 && !string.IsNullOrWhiteSpace(probe.InstanceId))
        {
            importItems.Add(new RemoteInventoryItem(
                probe.InstanceId,
                probe.ModelId,
                probe.DisplayName,
                probe.Family,
                "partial",
                true,
                probe.PrimaryMode,
                probe.Capabilities?.ToList() ?? new List<string>(),
                probe.HealthStatus,
                true));
        }

        foreach (var item in importItems)
        {
            var routeTargetId = BuildImportedTargetId(peer.PeerId, item.RemoteInstanceId);
            if (!peerTargets.TryGetValue(routeTargetId, out var remote))
            {
                remote = new RemoteInstanceConnectionState
                {
                    InstanceId = routeTargetId,
                    PeerId = peer.PeerId,
                    AllowLanAccess = allowLanAccessOverride ?? item.AllowLanAccess
                };
                _state.RemoteInstances.Add(remote);
            }

            remote.PeerId = peer.PeerId;
            remote.RemoteInstanceId = item.RemoteInstanceId;
            remote.ModelId = item.ModelId;
            remote.DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? routeTargetId : item.DisplayName;
            remote.Host = peer.LastKnownHost;
            remote.Port = peer.ControlPlanePort;
            remote.AllowLanAccess = (allowLanAccessOverride ?? remote.AllowLanAccess) && item.AllowLanAccess;
            remote.Status = inventorySucceeded ? "connected" : "partial";
            remote.StatusMessage = inventorySucceeded
                ? BuildInventoryStatusMessage(item)
                : "Peer status succeeded but imported runtime inventory is incomplete.";
            remote.RuntimeBackend = string.IsNullOrWhiteSpace(probe.RuntimeBackend) ? remote.RuntimeBackend : probe.RuntimeBackend;
            remote.PrimaryMode = string.IsNullOrWhiteSpace(item.PrimaryMode) ? probe.PrimaryMode : item.PrimaryMode;
            remote.Family = string.IsNullOrWhiteSpace(item.Family) ? probe.Family : item.Family;
            remote.HealthStatus = string.IsNullOrWhiteSpace(item.HealthStatus) ? probe.HealthStatus : item.HealthStatus;
            remote.PeerToken = peer.SharedSecret;
            remote.Capabilities = item.Capabilities.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            remote.LastHeartbeatUtc = importUtc;
            remote.LastImportUtc = importUtc;
            remote.IsCached = !inventorySucceeded;
            remote.RemoteRoleNames = remoteRolesByInstanceId.TryGetValue(item.RemoteInstanceId, out var remoteRoles)
                ? remoteRoles.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();
            seenIds.Add(routeTargetId);
        }

        foreach (var stale in _state.RemoteInstances.Where(remote =>
                     string.Equals(remote.PeerId, peer.PeerId, StringComparison.OrdinalIgnoreCase) &&
                     !seenIds.Contains(remote.InstanceId)))
        {
            stale.Host = peer.LastKnownHost;
            stale.Port = peer.ControlPlanePort;
            stale.PeerToken = peer.SharedSecret;
            stale.Status = "cached";
            stale.StatusMessage = inventorySucceeded
                ? "Showing cached imported target because it did not appear in the latest peer inventory."
                : "Showing cached imported target while peer inventory is incomplete.";
            stale.IsCached = true;
        }
    }

    private PeerImportResult BuildPeerImportResult(string peerId)
    {
        var targets = GetImportedTargets(peerId);
        var primaryTarget = targets.Count == 0
            ? null
            : GetRoutingInstances().FirstOrDefault(snapshot => string.Equals(snapshot.InstanceId, targets[0].RouteTargetId, StringComparison.OrdinalIgnoreCase));
        return new PeerImportResult(peerId, targets, primaryTarget);
    }

    private static string BuildImportedTargetId(string peerId, string remoteInstanceId)
    {
        var normalizedRemoteId = string.IsNullOrWhiteSpace(remoteInstanceId) ? "status" : remoteInstanceId.Trim();
        return $"{peerId}/{normalizedRemoteId}";
    }

    private static string ExtractTargetTail(string remoteInstanceId, string routeTargetId)
    {
        if (!string.IsNullOrWhiteSpace(remoteInstanceId))
        {
            return remoteInstanceId.Trim();
        }

        var slashIndex = routeTargetId.IndexOf('/');
        return slashIndex >= 0 && slashIndex < routeTargetId.Length - 1
            ? routeTargetId[(slashIndex + 1)..]
            : routeTargetId;
    }

    private async Task<RemoteInventoryFetchResult> FetchRemoteInventoryAsync(string host, int port, string? peerToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}:{port}/nexuscentral/get_runtime_inventory");
            if (!string.IsNullOrWhiteSpace(peerToken))
            {
                request.Headers.TryAddWithoutValidation(LanRoutingSettingsService.AuthHeaderName, peerToken.Trim());
            }

            using var response = await RoutingHttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new RemoteInventoryFetchResult(false, Array.Empty<RemoteInventoryItem>(), $"HTTP {(int)response.StatusCode}");
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var payload = document.RootElement.TryGetProperty("data", out var dataElement) ? dataElement : document.RootElement;
            if (!payload.TryGetProperty("inventory", out var inventoryElement) || inventoryElement.ValueKind != JsonValueKind.Array)
            {
                return new RemoteInventoryFetchResult(true, Array.Empty<RemoteInventoryItem>(), string.Empty);
            }

            var inventory = inventoryElement.EnumerateArray()
                .Select(item => new RemoteInventoryItem(
                    item.TryGetProperty("instance_id", out var instanceIdElement) ? instanceIdElement.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("model_id", out var modelIdElement) ? modelIdElement.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("display_name", out var displayNameElement) ? displayNameElement.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("family", out var familyElement) ? familyElement.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("load_state", out var loadStateElement) ? loadStateElement.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("is_active", out var isActiveElement) && isActiveElement.ValueKind is JsonValueKind.True,
                    item.TryGetProperty("primary_mode", out var modeElement) ? modeElement.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("capabilities", out var capabilitiesElement) && capabilitiesElement.ValueKind == JsonValueKind.Array
                        ? capabilitiesElement.EnumerateArray().Select(value => value.GetString() ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).ToList()
                        : new List<string>(),
                    item.TryGetProperty("health_status", out var healthElement) ? healthElement.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("allow_lan_access", out var allowElement) && allowElement.ValueKind is JsonValueKind.True))
                .Where(item => !string.IsNullOrWhiteSpace(item.RemoteInstanceId))
                .ToList();

            return new RemoteInventoryFetchResult(true, inventory, string.Empty);
        }
        catch (Exception ex)
        {
            return new RemoteInventoryFetchResult(false, Array.Empty<RemoteInventoryItem>(), ex.Message);
        }
    }

    private async Task<RemoteRolesFetchResult> FetchRemoteRolesAsync(string host, int port, string? peerToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}:{port}/nexuscentral/get_runtime_roles");
            if (!string.IsNullOrWhiteSpace(peerToken))
            {
                request.Headers.TryAddWithoutValidation(LanRoutingSettingsService.AuthHeaderName, peerToken.Trim());
            }

            using var response = await RoutingHttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new RemoteRolesFetchResult(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var payload = document.RootElement.TryGetProperty("data", out var dataElement) ? dataElement : document.RootElement;
            if (!payload.TryGetProperty("roles", out var rolesElement) || rolesElement.ValueKind != JsonValueKind.Array)
            {
                return new RemoteRolesFetchResult(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
            }

            var assignments = rolesElement.EnumerateArray()
                .Select(item => new
                {
                    RoleName = item.TryGetProperty("role", out var roleElement) ? roleElement.GetString() ?? string.Empty : string.Empty,
                    AssignedInstanceId = item.TryGetProperty("assigned_instance_id", out var assignedElement) ? assignedElement.GetString() ?? string.Empty : string.Empty
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.RoleName) && !string.IsNullOrWhiteSpace(item.AssignedInstanceId))
                .GroupBy(item => item.AssignedInstanceId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group.Select(item => item.RoleName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            return new RemoteRolesFetchResult(assignments);
        }
        catch
        {
            return new RemoteRolesFetchResult(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private static string BuildInventoryStatusMessage(RemoteInventoryItem item)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.LoadState))
        {
            parts.Add(item.LoadState);
        }

        if (!item.IsActive)
        {
            parts.Add("inactive");
        }

        if (!string.IsNullOrWhiteSpace(item.HealthStatus))
        {
            parts.Add(item.HealthStatus);
        }

        return string.Join(" | ", parts.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private async Task MonitorRemotePeersAsync(CancellationToken cancellationToken)
    {
        var failureStreak = 0;
        var nextDelay = RemotePeerHeartbeatInterval;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(nextDelay, cancellationToken);
                var failureCount = await TryRefreshAllRemoteInstancesAsync(cancellationToken);
                failureStreak = failureCount > 0
                    ? Math.Min(failureStreak + 1, 6)
                    : 0;
                nextDelay = GetRemoteRefreshDelay(failureStreak);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logs.Append("warning", "router", $"Remote peer monitor stopped. {ex.Message}");
        }
    }

    private async Task<int> TryRefreshAllRemoteInstancesAsync(CancellationToken cancellationToken)
    {
        if (!await _remoteRefreshLock.WaitAsync(0, cancellationToken))
        {
            return 0;
        }

        try
        {
            return await RefreshAllRemoteInstancesCoreAsync(cancellationToken);
        }
        finally
        {
            _remoteRefreshLock.Release();
        }
    }

    private static bool ShouldBackoffPeerRefresh(LanPeerSnapshot? peer, PeerImportResult? result)
    {
        if (result is null || peer is null)
        {
            return true;
        }

        return peer.IsOffline || peer.IsAuthFailed || peer.IsPartialImport;
    }

    private static TimeSpan GetRemoteRefreshDelay(int failureStreak)
    {
        if (failureStreak <= 0)
        {
            return RemotePeerHeartbeatInterval;
        }

        var delaySeconds = RemotePeerHeartbeatInterval.TotalSeconds * Math.Pow(2, failureStreak);
        return TimeSpan.FromSeconds(Math.Min(RemotePeerMaxRefreshBackoff.TotalSeconds, delaySeconds));
    }

    private IReadOnlyList<string> GetAssignedRoleNamesUnsafe(string instanceId)
    {
        return _state.Roles
            .Where(role => string.Equals(role.AssignedInstanceId, instanceId, StringComparison.OrdinalIgnoreCase))
            .Select(role => role.RoleName)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int AllocateHostedPortUnsafe()
    {
        var usedPorts = _state.Instances.Select(instance => instance.HostedPort).Where(port => port > 0).ToHashSet();
        var candidate = 3100;
        while (usedPorts.Contains(candidate))
        {
            candidate++;
        }

        return candidate;
    }

    private int NormalizeHostedPortUnsafe(int requestedPort, string? currentInstanceId = null, HashSet<int>? usedPorts = null)
    {
        var candidate = Math.Clamp(requestedPort, 3005, 65000);
        var collisions = usedPorts ?? _state.Instances
            .Where(instance => !string.Equals(instance.InstanceId, currentInstanceId, StringComparison.OrdinalIgnoreCase))
            .Select(instance => instance.HostedPort)
            .Where(port => port > 0)
            .ToHashSet();

        while (collisions.Contains(candidate))
        {
            candidate++;
        }

        return candidate;
    }

    private async Task<RemoteProbeResult> ProbeRemoteStatusAsync(string host, int port, string? peerToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}:{port}/nexuscentral/status");
            if (!string.IsNullOrWhiteSpace(peerToken))
            {
                request.Headers.TryAddWithoutValidation(LanRoutingSettingsService.AuthHeaderName, peerToken.Trim());
            }

            using var response = await RoutingHttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new RemoteProbeResult(false, $"HTTP {(int)response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
            {
                return new RemoteProbeResult(true, "Remote Nexus Central responded.");
            }

            using var document = JsonDocument.Parse(content);
            var payload = document.RootElement.TryGetProperty("data", out var dataElement)
                ? dataElement
                : document.RootElement.TryGetProperty("payload", out var payloadElement)
                    ? payloadElement
                    : document.RootElement;
            var statusText = payload.TryGetProperty("status_text", out var statusElement)
                ? statusElement.GetString() ?? string.Empty
                : string.Empty;
            var summaryText = payload.TryGetProperty("summary_text", out var summaryElement)
                ? summaryElement.GetString() ?? string.Empty
                : string.Empty;
            var message = string.Join(" | ", new[] { statusText, summaryText }.Where(value => !string.IsNullOrWhiteSpace(value)));
            var instanceId = payload.TryGetProperty("instance_id", out var instanceIdElement)
                ? instanceIdElement.GetString() ?? string.Empty
                : string.Empty;
            var modelId = payload.TryGetProperty("model_id", out var modelIdElement)
                ? modelIdElement.GetString() ?? string.Empty
                : string.Empty;
            var displayName = payload.TryGetProperty("display_name", out var displayNameElement)
                ? displayNameElement.GetString() ?? string.Empty
                : string.Empty;
            var peerId = payload.TryGetProperty("local_peer_id", out var peerIdElement)
                ? peerIdElement.GetString() ?? string.Empty
                : string.Empty;
            var runtimeBackend = payload.TryGetProperty("runtime_backend", out var backendElement)
                ? backendElement.GetString() ?? string.Empty
                : string.Empty;
            var primaryMode = payload.TryGetProperty("primary_mode", out var primaryModeElement)
                ? primaryModeElement.GetString() ?? string.Empty
                : string.Empty;
            var family = payload.TryGetProperty("family", out var familyElement)
                ? familyElement.GetString() ?? string.Empty
                : string.Empty;
            var healthStatus = payload.TryGetProperty("health_status", out var healthElement)
                ? healthElement.GetString() ?? string.Empty
                : string.Empty;
            var capabilities = payload.TryGetProperty("capabilities", out var capabilitiesElement) && capabilitiesElement.ValueKind == JsonValueKind.Array
                ? capabilitiesElement.EnumerateArray().Select(element => element.GetString() ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).ToList()
                : new List<string>();
            return new RemoteProbeResult(
                true,
                string.IsNullOrWhiteSpace(message) ? "Remote Nexus Central responded." : message,
                instanceId,
                modelId,
                string.IsNullOrWhiteSpace(displayName) ? $"{host}:{port}" : displayName,
                runtimeBackend,
                primaryMode,
                family,
                healthStatus,
                capabilities,
                peerId);
        }
        catch (Exception ex)
        {
            return new RemoteProbeResult(false, ex.Message);
        }
    }

    private void PersistUnsafe()
    {
        File.WriteAllText(_paths.LibraryStatePath, JsonSerializer.Serialize(_state, _jsonOptions));
    }

    private void NotifyLibraryInventoryChanged()
    {
        LibraryInventoryChanged?.Invoke();
        Changed?.Invoke();
    }

    private void NotifyInstanceChanged(string instanceId)
    {
        InstanceChanged?.Invoke(instanceId);
        Changed?.Invoke();
    }

    private void NotifyInstanceConsoleChanged(string instanceId)
    {
        InstanceConsoleChanged?.Invoke(instanceId);
    }

    private void NotifyChatInstancesChanged()
    {
        ChatInstancesChanged?.Invoke();
    }

    public void Dispose()
    {
        _lifecycleCts.Cancel();
        _lifecycleCts.Dispose();
        _remoteRefreshLock.Dispose();
    }
}

public record ModelLibraryItemSnapshot(
    string ModelId,
    string DisplayName,
    string LocalPath,
    long FileSizeBytes,
    string HealthStatus,
    string LoadState,
    bool IsLoaded,
    bool IsPinned,
    string RuntimeBackend,
    bool IsRunnable,
    string HealthReason,
    IReadOnlyList<string> AssignedEndpoints,
    string Family,
    int InstanceCount);

public record LoadedInstanceSnapshot(
    string InstanceId,
    string ModelId,
    string DisplayName,
    string LocalPath,
    string RuntimeBackend,
    string LoadState,
    bool IsActive,
    bool IsPinned,
    string PrimaryMode,
    IReadOnlyList<string> Capabilities,
    long MemoryReservedMb,
    long MemoryActualMb,
    int MaxTokens,
    bool AllowLanAccess,
    int HostedPort,
    string HostedStatus,
    string HostedStatusMessage,
    string HealthStatus,
    string HealthReason,
    DateTimeOffset? LastLoadedUtc,
    IReadOnlyList<InstanceConsoleEntrySnapshot> ConsoleEntries,
    string Family);

public record InstanceConsoleEntrySnapshot(DateTimeOffset TimestampUtc, string Source, string Text);

public record RuntimeRoleSnapshot(
    string RoleName,
    string AssignedInstanceId,
    string AssignedModelId,
    string AssignedModelDisplayName,
    string RoutingMode,
    long ReservedMemory,
    int MaxParallel,
    int QueueLimit,
    string LiveStatus,
    bool IsRemoteTarget,
    bool AllowLanAccess,
    int HostedPort);

public record RuntimeInventorySnapshot(
    string InstanceId,
    string ModelId,
    string ModelName,
    string Family,
    string State,
    string Residency,
    long ReservedMemory,
    long ActualMemory,
    int CurrentRequests,
    int QueueDepth,
    string Roles,
    string Reason);

public record ImportedRouteTargetSnapshot(
    string RouteTargetId,
    string PeerId,
    string RemoteInstanceId,
    string ModelId,
    string DisplayName,
    string StatusText,
    bool AllowLanAccess,
    bool IsReady,
    bool IsCached,
    string PrimaryMode,
    string HealthStatus,
    string BlockedReason,
    IReadOnlyList<string> AssignedRoles,
    IReadOnlyList<string> RemoteRoles,
    IReadOnlyList<string> Capabilities,
    DateTimeOffset? LastHeartbeatUtc,
    DateTimeOffset? LastImportUtc);

public record PeerImportResult(
    string PeerId,
    IReadOnlyList<ImportedRouteTargetSnapshot> Targets,
    RoutingInstanceSnapshot? PrimaryTarget);

public record RouteExecutionTarget(
    string RouteTargetId,
    string InstanceId,
    string ModelId,
    string DisplayName,
    string EndpointName,
    bool IsRemote,
    string Host,
    int Port,
    string RuntimeBackend,
    string PrimaryMode,
    string LocalPath,
    bool AllowLanAccess,
    bool IsReady,
    string HealthStatus,
    string HealthReason,
    IReadOnlyList<string> Capabilities,
    string Family,
    long MemoryReservedMb,
    string SharedSecret);

public class LibraryStateDocument
{
    public List<LibraryInstanceState> Instances { get; set; } = new();
    public List<RoutingRoleState> Roles { get; set; } = new();
    public List<RemoteInstanceConnectionState> RemoteInstances { get; set; } = new();
}

public class LibraryInstanceState
{
    public string InstanceId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LoadState { get; set; } = "unloaded";
    public bool IsActive { get; set; }
    public string PrimaryMode { get; set; } = "thinking";
    public List<string> Capabilities { get; set; } = new();
    public long MemoryReservedMb { get; set; }
    public long MemoryActualMb { get; set; }
    public int MaxTokens { get; set; } = 1200;
    public bool IsPinned { get; set; }
    public bool AllowLanAccess { get; set; }
    public int HostedPort { get; set; }
    public string HostedStatus { get; set; } = "disabled";
    public string HostedStatusMessage { get; set; } = string.Empty;
    public DateTimeOffset? LastLoadedUtc { get; set; }
    public DateTimeOffset? LastRoutedUtc { get; set; }
    public List<LibraryInstanceConsoleEntry> ConsoleEntries { get; set; } = new();
}

public class RoutingRoleState
{
    public string RoleName { get; set; } = string.Empty;
    public string AssignedInstanceId { get; set; } = string.Empty;
    public string AssignedModelId { get; set; } = string.Empty;
    public string RoutingMode { get; set; } = string.Empty;
    public long ReservedMemory { get; set; }
    public int MaxParallel { get; set; } = 1;
    public int QueueLimit { get; set; } = 4;
}

public class RemoteInstanceConnectionState
{
    public string InstanceId { get; set; } = string.Empty;
    public string PeerId { get; set; } = string.Empty;
    public string RemoteInstanceId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 3005;
    public bool AllowLanAccess { get; set; } = true;
    public string Status { get; set; } = "unknown";
    public string StatusMessage { get; set; } = string.Empty;
    public string RuntimeBackend { get; set; } = string.Empty;
    public string PrimaryMode { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string HealthStatus { get; set; } = string.Empty;
    public string PeerToken { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = new();
    public DateTimeOffset? LastHeartbeatUtc { get; set; }
    public DateTimeOffset? LastImportUtc { get; set; }
    public DateTimeOffset? LastRoutedUtc { get; set; }
    public bool IsCached { get; set; }
    public List<string> RemoteRoleNames { get; set; } = new();
}

public record RoutingInstanceSnapshot(
    string InstanceId,
    string DisplayName,
    bool IsRemote,
    string OriginLabel,
    string AddressText,
    string StatusText,
    string RolesSummary,
    bool IsEnabled,
    bool AllowLanAccess,
    int HostedPort,
    string RuntimeBackend,
    string HealthSummary,
    long ReservedMemory,
    long ActualMemory,
    string RoutingMode,
    string DetailText,
    bool IsReachable);

public record RemoteProbeResult(
    bool IsReachable,
    string Message,
    string InstanceId = "",
    string ModelId = "",
    string DisplayName = "",
    string RuntimeBackend = "",
    string PrimaryMode = "",
    string Family = "",
    string HealthStatus = "",
    IReadOnlyList<string>? Capabilities = null,
    string PeerId = "");

public record RemoteInventoryItem(
    string RemoteInstanceId,
    string ModelId,
    string DisplayName,
    string Family,
    string LoadState,
    bool IsActive,
    string PrimaryMode,
    IReadOnlyList<string> Capabilities,
    string HealthStatus,
    bool AllowLanAccess);

public record RemoteInventoryFetchResult(
    bool IsSuccess,
    IReadOnlyList<RemoteInventoryItem> Inventory,
    string ErrorMessage);

public record RemoteRolesFetchResult(
    IReadOnlyDictionary<string, IReadOnlyList<string>> RoleAssignments);

public class LibraryInstanceConsoleEntry
{
    public DateTimeOffset TimestampUtc { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
