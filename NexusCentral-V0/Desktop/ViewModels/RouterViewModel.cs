using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using NexusCentral_Avalonia.Models;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.ViewModels;

public partial class RouterViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly Dictionary<string, RoutingRoleCardViewModel> _roleIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<ChatQueueItemViewModel> _observedQueueItems = new();

    public ObservableCollection<RoutingRoleCardViewModel> RouteRoles { get; } = new();
    public ObservableCollection<RouterPeerListItemViewModel> Peers { get; } = new();
    public ObservableCollection<RouterImportedTargetViewModel> ImportedTargets { get; } = new();
    public ObservableCollection<RouterQueueRequestViewModel> QueueRequests { get; } = new();
    public ObservableCollection<RouterQueueFilterOptionViewModel> QueueFilterOptions { get; } = new();
    public ObservableCollection<RouterQueueRetentionOptionViewModel> CompletedRetentionOptions { get; } = new();

    [ObservableProperty]
    private string _dashboardStatusText = "Routing dashboard ready.";

    [ObservableProperty]
    private string _lastRequestHeadline = "No recent requests";

    [ObservableProperty]
    private string _lastRequestDetail = "Run a request in Lab to populate routing telemetry.";

    [ObservableProperty]
    private string _queueSummaryText = "Queue empty";

    [ObservableProperty]
    private string _instanceUsageText = "0 local | 0 imported";

    [ObservableProperty]
    private bool _isQueuePanelOpen;

    [ObservableProperty]
    private bool _hasQueueHistory;

    [ObservableProperty]
    private bool _hasVisibleQueueRequests;

    [ObservableProperty]
    private string _queueFilterSummaryText = "No stored requests.";

    [ObservableProperty]
    private int _completedRequestCount;

    [ObservableProperty]
    private RouterQueueFilterOptionViewModel? _selectedQueueFilter;

    [ObservableProperty]
    private RouterQueueRetentionOptionViewModel? _selectedCompletedRetentionOption;

    [ObservableProperty]
    private string _discoveryStatusText = "LAN discovery idle.";

    [ObservableProperty]
    private string _manualPeerStatusText = "Manual peer add is available.";

    [ObservableProperty]
    private string _manualDisplayNameInput = string.Empty;

    [ObservableProperty]
    private string _manualHostInput = string.Empty;

    [ObservableProperty]
    private string _manualPortText = "3005";

    [ObservableProperty]
    private string _manualPeerTokenInput = string.Empty;

    [ObservableProperty]
    private bool _manualAllowRoutingInput = true;

    [ObservableProperty]
    private string _peerSearchText = string.Empty;

    [ObservableProperty]
    private bool _showDiscoveredPeers = true;

    [ObservableProperty]
    private bool _showTrustedPeers = true;

    [ObservableProperty]
    private bool _showConnectedPeers = true;

    [ObservableProperty]
    private bool _showStalePeers = true;

    [ObservableProperty]
    private bool _showManualPeers = true;

    [ObservableProperty]
    private bool _isPeerOverlayOpen;

    [ObservableProperty]
    private bool _showManualPeerOverlay = true;

    [ObservableProperty]
    private RouterPeerListItemViewModel? _selectedPeer;

    [ObservableProperty]
    private string _selectedPeerTokenInput = string.Empty;

    [ObservableProperty]
    private string _selectedPeerActionStatusText = "Select a peer to inspect trust, imported targets, and repair actions.";

    [ObservableProperty]
    private string _selectedPeerAddressText = string.Empty;

    [ObservableProperty]
    private string _selectedPeerSummaryText = string.Empty;

    [ObservableProperty]
    private string _selectedPeerImportedSummaryText = string.Empty;

    [ObservableProperty]
    private string _localLanDisplayName = string.Empty;

    [ObservableProperty]
    private string _localLanControlPlaneUrl = string.Empty;

    [ObservableProperty]
    private string _localLanSharedSecret = string.Empty;

    [ObservableProperty]
    private string _lanControlPlaneStatusText = "LAN control plane disabled.";

    [ObservableProperty]
    private bool _isLanControlPlaneExposed;

    public RouterViewModel(AppServices services)
    {
        _services = services;
        QueueFilterOptions.Add(new RouterQueueFilterOptionViewModel("all", "All"));
        QueueFilterOptions.Add(new RouterQueueFilterOptionViewModel("active", "Active"));
        QueueFilterOptions.Add(new RouterQueueFilterOptionViewModel("queued", "Queued"));
        QueueFilterOptions.Add(new RouterQueueFilterOptionViewModel("running", "Running"));
        QueueFilterOptions.Add(new RouterQueueFilterOptionViewModel("completed", "Completed"));
        QueueFilterOptions.Add(new RouterQueueFilterOptionViewModel("errors", "Errors"));
        CompletedRetentionOptions.Add(new RouterQueueRetentionOptionViewModel(5, "Last 5"));
        CompletedRetentionOptions.Add(new RouterQueueRetentionOptionViewModel(10, "Last 10"));
        CompletedRetentionOptions.Add(new RouterQueueRetentionOptionViewModel(25, "Last 25"));
        CompletedRetentionOptions.Add(new RouterQueueRetentionOptionViewModel(50, "Last 50"));
        CompletedRetentionOptions.Add(new RouterQueueRetentionOptionViewModel(int.MaxValue, "All"));
        _selectedQueueFilter = QueueFilterOptions.FirstOrDefault();
        _selectedCompletedRetentionOption = CompletedRetentionOptions.FirstOrDefault(option => option.Value == 10) ?? CompletedRetentionOptions.FirstOrDefault();

        _services.Library.LibraryInventoryChanged += RefreshDashboard;
        _services.Library.InstanceChanged += _ => RefreshDashboard();
        _services.Library.InstanceConsoleChanged += _ => RefreshDashboard();
        _services.RoutingRequests.Changed += RefreshDashboard;
        _services.LanRouting.Changed += RefreshDashboard;
        _services.PeerRegistry.Changed += RefreshDashboard;
        _services.LanDiscoveryBrowser.Changed += RefreshDashboard;
        _services.LanDiscoveryAdvertisement.Changed += RefreshDashboard;
        if (_services.Chat is { } chat)
        {
            chat.PropertyChanged += OnChatPropertyChanged;
            chat.QueueItems.CollectionChanged += OnQueueItemsChanged;
            SyncQueueItemSubscriptions(chat);
        }

        RefreshDashboard();
    }

    public bool HasSelectedPeer => SelectedPeer is not null;
    public bool HasPeers => Peers.Count > 0;
    public bool NoPeers => Peers.Count == 0;
    public bool HasImportedTargets => ImportedTargets.Count > 0;
    public bool CanRefreshSelectedPeer => SelectedPeer is not null;
    public bool CanForgetSelectedPeer => SelectedPeer is not null;
    public bool CanRepairSelectedPeer => SelectedPeer is not null && (SelectedPeer.IsAuthFailed || !SelectedPeer.IsTrusted);
    public bool CanPruneCompletedRequests => CompletedRequestCount > EffectiveCompletedRetentionCount;
    public bool CanClearCompletedRequests => CompletedRequestCount > 0;
    public double QueuePanelMaxHeight => IsQueuePanelOpen ? 340d : 0d;
    public double QueuePanelOpacity => IsQueuePanelOpen ? 1d : 0d;
    public string QueuePanelToggleText => IsQueuePanelOpen ? "Hide Queue" : "Show Queue";
    public string LanControlPlaneToggleText => IsLanControlPlaneExposed ? "Disable LAN Control" : "Enable LAN Control";
    public string QueueEmptyStateText => HasQueueHistory
        ? "No requests match this filter."
        : "No stored requests yet.";
    public string ImportedTargetsEmptyText => HasSelectedPeer
        ? "No imported targets for this peer yet."
        : "Select a peer to view imported targets.";
    public string PeerUtilitySummaryText => $"{Peers.Count} peers | {InstanceUsageText} | {QueueSummaryText}";

    [RelayCommand]
    private void ToggleQueuePanel()
    {
        IsQueuePanelOpen = !IsQueuePanelOpen;
    }

    [RelayCommand]
    private void TogglePeerOverlay()
    {
        IsPeerOverlayOpen = !IsPeerOverlayOpen;
    }

    [RelayCommand]
    private void ClosePeerOverlay()
    {
        IsPeerOverlayOpen = false;
    }

    [RelayCommand]
    private void OpenManualPeerOverlay()
    {
        ShowManualPeerOverlay = true;
    }

    [RelayCommand]
    private void OpenThisMachineOverlay()
    {
        ShowManualPeerOverlay = false;
    }

    [RelayCommand]
    private void ToggleLanControlPlaneExposure()
    {
        _services.LanRouting.SetExposeControlPlaneOnLan(!IsLanControlPlaneExposed);
    }

    [RelayCommand]
    private void RotateLanSharedSecret()
    {
        _services.LanRouting.RotateSharedSecret();
    }

    [RelayCommand]
    private void PruneCompletedRequests()
    {
        if (_services.Chat is not { } chat)
        {
            return;
        }

        chat.PruneCompletedQueueItems(EffectiveCompletedRetentionCount);
        RefreshRequestStatus();
    }

    [RelayCommand]
    private void ClearCompletedRequests()
    {
        if (_services.Chat is not { } chat)
        {
            return;
        }

        chat.ClearCompletedQueueItems();
        RefreshRequestStatus();
    }

    [RelayCommand]
    private async Task AddManualPeer()
    {
        if (string.IsNullOrWhiteSpace(ManualHostInput))
        {
            ManualPeerStatusText = "Enter a host name or LAN IP before adding a peer.";
            return;
        }

        if (!int.TryParse(ManualPortText, out var port) || port < 1 || port > 65535)
        {
            ManualPeerStatusText = "Use a valid control-plane port between 1 and 65535.";
            return;
        }

        ManualPeerStatusText = $"Adding peer {ManualHostInput}:{port}...";
        var target = await _services.Library.ConnectRemoteInstanceAsync(ManualDisplayNameInput, ManualHostInput, port, ManualAllowRoutingInput, ManualPeerTokenInput);
        ManualPeerStatusText = target is null
            ? $"Saved {ManualHostInput}:{port}."
            : $"Imported targets from {ManualHostInput}:{port}.";
        ManualDisplayNameInput = string.Empty;
        ManualHostInput = string.Empty;
        ManualPortText = "3005";
        ManualPeerTokenInput = string.Empty;
        ManualAllowRoutingInput = true;
        IsPeerOverlayOpen = false;
        RefreshDashboard();
    }

    [RelayCommand]
    private async Task RefreshSelectedPeer()
    {
        if (SelectedPeer is null)
        {
            return;
        }

        SelectedPeerActionStatusText = $"Refreshing {SelectedPeer.DisplayName}...";
        var result = await _services.Library.RefreshPeerAsync(SelectedPeer.PeerId);
        SelectedPeerActionStatusText = result is null
            ? $"Could not refresh {SelectedPeer.DisplayName}."
            : $"Refreshed {SelectedPeer.DisplayName}.";
        RefreshDashboard();
    }

    [RelayCommand]
    private async Task PairOrRepairSelectedPeer()
    {
        if (SelectedPeer is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPeerTokenInput))
        {
            SelectedPeerActionStatusText = "Enter the peer token before pairing or repairing auth.";
            return;
        }

        SelectedPeerActionStatusText = SelectedPeer.IsAuthFailed
            ? $"Repairing auth for {SelectedPeer.DisplayName}..."
            : $"Pairing {SelectedPeer.DisplayName}...";
        var result = await _services.Library.RetryPeerAuthAsync(SelectedPeer.PeerId, SelectedPeerTokenInput);
        SelectedPeerActionStatusText = result is null
            ? $"Could not pair {SelectedPeer.DisplayName}."
            : $"Pairing updated for {SelectedPeer.DisplayName}.";
        SelectedPeerTokenInput = string.Empty;
        RefreshDashboard();
    }

    [RelayCommand]
    private void ForgetSelectedPeer()
    {
        if (SelectedPeer is null)
        {
            return;
        }

        var peerName = SelectedPeer.DisplayName;
        _services.Library.ForgetPeer(SelectedPeer.PeerId);
        SelectedPeer = null;
        SelectedPeerActionStatusText = $"Forgot peer {peerName}.";
        RefreshDashboard();
    }

    private void RefreshDashboard()
    {
        var routingTargets = _services.Library.GetRoutingInstances();
        Dispatcher.UIThread.Post(() =>
        {
            RefreshLanControlPlaneState();
            RefreshPeerDirectory();
            RefreshSelectedPeerWorkspace();
            RefreshRoles(routingTargets);
            RefreshRequestStatus();
            var localCount = routingTargets.Count(target => !target.IsRemote);
            var importedCount = routingTargets.Count(target => target.IsRemote);
            DashboardStatusText = $"{Peers.Count} peer(s) | {importedCount} imported | {localCount} local";
            InstanceUsageText = $"{localCount} local | {importedCount} imported";
            OnPropertyChanged(nameof(PeerUtilitySummaryText));
        });
    }

    private void RefreshLanControlPlaneState()
    {
        var lanSettings = _services.LanRouting.GetSnapshot();
        LocalLanDisplayName = lanSettings.DisplayName;
        LocalLanControlPlaneUrl = $"http://{lanSettings.AdvertisedHost}:{lanSettings.ControlPlanePort}";
        LocalLanSharedSecret = lanSettings.SharedSecret;
        IsLanControlPlaneExposed = lanSettings.ExposeControlPlaneOnLan;
        var advertisement = _services.LanDiscoveryAdvertisement.StatusText;
        LanControlPlaneStatusText = lanSettings.ExposeControlPlaneOnLan
            ? $"LAN control plane active on {LocalLanControlPlaneUrl}. {advertisement}"
            : "LAN control plane disabled.";
        DiscoveryStatusText = _services.LanDiscoveryBrowser.GetSnapshot().StatusText;
    }

    private void RefreshPeerDirectory()
    {
        var importedCounts = _services.Library.GetImportedTargets()
            .GroupBy(target => target.PeerId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var selectedPeerId = SelectedPeer?.PeerId ?? string.Empty;
        var filteredPeers = _services.PeerRegistry.GetPeers()
            .Where(IncludePeer)
            .Select(peer => new RouterPeerListItemViewModel(
                peer,
                importedCounts.TryGetValue(peer.PeerId, out var importedCount) ? importedCount : peer.CachedImportedTargetCount))
            .OrderByDescending(peer => peer.IsConnected)
            .ThenByDescending(peer => peer.IsTrusted)
            .ThenBy(peer => peer.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Peers.Clear();
        foreach (var peer in filteredPeers)
        {
            Peers.Add(peer);
        }

        if (!string.IsNullOrWhiteSpace(selectedPeerId))
        {
            SelectedPeer = Peers.FirstOrDefault(peer => string.Equals(peer.PeerId, selectedPeerId, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedPeer is not null && Peers.All(peer => !string.Equals(peer.PeerId, SelectedPeer.PeerId, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedPeer = null;
        }

        OnPropertyChanged(nameof(HasPeers));
        OnPropertyChanged(nameof(NoPeers));
        OnPropertyChanged(nameof(HasSelectedPeer));
    }

    private void RefreshSelectedPeerWorkspace()
    {
        ImportedTargets.Clear();
        if (SelectedPeer is null)
        {
            SelectedPeerAddressText = string.Empty;
            SelectedPeerSummaryText = "No peer selected.";
            SelectedPeerImportedSummaryText = string.Empty;
            OnPropertyChanged(nameof(HasImportedTargets));
            OnPropertyChanged(nameof(CanRefreshSelectedPeer));
            OnPropertyChanged(nameof(CanRepairSelectedPeer));
            OnPropertyChanged(nameof(CanForgetSelectedPeer));
            return;
        }

        var peer = _services.PeerRegistry.GetPeer(SelectedPeer.PeerId);
        if (peer is null)
        {
            return;
        }

        foreach (var target in _services.Library.GetImportedTargets(peer.PeerId))
        {
            ImportedTargets.Add(new RouterImportedTargetViewModel(target));
        }

        SelectedPeerAddressText = $"{peer.LastKnownHost}:{peer.ControlPlanePort}";
        SelectedPeerSummaryText = string.Join(" | ", new[]
        {
            peer.PresentationState,
            peer.SourceKind,
            peer.IsTrusted ? "trusted" : "untrusted",
            peer.LastSeenUtc.HasValue ? $"last seen {FormatRelative(peer.LastSeenUtc.Value)}" : "not seen live yet"
        });
        SelectedPeerImportedSummaryText = string.IsNullOrWhiteSpace(peer.CachedInventorySummary)
            ? $"{ImportedTargets.Count} imported target(s)"
            : peer.CachedInventorySummary;
        OnPropertyChanged(nameof(HasImportedTargets));
        OnPropertyChanged(nameof(CanRefreshSelectedPeer));
        OnPropertyChanged(nameof(CanRepairSelectedPeer));
        OnPropertyChanged(nameof(CanForgetSelectedPeer));
    }

    private void RefreshRoles(IReadOnlyList<RoutingInstanceSnapshot> routingTargets)
    {
        var targets = BuildRoutingTargets(routingTargets);
        var visibleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in _services.Library.GetRoles())
        {
            visibleIds.Add(snapshot.RoleName);
            if (_roleIndex.TryGetValue(snapshot.RoleName, out var existing))
            {
                existing.Update(snapshot, targets);
                continue;
            }

            var card = new RoutingRoleCardViewModel(_services, snapshot, targets);
            _roleIndex[snapshot.RoleName] = card;
            RouteRoles.Add(card);
        }

        for (var index = RouteRoles.Count - 1; index >= 0; index--)
        {
            var card = RouteRoles[index];
            if (visibleIds.Contains(card.RoleName))
            {
                continue;
            }

            _roleIndex.Remove(card.RoleName);
            RouteRoles.RemoveAt(index);
        }
    }

    private void RefreshRequestStatus()
    {
        if (_services.Chat is not { } chat)
        {
            LastRequestHeadline = "Chat not available";
            LastRequestDetail = "Router queue telemetry requires the Lab chat view model.";
            QueueSummaryText = "Queue unavailable";
            HasQueueHistory = false;
            HasVisibleQueueRequests = false;
            QueueFilterSummaryText = "Queue unavailable.";
            CompletedRequestCount = 0;
            QueueRequests.Clear();
            return;
        }

        QueueSummaryText = chat.QueueStatusText;
        var orderedQueueItems = chat.QueueItems
            .OrderBy(item => item.IsRunning ? 0 : item.IsQueued ? 1 : 2)
            .ThenBy(item => item.IsQueued ? item.CreatedAtUtc : DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.IsQueued ? DateTimeOffset.MinValue : item.CompletedAtUtc ?? item.StartedAtUtc ?? item.CreatedAtUtc)
            .ToList();
        var queueRows = ApplyQueueFilter(orderedQueueItems)
            .Select(item => new RouterQueueRequestViewModel(item))
            .ToList();
        QueueRequests.Clear();
        foreach (var queueRow in queueRows)
        {
            QueueRequests.Add(queueRow);
        }

        CompletedRequestCount = orderedQueueItems.Count(item => !item.IsQueued && !item.IsRunning);
        HasQueueHistory = orderedQueueItems.Count > 0;
        HasVisibleQueueRequests = queueRows.Count > 0;
        QueueFilterSummaryText = BuildQueueFilterSummary(queueRows.Count, orderedQueueItems.Count);
        if (chat.QueueItems.Any(item => item.IsQueued || item.IsRunning))
        {
            IsQueuePanelOpen = true;
        }

        var latestRun = chat.GetLatestRun();
        if (latestRun is null)
        {
            LastRequestHeadline = "No recent requests";
            LastRequestDetail = "Run a request in Lab to populate routing telemetry.";
            return;
        }

        LastRequestHeadline = Shorten(latestRun.Prompt, latestRun.SelectedModelLabel, latestRun.ModelLabel, latestRun.OutputPreview);
        LastRequestDetail = string.Join(" | ", new[]
        {
            string.IsNullOrWhiteSpace(latestRun.Status) ? string.Empty : latestRun.Status,
            string.IsNullOrWhiteSpace(latestRun.ExecutionMode) ? string.Empty : latestRun.ExecutionMode,
            string.IsNullOrWhiteSpace(latestRun.SelectedModelLabel) ? latestRun.ModelLabel : latestRun.SelectedModelLabel,
            string.IsNullOrWhiteSpace(latestRun.SelectedInstanceId) ? "auto-route" : latestRun.SelectedInstanceId
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private IReadOnlyList<RoutingTargetOptionViewModel> BuildRoutingTargets(IReadOnlyList<RoutingInstanceSnapshot> routingTargets)
    {
        var options = new List<RoutingTargetOptionViewModel>
        {
            new(string.Empty, "Unassigned", "Role has no routing target")
        };
        options.AddRange(routingTargets
            .Select(snapshot => new RoutingTargetOptionViewModel(
                snapshot.InstanceId,
                snapshot.DisplayName,
                $"{snapshot.OriginLabel} | {snapshot.StatusText} | {snapshot.AddressText}")));
        return options;
    }

    private bool IncludePeer(LanPeerSnapshot peer)
    {
        if (!string.IsNullOrWhiteSpace(PeerSearchText))
        {
            var search = PeerSearchText.Trim();
            if (!(peer.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                  peer.LastKnownHost.Contains(search, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        if (!ShowManualPeers && peer.IsManual)
        {
            return false;
        }

        if (!ShowDiscoveredPeers && peer.IsDiscovered && !peer.IsTrusted)
        {
            return false;
        }

        if (!ShowTrustedPeers && peer.IsTrusted)
        {
            return false;
        }

        if (!ShowConnectedPeers && peer.IsConnected)
        {
            return false;
        }

        if (!ShowStalePeers && (peer.IsStale || peer.IsOffline || peer.IsCachedOnly))
        {
            return false;
        }

        return true;
    }

    private void OnChatPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatViewModel.LatestRunId) or nameof(ChatViewModel.QueueStatusText))
        {
            RefreshRequestStatus();
        }
    }

    private void OnQueueItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_services.Chat is { } chat)
        {
            SyncQueueItemSubscriptions(chat);
        }

        RefreshRequestStatus();
    }

    private void OnQueueItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatQueueItemViewModel.Status) or
            nameof(ChatQueueItemViewModel.StatusText) or
            nameof(ChatQueueItemViewModel.IsActive) or
            nameof(ChatQueueItemViewModel.ErrorText) or
            nameof(ChatQueueItemViewModel.StartedAtUtc) or
            nameof(ChatQueueItemViewModel.CompletedAtUtc))
        {
            RefreshRequestStatus();
        }
    }

    private void SyncQueueItemSubscriptions(ChatViewModel chat)
    {
        var currentItems = chat.QueueItems.ToHashSet();
        foreach (var staleItem in _observedQueueItems.Where(item => !currentItems.Contains(item)).ToList())
        {
            staleItem.PropertyChanged -= OnQueueItemPropertyChanged;
            _observedQueueItems.Remove(staleItem);
        }

        foreach (var queueItem in currentItems)
        {
            if (_observedQueueItems.Add(queueItem))
            {
                queueItem.PropertyChanged += OnQueueItemPropertyChanged;
            }
        }
    }

    private IReadOnlyList<ChatQueueItemViewModel> ApplyQueueFilter(IReadOnlyList<ChatQueueItemViewModel> items)
    {
        var filterId = SelectedQueueFilter?.Id ?? "all";
        var filteredItems = items
            .Where(item => MatchesQueueFilter(item, filterId))
            .ToList();
        if (!FilterIncludesCompleted(filterId))
        {
            return filteredItems;
        }

        IEnumerable<ChatQueueItemViewModel> completedItems = filteredItems
            .Where(item => !item.IsQueued && !item.IsRunning)
            .OrderByDescending(item => item.CompletedAtUtc ?? item.StartedAtUtc ?? item.CreatedAtUtc);
        if (EffectiveCompletedRetentionCount != int.MaxValue)
        {
            completedItems = completedItems.Take(EffectiveCompletedRetentionCount);
        }

        return filteredItems
            .Where(item => item.IsQueued || item.IsRunning)
            .Concat(completedItems)
            .OrderBy(item => item.IsRunning ? 0 : item.IsQueued ? 1 : 2)
            .ThenBy(item => item.IsQueued ? item.CreatedAtUtc : DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.IsQueued ? DateTimeOffset.MinValue : item.CompletedAtUtc ?? item.StartedAtUtc ?? item.CreatedAtUtc)
            .ToList();
    }

    private string BuildQueueFilterSummary(int visibleCount, int storedCount)
    {
        if (storedCount == 0)
        {
            return "No stored requests.";
        }

        var retentionText = FilterIncludesCompleted(SelectedQueueFilter?.Id ?? "all")
            ? $"retaining {SelectedCompletedRetentionOption?.Label ?? "Last 10"} completed"
            : "completed rows hidden by filter";
        return $"{visibleCount} visible of {storedCount} stored | {retentionText}";
    }

    private int EffectiveCompletedRetentionCount => SelectedCompletedRetentionOption?.Value ?? 10;

    private static bool MatchesQueueFilter(ChatQueueItemViewModel item, string filterId)
    {
        return filterId switch
        {
            "active" => item.IsQueued || item.IsRunning,
            "queued" => item.IsQueued,
            "running" => item.IsRunning,
            "completed" => !item.IsQueued && !item.IsRunning,
            "errors" => item.IsError,
            _ => true
        };
    }

    private static bool FilterIncludesCompleted(string filterId)
    {
        return filterId is "all" or "completed" or "errors";
    }

    private static string Shorten(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var trimmed = candidate.Trim();
            return trimmed.Length <= 88 ? trimmed : $"{trimmed[..85]}...";
        }

        return "Latest request";
    }

    private static string FormatRelative(DateTimeOffset value)
    {
        var elapsed = DateTimeOffset.UtcNow - value;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return $"{Math.Max(1, (int)elapsed.TotalSeconds)}s ago";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{Math.Max(1, (int)elapsed.TotalMinutes)}m ago";
        }

        return $"{Math.Max(1, (int)elapsed.TotalHours)}h ago";
    }

    partial void OnIsQueuePanelOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(QueuePanelMaxHeight));
        OnPropertyChanged(nameof(QueuePanelOpacity));
        OnPropertyChanged(nameof(QueuePanelToggleText));
    }

    partial void OnHasQueueHistoryChanged(bool value)
    {
        OnPropertyChanged(nameof(QueueEmptyStateText));
    }

    partial void OnHasVisibleQueueRequestsChanged(bool value)
    {
        OnPropertyChanged(nameof(QueueEmptyStateText));
    }

    partial void OnCompletedRequestCountChanged(int value)
    {
        OnPropertyChanged(nameof(CanPruneCompletedRequests));
        OnPropertyChanged(nameof(CanClearCompletedRequests));
    }

    partial void OnIsLanControlPlaneExposedChanged(bool value)
    {
        OnPropertyChanged(nameof(LanControlPlaneToggleText));
    }

    partial void OnSelectedQueueFilterChanged(RouterQueueFilterOptionViewModel? value)
    {
        RefreshRequestStatus();
    }

    partial void OnSelectedCompletedRetentionOptionChanged(RouterQueueRetentionOptionViewModel? value)
    {
        OnPropertyChanged(nameof(CanPruneCompletedRequests));
        RefreshRequestStatus();
    }

    partial void OnPeerSearchTextChanged(string value)
    {
        RefreshPeerDirectory();
    }

    partial void OnShowDiscoveredPeersChanged(bool value)
    {
        RefreshPeerDirectory();
    }

    partial void OnShowTrustedPeersChanged(bool value)
    {
        RefreshPeerDirectory();
    }

    partial void OnShowConnectedPeersChanged(bool value)
    {
        RefreshPeerDirectory();
    }

    partial void OnShowStalePeersChanged(bool value)
    {
        RefreshPeerDirectory();
    }

    partial void OnShowManualPeersChanged(bool value)
    {
        RefreshPeerDirectory();
    }

    partial void OnSelectedPeerChanged(RouterPeerListItemViewModel? value)
    {
        SelectedPeerTokenInput = string.Empty;
        if (value is not null)
        {
            foreach (var peer in Peers)
            {
                peer.IsExpanded = string.Equals(peer.PeerId, value.PeerId, StringComparison.OrdinalIgnoreCase);
            }
        }
        RefreshSelectedPeerWorkspace();
    }
}

