using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NexusCentral_Avalonia.Services;

public class LanPeerRegistryService
{
    private static readonly TimeSpan PeerFreshFor = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan PeerStaleFor = TimeSpan.FromMinutes(5);

    private readonly AppPathsService _paths;
    private readonly LanRoutingSettingsService _lanRouting;
    private readonly LogService _logs;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private LanPeerRegistryDocument _document = new();

    public LanPeerRegistryService(AppPathsService paths, LanRoutingSettingsService lanRouting, LogService logs)
    {
        _paths = paths;
        _lanRouting = lanRouting;
        _logs = logs;
        Load();
    }

    public event Action? Changed;

    public LanPeerRegistrySnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return BuildSnapshotUnsafe();
        }
    }

    public IReadOnlyList<LanPeerSnapshot> GetPeers() => GetSnapshot().Peers;

    public LanPeerSnapshot? GetPeer(string peerId)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return null;
        }

        lock (_lock)
        {
            var peer = FindPeerUnsafe(peerId);
            return peer is null ? null : BuildPeerSnapshotUnsafe(peer);
        }
    }

    public LanPeerSnapshot UpsertManualPeer(string displayName, string host, int controlPlanePort, string? sharedSecret = null, bool isTrusted = true)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Host is required.", nameof(host));
        }

        LanPeerSnapshot snapshot;
        lock (_lock)
        {
            var normalizedHost = host.Trim();
            var peer = _document.Peers.FirstOrDefault(candidate =>
                string.Equals(candidate.LastKnownHost, normalizedHost, StringComparison.OrdinalIgnoreCase) &&
                candidate.ControlPlanePort == Math.Clamp(controlPlanePort, 1, 65535));

            if (peer is null)
            {
                peer = new LanPeerDocument
                {
                    PeerId = GeneratePeerId(),
                    SourceKind = "manual"
                };
                _document.Peers.Add(peer);
            }

            peer.SourceKind = MergeSourceKind(peer.SourceKind, "manual");
            peer.DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"{normalizedHost}:{controlPlanePort}" : displayName.Trim();
            peer.LastKnownHost = normalizedHost;
            peer.ControlPlanePort = Math.Clamp(controlPlanePort, 1, 65535);
            var normalizedSecret = sharedSecret?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedSecret))
            {
                peer.SharedSecret = normalizedSecret;
            }

            peer.TrustState = isTrusted || !string.IsNullOrWhiteSpace(peer.SharedSecret) ? "trusted" : "discovered";
            peer.LastSeenUtc ??= DateTimeOffset.UtcNow;
            PersistUnsafe();
            snapshot = BuildPeerSnapshotUnsafe(peer);
        }

        Changed?.Invoke();
        return snapshot;
    }

    public void ApplyDiscoveryObservations(IReadOnlyList<LanDiscoveryPeerObservation> observations)
    {
        lock (_lock)
        {
            foreach (var observation in observations.Where(item => !string.IsNullOrWhiteSpace(item.PeerId)))
            {
                if (string.Equals(observation.PeerId, _lanRouting.GetSnapshot().LocalPeerId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var peer = FindPeerUnsafe(observation.PeerId);
                if (peer is null)
                {
                    peer = _document.Peers.FirstOrDefault(candidate =>
                        !string.IsNullOrWhiteSpace(candidate.LastKnownHost) &&
                        string.Equals(candidate.LastKnownHost, observation.Host, StringComparison.OrdinalIgnoreCase) &&
                        candidate.ControlPlanePort == observation.ControlPlanePort);
                }

                if (peer is null)
                {
                    peer = new LanPeerDocument
                    {
                        PeerId = observation.PeerId.Trim(),
                        SourceKind = "discovered",
                        TrustState = "discovered"
                    };
                    _document.Peers.Add(peer);
                }
                else if (!string.Equals(peer.PeerId, observation.PeerId, StringComparison.OrdinalIgnoreCase))
                {
                    var existing = FindPeerUnsafe(observation.PeerId);
                    if (existing is not null && !ReferenceEquals(existing, peer))
                    {
                        MergePeerDocumentsUnsafe(existing, peer);
                        _document.Peers.Remove(peer);
                        peer = existing;
                    }
                    else
                    {
                        peer.PeerId = observation.PeerId.Trim();
                    }
                }

                peer.SourceKind = MergeSourceKind(peer.SourceKind, "discovered");
                peer.DisplayName = ChooseObservedDisplayName(peer.DisplayName, observation.DisplayName, observation.Host, observation.ControlPlanePort);
                peer.LastKnownHost = string.IsNullOrWhiteSpace(observation.Host) ? peer.LastKnownHost : observation.Host.Trim();
                peer.ControlPlanePort = observation.ControlPlanePort <= 0 ? peer.ControlPlanePort : observation.ControlPlanePort;
                peer.LastSeenUtc = observation.LastSeenUtc;
                peer.LastDiscoveryUtc = observation.LastSeenUtc;
                peer.DiscoveryServiceName = observation.ServiceName?.Trim() ?? peer.DiscoveryServiceName;
                peer.DiscoveryVersion = observation.Version?.Trim() ?? peer.DiscoveryVersion;
                if (string.IsNullOrWhiteSpace(peer.SharedSecret))
                {
                    peer.TrustState = "discovered";
                }
            }

            PersistUnsafe();
        }

        Changed?.Invoke();
    }

    public LanPeerSnapshot? AdoptObservedPeerId(string currentPeerId, string observedPeerId)
    {
        if (string.IsNullOrWhiteSpace(currentPeerId) || string.IsNullOrWhiteSpace(observedPeerId))
        {
            return null;
        }

        LanPeerSnapshot? snapshot;
        lock (_lock)
        {
            var current = FindPeerUnsafe(currentPeerId);
            if (current is null)
            {
                return null;
            }

            var normalizedObservedPeerId = observedPeerId.Trim();
            if (string.Equals(current.PeerId, normalizedObservedPeerId, StringComparison.OrdinalIgnoreCase))
            {
                return BuildPeerSnapshotUnsafe(current);
            }

            var existing = FindPeerUnsafe(normalizedObservedPeerId);
            if (existing is not null && !ReferenceEquals(existing, current))
            {
                MergePeerDocumentsUnsafe(existing, current);
                _document.Peers.Remove(current);
                snapshot = BuildPeerSnapshotUnsafe(existing);
            }
            else
            {
                current.PeerId = normalizedObservedPeerId;
                snapshot = BuildPeerSnapshotUnsafe(current);
            }

            PersistUnsafe();
        }

        Changed?.Invoke();
        return snapshot;
    }

    public LanPeerSnapshot? UpdatePeerObservation(string peerId, string? displayName = null, string? host = null, int? controlPlanePort = null, DateTimeOffset? lastSeenUtc = null)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return null;
        }

        LanPeerSnapshot? snapshot;
        lock (_lock)
        {
            var peer = FindPeerUnsafe(peerId);
            if (peer is null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                peer.DisplayName = displayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(host))
            {
                peer.LastKnownHost = host.Trim();
            }

            if (controlPlanePort.HasValue)
            {
                peer.ControlPlanePort = Math.Clamp(controlPlanePort.Value, 1, 65535);
            }

            if (lastSeenUtc.HasValue)
            {
                peer.LastSeenUtc = lastSeenUtc.Value;
            }

            PersistUnsafe();
            snapshot = BuildPeerSnapshotUnsafe(peer);
        }

        Changed?.Invoke();
        return snapshot;
    }

    public LanPeerSnapshot? MarkPeerTrusted(string peerId, string sharedSecret)
    {
        if (string.IsNullOrWhiteSpace(peerId) || string.IsNullOrWhiteSpace(sharedSecret))
        {
            return null;
        }

        LanPeerSnapshot? snapshot;
        lock (_lock)
        {
            var peer = FindPeerUnsafe(peerId);
            if (peer is null)
            {
                return null;
            }

            peer.SharedSecret = sharedSecret.Trim();
            peer.TrustState = "trusted";
            peer.LastAuthFailureReason = string.Empty;
            PersistUnsafe();
            snapshot = BuildPeerSnapshotUnsafe(peer);
        }

        Changed?.Invoke();
        return snapshot;
    }

    public LanPeerSnapshot? MarkPeerInspecting(string peerId)
    {
        return UpdatePeerImportState(peerId, "refreshing");
    }

    public LanPeerSnapshot? MarkPeerAuthFailed(string peerId, string reason)
    {
        return UpdatePeerTrustState(peerId, "auth_failed", reason);
    }

    public LanPeerSnapshot? MarkPeerOffline(string peerId, string? reason = null)
    {
        return UpdatePeerTrustState(peerId, "offline", reason);
    }

    public LanPeerSnapshot? MarkPeerConnected(string peerId, DateTimeOffset? lastSeenUtc = null)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return null;
        }

        LanPeerSnapshot? snapshot;
        lock (_lock)
        {
            var peer = FindPeerUnsafe(peerId);
            if (peer is null)
            {
                return null;
            }

            peer.TrustState = string.IsNullOrWhiteSpace(peer.SharedSecret) ? "discovered" : "trusted";
            peer.LastAuthFailureReason = string.Empty;
            if (lastSeenUtc.HasValue)
            {
                peer.LastSeenUtc = lastSeenUtc.Value;
            }

            PersistUnsafe();
            snapshot = BuildPeerSnapshotUnsafe(peer);
        }

        Changed?.Invoke();
        return snapshot;
    }

    public LanPeerSnapshot? UpdateCachedImportSummary(string peerId, int importedTargetCount, string summary, DateTimeOffset? lastSuccessfulImportUtc = null, string importState = "complete")
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return null;
        }

        LanPeerSnapshot? snapshot;
        lock (_lock)
        {
            var peer = FindPeerUnsafe(peerId);
            if (peer is null)
            {
                return null;
            }

            peer.CachedImportedTargetCount = Math.Max(0, importedTargetCount);
            peer.CachedInventorySummary = summary?.Trim() ?? string.Empty;
            peer.LastSuccessfulImportUtc = lastSuccessfulImportUtc ?? DateTimeOffset.UtcNow;
            peer.ImportState = NormalizeImportState(importState);
            peer.TrustState = string.IsNullOrWhiteSpace(peer.SharedSecret) ? "discovered" : "trusted";
            peer.LastAuthFailureReason = string.Empty;
            PersistUnsafe();
            snapshot = BuildPeerSnapshotUnsafe(peer);
        }

        Changed?.Invoke();
        return snapshot;
    }

    public LanPeerSnapshot? MarkImportPartial(string peerId, int importedTargetCount, string summary, DateTimeOffset? importUtc = null)
    {
        return UpdateCachedImportSummary(peerId, importedTargetCount, summary, importUtc, "partial");
    }

    public bool RemovePeer(string peerId)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return false;
        }

        var removed = false;
        lock (_lock)
        {
            var peer = FindPeerUnsafe(peerId);
            if (peer is null)
            {
                return false;
            }

            removed = _document.Peers.Remove(peer);
            if (removed)
            {
                PersistUnsafe();
            }
        }

        if (removed)
        {
            Changed?.Invoke();
        }

        return removed;
    }

    private LanPeerSnapshot? UpdatePeerTrustState(string peerId, string trustState, string? reason)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return null;
        }

        LanPeerSnapshot? snapshot;
        lock (_lock)
        {
            var peer = FindPeerUnsafe(peerId);
            if (peer is null)
            {
                return null;
            }

            peer.TrustState = trustState;
            peer.LastAuthFailureReason = reason?.Trim() ?? string.Empty;
            PersistUnsafe();
            snapshot = BuildPeerSnapshotUnsafe(peer);
        }

        Changed?.Invoke();
        return snapshot;
    }

    private LanPeerSnapshot? UpdatePeerImportState(string peerId, string importState)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return null;
        }

        LanPeerSnapshot? snapshot;
        lock (_lock)
        {
            var peer = FindPeerUnsafe(peerId);
            if (peer is null)
            {
                return null;
            }

            peer.ImportState = NormalizeImportState(importState);
            PersistUnsafe();
            snapshot = BuildPeerSnapshotUnsafe(peer);
        }

        Changed?.Invoke();
        return snapshot;
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_paths.LanPeerRegistryPath))
            {
                var parsed = JsonSerializer.Deserialize<LanPeerRegistryDocument>(File.ReadAllText(_paths.LanPeerRegistryPath));
                if (parsed is not null)
                {
                    _document = parsed;
                }
            }
        }
        catch (Exception ex)
        {
            _logs.Append("warning", "router", $"Could not load LAN peer registry. {ex.Message}");
            _document = new LanPeerRegistryDocument();
        }

        lock (_lock)
        {
            NormalizeDocumentUnsafe();
            PersistUnsafe();
        }
    }

    private LanPeerRegistrySnapshot BuildSnapshotUnsafe()
    {
        return new LanPeerRegistrySnapshot(
            _lanRouting.GetSnapshot().LocalPeerId,
            _document.Peers
                .Select(BuildPeerSnapshotUnsafe)
                .OrderByDescending(peer => peer.IsTrusted)
                .ThenByDescending(peer => peer.IsConnected)
                .ThenByDescending(peer => peer.LastSeenUtc)
                .ThenBy(peer => peer.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private LanPeerSnapshot BuildPeerSnapshotUnsafe(LanPeerDocument peer)
    {
        var normalizedTrustState = NormalizeTrustState(peer.TrustState, peer.SharedSecret);
        var presentationState = ComputePresentationState(peer, normalizedTrustState);
        var isTrusted = !string.IsNullOrWhiteSpace(peer.SharedSecret);
        var isManual = peer.SourceKind is "manual" or "both";
        var isDiscovered = peer.SourceKind is "discovered" or "both";
        var importedTargetCount = Math.Max(0, peer.CachedImportedTargetCount);

        return new LanPeerSnapshot(
            peer.PeerId,
            peer.DisplayName,
            peer.LastKnownHost,
            peer.ControlPlanePort,
            peer.SourceKind,
            normalizedTrustState,
            presentationState,
            isTrusted,
            !string.IsNullOrWhiteSpace(peer.SharedSecret),
            peer.SharedSecret,
            isManual,
            isDiscovered,
            importedTargetCount,
            peer.CachedInventorySummary,
            peer.ImportState,
            string.Equals(presentationState, "Connected", StringComparison.OrdinalIgnoreCase),
            string.Equals(presentationState, "PartialImport", StringComparison.OrdinalIgnoreCase),
            string.Equals(presentationState, "Stale", StringComparison.OrdinalIgnoreCase),
            string.Equals(presentationState, "Offline", StringComparison.OrdinalIgnoreCase),
            string.Equals(presentationState, "AuthFailed", StringComparison.OrdinalIgnoreCase),
            string.Equals(presentationState, "CachedOnly", StringComparison.OrdinalIgnoreCase),
            peer.LastSeenUtc,
            peer.LastDiscoveryUtc,
            peer.LastSuccessfulImportUtc,
            peer.LastAuthFailureReason);
    }

    private LanPeerDocument? FindPeerUnsafe(string peerId)
    {
        return _document.Peers.FirstOrDefault(peer => string.Equals(peer.PeerId, peerId, StringComparison.OrdinalIgnoreCase));
    }

    private static void MergePeerDocumentsUnsafe(LanPeerDocument target, LanPeerDocument source)
    {
        target.DisplayName = ChoosePreferredDisplayName(target, source);
        target.LastKnownHost = string.IsNullOrWhiteSpace(target.LastKnownHost) ? source.LastKnownHost : target.LastKnownHost;
        target.ControlPlanePort = target.ControlPlanePort <= 0 ? source.ControlPlanePort : target.ControlPlanePort;
        target.SharedSecret = string.IsNullOrWhiteSpace(target.SharedSecret) ? source.SharedSecret : target.SharedSecret;
        target.SourceKind = MergeSourceKind(target.SourceKind, source.SourceKind);
        target.TrustState = ChoosePreferredTrustState(target.TrustState, source.TrustState, target.SharedSecret, source.SharedSecret);
        target.ImportState = ChoosePreferredImportState(target.ImportState, source.ImportState);
        target.LastSeenUtc = ChooseLatest(target.LastSeenUtc, source.LastSeenUtc);
        target.LastDiscoveryUtc = ChooseLatest(target.LastDiscoveryUtc, source.LastDiscoveryUtc);
        target.LastSuccessfulImportUtc = ChooseLatest(target.LastSuccessfulImportUtc, source.LastSuccessfulImportUtc);
        if (source.CachedImportedTargetCount > target.CachedImportedTargetCount)
        {
            target.CachedImportedTargetCount = source.CachedImportedTargetCount;
        }

        if (string.IsNullOrWhiteSpace(target.CachedInventorySummary) || source.CachedImportedTargetCount >= target.CachedImportedTargetCount)
        {
            target.CachedInventorySummary = string.IsNullOrWhiteSpace(source.CachedInventorySummary) ? target.CachedInventorySummary : source.CachedInventorySummary;
        }

        target.LastAuthFailureReason = string.IsNullOrWhiteSpace(target.LastAuthFailureReason) ? source.LastAuthFailureReason : target.LastAuthFailureReason;
        target.DiscoveryServiceName = string.IsNullOrWhiteSpace(target.DiscoveryServiceName) ? source.DiscoveryServiceName : target.DiscoveryServiceName;
        target.DiscoveryVersion = string.IsNullOrWhiteSpace(target.DiscoveryVersion) ? source.DiscoveryVersion : target.DiscoveryVersion;
    }

    private void NormalizeDocumentUnsafe()
    {
        _document.SchemaVersion = Math.Max(2, _document.SchemaVersion);
        _document.Peers ??= new List<LanPeerDocument>();

        foreach (var peer in _document.Peers)
        {
            peer.PeerId = string.IsNullOrWhiteSpace(peer.PeerId) ? GeneratePeerId() : peer.PeerId.Trim();
            peer.DisplayName = string.IsNullOrWhiteSpace(peer.DisplayName)
                ? string.IsNullOrWhiteSpace(peer.LastKnownHost) ? peer.PeerId : $"{peer.LastKnownHost}:{peer.ControlPlanePort}"
                : peer.DisplayName.Trim();
            peer.LastKnownHost = peer.LastKnownHost?.Trim() ?? string.Empty;
            peer.ControlPlanePort = peer.ControlPlanePort <= 0 ? LanRoutingSettingsService.DefaultControlPlanePort : peer.ControlPlanePort;
            peer.SourceKind = NormalizeSourceKind(peer.SourceKind);
            peer.TrustState = NormalizeTrustState(peer.TrustState, peer.SharedSecret);
            peer.ImportState = NormalizeImportState(peer.ImportState);
            peer.SharedSecret = peer.SharedSecret?.Trim() ?? string.Empty;
            peer.CachedInventorySummary = peer.CachedInventorySummary?.Trim() ?? string.Empty;
            peer.LastAuthFailureReason = peer.LastAuthFailureReason?.Trim() ?? string.Empty;
            peer.DiscoveryServiceName = peer.DiscoveryServiceName?.Trim() ?? string.Empty;
            peer.DiscoveryVersion = peer.DiscoveryVersion?.Trim() ?? string.Empty;
        }

        _document.Peers = _document.Peers
            .GroupBy(peer => peer.PeerId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private void PersistUnsafe()
    {
        File.WriteAllText(_paths.LanPeerRegistryPath, JsonSerializer.Serialize(_document, _jsonOptions));
    }

    private static string NormalizeTrustState(string? trustState, string? sharedSecret)
    {
        if (string.Equals(trustState, "auth_failed", StringComparison.OrdinalIgnoreCase))
        {
            return "auth_failed";
        }

        if (string.Equals(trustState, "offline", StringComparison.OrdinalIgnoreCase))
        {
            return "offline";
        }

        return string.IsNullOrWhiteSpace(sharedSecret) ? "discovered" : "trusted";
    }

    private static string NormalizeImportState(string? importState)
    {
        if (string.Equals(importState, "partial", StringComparison.OrdinalIgnoreCase))
        {
            return "partial";
        }

        if (string.Equals(importState, "refreshing", StringComparison.OrdinalIgnoreCase))
        {
            return "refreshing";
        }

        return string.IsNullOrWhiteSpace(importState) ? "none" : "complete";
    }

    private static string NormalizeSourceKind(string? sourceKind)
    {
        var normalized = sourceKind?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "discovered" => "discovered",
            "both" => "both",
            _ => "manual"
        };
    }

    private static string ComputePresentationState(LanPeerDocument peer, string normalizedTrustState)
    {
        var now = DateTimeOffset.UtcNow;
        var lastContact = new[] { peer.LastSuccessfulImportUtc, peer.LastSeenUtc, peer.LastDiscoveryUtc }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty(DateTimeOffset.MinValue)
            .Max();
        var hasRecentDiscovery = peer.LastDiscoveryUtc.HasValue && now - peer.LastDiscoveryUtc.Value <= PeerFreshFor;
        var hasRecentImport = peer.LastSuccessfulImportUtc.HasValue && now - peer.LastSuccessfulImportUtc.Value <= PeerFreshFor;
        var isTrusted = !string.IsNullOrWhiteSpace(peer.SharedSecret);

        if (string.Equals(normalizedTrustState, "auth_failed", StringComparison.OrdinalIgnoreCase))
        {
            return "AuthFailed";
        }

        if (!isTrusted)
        {
            return hasRecentDiscovery ? "DiscoveredUntrusted" : "CachedOnly";
        }

        if (string.Equals(peer.ImportState, "refreshing", StringComparison.OrdinalIgnoreCase))
        {
            return "TrustedInspecting";
        }

        if (string.Equals(normalizedTrustState, "offline", StringComparison.OrdinalIgnoreCase))
        {
            if (lastContact != DateTimeOffset.MinValue && now - lastContact <= PeerStaleFor)
            {
                return "Stale";
            }

            return "Offline";
        }

        if (string.Equals(peer.ImportState, "partial", StringComparison.OrdinalIgnoreCase))
        {
            return hasRecentImport || hasRecentDiscovery ? "PartialImport" : "CachedOnly";
        }

        if (hasRecentImport || hasRecentDiscovery)
        {
            return "Connected";
        }

        if (lastContact != DateTimeOffset.MinValue && now - lastContact <= PeerStaleFor)
        {
            return "Stale";
        }

        return peer.CachedImportedTargetCount > 0 ? "CachedOnly" : "Offline";
    }

    private static string ChoosePreferredDisplayName(LanPeerDocument target, LanPeerDocument source)
    {
        if (string.IsNullOrWhiteSpace(target.DisplayName) || IsEndpointDisplayName(target.DisplayName, target.LastKnownHost, target.ControlPlanePort))
        {
            return string.IsNullOrWhiteSpace(source.DisplayName) ? target.DisplayName : source.DisplayName;
        }

        return target.DisplayName;
    }

    private static string ChooseObservedDisplayName(string currentDisplayName, string observedDisplayName, string host, int port)
    {
        if (string.IsNullOrWhiteSpace(currentDisplayName) || IsEndpointDisplayName(currentDisplayName, host, port))
        {
            return string.IsNullOrWhiteSpace(observedDisplayName) ? currentDisplayName : observedDisplayName.Trim();
        }

        return currentDisplayName;
    }

    private static bool IsEndpointDisplayName(string displayName, string host, int controlPlanePort)
    {
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        return string.Equals(displayName.Trim(), $"{host.Trim()}:{controlPlanePort}", StringComparison.OrdinalIgnoreCase);
    }

    private static string ChoosePreferredTrustState(string targetTrustState, string sourceTrustState, string targetSharedSecret, string sourceSharedSecret)
    {
        if (string.Equals(targetTrustState, "auth_failed", StringComparison.OrdinalIgnoreCase) || string.Equals(sourceTrustState, "auth_failed", StringComparison.OrdinalIgnoreCase))
        {
            return "auth_failed";
        }

        if (string.Equals(targetTrustState, "offline", StringComparison.OrdinalIgnoreCase) || string.Equals(sourceTrustState, "offline", StringComparison.OrdinalIgnoreCase))
        {
            return "offline";
        }

        return NormalizeTrustState(targetTrustState, string.IsNullOrWhiteSpace(targetSharedSecret) ? sourceSharedSecret : targetSharedSecret);
    }

    private static string ChoosePreferredImportState(string targetImportState, string sourceImportState)
    {
        if (string.Equals(targetImportState, "refreshing", StringComparison.OrdinalIgnoreCase) || string.Equals(sourceImportState, "refreshing", StringComparison.OrdinalIgnoreCase))
        {
            return "refreshing";
        }

        if (string.Equals(targetImportState, "partial", StringComparison.OrdinalIgnoreCase) || string.Equals(sourceImportState, "partial", StringComparison.OrdinalIgnoreCase))
        {
            return "partial";
        }

        if (string.Equals(targetImportState, "complete", StringComparison.OrdinalIgnoreCase) || string.Equals(sourceImportState, "complete", StringComparison.OrdinalIgnoreCase))
        {
            return "complete";
        }

        return "none";
    }

    private static string MergeSourceKind(string currentSourceKind, string observedSourceKind)
    {
        var current = NormalizeSourceKind(currentSourceKind);
        var observed = NormalizeSourceKind(observedSourceKind);
        if (string.Equals(current, observed, StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }

        return "both";
    }

    private static DateTimeOffset? ChooseLatest(DateTimeOffset? first, DateTimeOffset? second)
    {
        if (!first.HasValue)
        {
            return second;
        }

        if (!second.HasValue)
        {
            return first;
        }

        return first >= second ? first : second;
    }

    private static string GeneratePeerId()
    {
        return $"peer-{Guid.NewGuid():N}";
    }
}

public record LanDiscoveryPeerObservation(
    string PeerId,
    string DisplayName,
    string Host,
    int ControlPlanePort,
    string ServiceName,
    string Version,
    DateTimeOffset LastSeenUtc);

public record LanPeerRegistrySnapshot(
    string LocalPeerId,
    IReadOnlyList<LanPeerSnapshot> Peers);

public record LanPeerSnapshot(
    string PeerId,
    string DisplayName,
    string LastKnownHost,
    int ControlPlanePort,
    string SourceKind,
    string TrustState,
    string PresentationState,
    bool IsTrusted,
    bool HasSharedSecret,
    string SharedSecret,
    bool IsManual,
    bool IsDiscovered,
    int CachedImportedTargetCount,
    string CachedInventorySummary,
    string ImportState,
    bool IsConnected,
    bool IsPartialImport,
    bool IsStale,
    bool IsOffline,
    bool IsAuthFailed,
    bool IsCachedOnly,
    DateTimeOffset? LastSeenUtc,
    DateTimeOffset? LastDiscoveryUtc,
    DateTimeOffset? LastSuccessfulImportUtc,
    string LastAuthFailureReason);

public class LanPeerRegistryDocument
{
    public int SchemaVersion { get; set; } = 2;
    public List<LanPeerDocument> Peers { get; set; } = new();
}

public class LanPeerDocument
{
    public string PeerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LastKnownHost { get; set; } = string.Empty;
    public int ControlPlanePort { get; set; } = LanRoutingSettingsService.DefaultControlPlanePort;
    public string SharedSecret { get; set; } = string.Empty;
    public string SourceKind { get; set; } = "manual";
    public string TrustState { get; set; } = "discovered";
    public string ImportState { get; set; } = "none";
    public DateTimeOffset? LastSeenUtc { get; set; }
    public DateTimeOffset? LastDiscoveryUtc { get; set; }
    public DateTimeOffset? LastSuccessfulImportUtc { get; set; }
    public int CachedImportedTargetCount { get; set; }
    public string CachedInventorySummary { get; set; } = string.Empty;
    public string LastAuthFailureReason { get; set; } = string.Empty;
    public string DiscoveryServiceName { get; set; } = string.Empty;
    public string DiscoveryVersion { get; set; } = string.Empty;
}