public class RouterQueueFilterOptionViewModel
{
    public RouterQueueFilterOptionViewModel(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }
}

public class RouterQueueRetentionOptionViewModel
{
    public RouterQueueRetentionOptionViewModel(int value, string label)
    {
        Value = value;
        Label = label;
    }

    public int Value { get; }
    public string Label { get; }
}

public class RouterQueueRequestViewModel
{
    public RouterQueueRequestViewModel(ChatQueueItemViewModel item)
    {
        StatusText = string.IsNullOrWhiteSpace(item.StatusText) ? item.Status : item.StatusText;
        RequestText = BuildPreview(item.PromptText, item.Title);
        MetaText = string.Join(" | ", new[]
        {
            ResolveExecutionLabel(item),
            string.IsNullOrWhiteSpace(item.ModelLabel) ? string.Empty : item.ModelLabel,
            string.IsNullOrWhiteSpace(item.SelectedInstanceId) ? "auto-route" : item.SelectedInstanceId,
            string.IsNullOrWhiteSpace(item.ArtifactSummary) ? string.Empty : item.ArtifactSummary
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        TimelineText = BuildTimelineText(item);
        ErrorText = item.ErrorText;
    }

    public string StatusText { get; }
    public string RequestText { get; }
    public string MetaText { get; }
    public string TimelineText { get; }
    public string ErrorText { get; }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    private static string ResolveExecutionLabel(ChatQueueItemViewModel item)
    {
        return item.ExecutionKind switch
        {
            ChatExecutionKind.Workflow => string.IsNullOrWhiteSpace(item.WorkflowName) ? "Workflow" : $"Workflow: {item.WorkflowName}",
            ChatExecutionKind.Wrapper => string.IsNullOrWhiteSpace(item.WrapperName) ? "Wrapper" : $"Wrapper: {item.WrapperName}",
            _ => "Direct chat"
        };
    }

    private static string BuildTimelineText(ChatQueueItemViewModel item)
    {
        var stateText = item.IsRunning
            ? $"Started {FormatTime(item.StartedAtUtc ?? item.CreatedAtUtc)}"
            : item.CompletedAtUtc.HasValue
                ? $"Finished {FormatTime(item.CompletedAtUtc.Value)}"
                : $"Queued {FormatTime(item.CreatedAtUtc)}";
        return stateText;
    }

    private static string BuildPreview(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var normalized = string.Join(" ", candidate
                .Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                .Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            return normalized.Length <= 96 ? normalized : $"{normalized[..93]}...";
        }

        return "Stored request";
    }

    private static string FormatTime(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("HH:mm:ss");
    }
}

public partial class RouterPeerListItemViewModel : ObservableObject
{
    public RouterPeerListItemViewModel(LanPeerSnapshot peer, int importedTargetCount)
    {
        PeerId = peer.PeerId;
        DisplayName = peer.DisplayName;
        HostText = string.IsNullOrWhiteSpace(peer.LastKnownHost) ? "No host yet" : $"{peer.LastKnownHost}:{peer.ControlPlanePort}";
        StatusText = peer.PresentationState;
        SummaryText = string.Join(" | ", new[]
        {
            peer.IsTrusted ? "trusted" : "untrusted",
            importedTargetCount == 1 ? "1 imported target" : $"{importedTargetCount} imported targets",
            peer.IsCachedOnly ? "cached" : string.Empty
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        SourceText = peer.SourceKind;
        ImportedTargetCount = importedTargetCount;
        IsTrusted = peer.IsTrusted;
        IsConnected = peer.IsConnected;
        IsAuthFailed = peer.IsAuthFailed;
        IsOffline = peer.IsOffline;
        IsStale = peer.IsStale;
        IsCachedOnly = peer.IsCachedOnly;
    }

    public string PeerId { get; }
    public string DisplayName { get; }
    public string HostText { get; }
    public string StatusText { get; }
    public string SummaryText { get; }
    public string SourceText { get; }
    public int ImportedTargetCount { get; }
    public bool IsTrusted { get; }
    public bool IsConnected { get; }
    public bool IsAuthFailed { get; }
    public bool IsOffline { get; }
    public bool IsStale { get; }
    public bool IsCachedOnly { get; }

    [ObservableProperty]
    private bool _isExpanded;
}

public class RouterImportedTargetViewModel
{
    public RouterImportedTargetViewModel(ImportedRouteTargetSnapshot snapshot)
    {
        RouteTargetId = snapshot.RouteTargetId;
        DisplayName = snapshot.DisplayName;
        StatusText = snapshot.StatusText;
        CapabilityText = snapshot.Capabilities.Count == 0 ? "Capabilities unavailable" : string.Join(", ", snapshot.Capabilities);
        DetailText = string.Join(" | ", new[]
        {
            string.IsNullOrWhiteSpace(snapshot.PrimaryMode) ? string.Empty : snapshot.PrimaryMode,
            string.IsNullOrWhiteSpace(snapshot.HealthStatus) ? string.Empty : snapshot.HealthStatus,
            snapshot.IsCached ? "cached" : string.Empty
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        AssignedRolesText = snapshot.AssignedRoles.Count == 0
            ? "No local roles assigned"
            : $"Assigned: {string.Join(", ", snapshot.AssignedRoles)}";
        BlockedReasonText = string.IsNullOrWhiteSpace(snapshot.BlockedReason) ? "Routable when selected." : snapshot.BlockedReason;
        IsReady = snapshot.IsReady;
    }

    public string RouteTargetId { get; }
    public string DisplayName { get; }
    public string StatusText { get; }
    public string CapabilityText { get; }
    public string DetailText { get; }
    public string AssignedRolesText { get; }
    public string BlockedReasonText { get; }
    public bool IsReady { get; }
}

public class RoutingTargetOptionViewModel
{
    public RoutingTargetOptionViewModel(string instanceId, string label, string detail)
    {
        InstanceId = instanceId;
        Label = label;
        Detail = detail;
    }

    public string InstanceId { get; }
    public string Label { get; }
    public string Detail { get; }
}

public partial class RoutingRoleCardViewModel : ObservableObject
{
    private readonly AppServices _services;
    private bool _suppressUpdates;
    private string _savedTargetId = string.Empty;
    private string _savedRoutingModeText = string.Empty;
    private string _savedReservedMemoryText = "0";
    private string _savedMaxParallelText = "1";
    private string _savedQueueLimitText = "4";

    public RoutingRoleCardViewModel(AppServices services, RuntimeRoleSnapshot snapshot, IReadOnlyList<RoutingTargetOptionViewModel> targets)
    {
        _services = services;
        RoleName = snapshot.RoleName;
        Update(snapshot, targets);
    }

    public string RoleName { get; }
    public ObservableCollection<RoutingTargetOptionViewModel> AvailableTargets { get; } = new();
    public bool HasPendingChanges =>
        !string.Equals(SelectedTarget?.InstanceId ?? string.Empty, _savedTargetId, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(RoutingModeText.Trim(), _savedRoutingModeText, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(ReservedMemoryText.Trim(), _savedReservedMemoryText, StringComparison.Ordinal) ||
        !string.Equals(MaxParallelText.Trim(), _savedMaxParallelText, StringComparison.Ordinal) ||
        !string.Equals(QueueLimitText.Trim(), _savedQueueLimitText, StringComparison.Ordinal);

    [ObservableProperty]
    private string _liveStatus = string.Empty;

    [ObservableProperty]
    private string _assignedSummaryText = "Unassigned";

    [ObservableProperty]
    private RoutingTargetOptionViewModel? _selectedTarget;

    [ObservableProperty]
    private string _routingModeText = string.Empty;

    [ObservableProperty]
    private string _reservedMemoryText = "0";

    [ObservableProperty]
    private string _maxParallelText = "1";

    [ObservableProperty]
    private string _queueLimitText = "4";

    [RelayCommand]
    private void SaveChanges()
    {
        var reservedMemory = int.TryParse(ReservedMemoryText, out var reservedValue) ? reservedValue : 0;
        var maxParallel = int.TryParse(MaxParallelText, out var parallelValue) ? Math.Max(1, parallelValue) : 1;
        var queueLimit = int.TryParse(QueueLimitText, out var queueValue) ? Math.Max(1, queueValue) : 4;
        var updated = _services.Library.UpdateRoleSettings(
            RoleName,
            modelId: null,
            routingMode: RoutingModeText,
            reservedMemory: reservedMemory,
            maxParallel: maxParallel,
            queueLimit: queueLimit,
            instanceId: string.IsNullOrWhiteSpace(SelectedTarget?.InstanceId) ? null : SelectedTarget!.InstanceId);

        if (updated is not null)
        {
            Update(updated, AvailableTargets.ToList());
        }
    }

    [RelayCommand]
    private void ResetChanges()
    {
        _suppressUpdates = true;
        SelectedTarget = AvailableTargets.FirstOrDefault(target => string.Equals(target.InstanceId, _savedTargetId, StringComparison.OrdinalIgnoreCase))
            ?? AvailableTargets.FirstOrDefault();
        RoutingModeText = _savedRoutingModeText;
        ReservedMemoryText = _savedReservedMemoryText;
        MaxParallelText = _savedMaxParallelText;
        QueueLimitText = _savedQueueLimitText;
        _suppressUpdates = false;
        OnPropertyChanged(nameof(HasPendingChanges));
    }

    public void Update(RuntimeRoleSnapshot snapshot, IReadOnlyList<RoutingTargetOptionViewModel> targets)
    {
        AvailableTargets.Clear();
        foreach (var target in targets)
        {
            AvailableTargets.Add(target);
        }

        _suppressUpdates = true;
        LiveStatus = snapshot.LiveStatus;
        AssignedSummaryText = string.IsNullOrWhiteSpace(snapshot.AssignedModelDisplayName)
            ? "Unassigned"
            : snapshot.AssignedModelDisplayName;
        RoutingModeText = string.IsNullOrWhiteSpace(snapshot.RoutingMode) ? snapshot.RoleName : snapshot.RoutingMode;
        ReservedMemoryText = snapshot.ReservedMemory.ToString();
        MaxParallelText = Math.Max(1, snapshot.MaxParallel).ToString();
        QueueLimitText = Math.Max(1, snapshot.QueueLimit).ToString();
        SelectedTarget = AvailableTargets.FirstOrDefault(target => string.Equals(target.InstanceId, snapshot.AssignedInstanceId, StringComparison.OrdinalIgnoreCase))
            ?? AvailableTargets.FirstOrDefault();
        _savedTargetId = SelectedTarget?.InstanceId ?? string.Empty;
        _savedRoutingModeText = RoutingModeText;
        _savedReservedMemoryText = ReservedMemoryText;
        _savedMaxParallelText = MaxParallelText;
        _savedQueueLimitText = QueueLimitText;
        _suppressUpdates = false;
        OnPropertyChanged(nameof(HasPendingChanges));
    }

    partial void OnSelectedTargetChanged(RoutingTargetOptionViewModel? value)
    {
        if (_suppressUpdates)
        {
            return;
        }

        AssignedSummaryText = value is null || string.IsNullOrWhiteSpace(value.InstanceId) ? "Unassigned" : value.Label;
        OnPropertyChanged(nameof(HasPendingChanges));
    }

    partial void OnRoutingModeTextChanged(string value)
    {
        if (!_suppressUpdates)
        {
            OnPropertyChanged(nameof(HasPendingChanges));
        }
    }

    partial void OnReservedMemoryTextChanged(string value)
    {
        if (!_suppressUpdates)
        {
            OnPropertyChanged(nameof(HasPendingChanges));
        }
    }

    partial void OnMaxParallelTextChanged(string value)
    {
        if (!_suppressUpdates)
        {
            OnPropertyChanged(nameof(HasPendingChanges));
        }
    }

    partial void OnQueueLimitTextChanged(string value)
    {
        if (!_suppressUpdates)
        {
            OnPropertyChanged(nameof(HasPendingChanges));
        }
    }
}
