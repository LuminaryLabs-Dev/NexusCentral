using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.ViewModels;

public partial class ModelItemViewModel : ObservableObject
{
    public ModelItemViewModel(
        string modelId,
        string name,
        string size,
        string state,
        string path,
        bool isPinned,
        string healthStatus,
        string backend,
        string assignedEndpoints,
        bool isRunnable,
        string healthReason,
        string modelFamily,
        string quantType,
        long estimatedMemoryMb,
        long maxMemoryMb,
        int maxThreads,
        int contextSize,
        string admissionStatus)
    {
        ModelId = modelId;
        Name = name;
        Size = size;
        State = state;
        Path = path;
        HealthStatus = healthStatus;
        Backend = backend;
        AssignedEndpoints = assignedEndpoints;
        IsRunnable = isRunnable;
        HealthReason = healthReason;
        ModelFamily = modelFamily;
        QuantType = quantType;
        EstimatedMemoryMb = estimatedMemoryMb;
        MaxMemoryMb = maxMemoryMb;
        MaxThreads = maxThreads;
        ContextSize = contextSize;
        AdmissionStatus = admissionStatus;
        IsPinned = isPinned;
        IsLoaded = string.Equals(state, "resident", StringComparison.OrdinalIgnoreCase);
        InstanceCount = string.IsNullOrWhiteSpace(assignedEndpoints) ? 0 : assignedEndpoints.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public string ModelId { get; }
    public string Name { get; }
    public string Size { get; }
    public string State { get; }
    public string Path { get; }
    public string HealthStatus { get; }
    public string Backend { get; }
    public string ModelFamily { get; }
    public string QuantType { get; }
    public long EstimatedMemoryMb { get; }
    public long MaxMemoryMb { get; }
    public int MaxThreads { get; }
    public int ContextSize { get; }
    public string AdmissionStatus { get; }
    public string AssignedEndpoints { get; }
    public bool IsRunnable { get; }
    public string HealthReason { get; }
    public int InstanceCount { get; }
    public bool IsLoaded { get; }
    public bool IsPinned { get; }
    public bool HasHealthStatus => !string.IsNullOrWhiteSpace(HealthStatus);

    [ObservableProperty]
    private bool _isExpanded;

    public string PrimaryActionGlyph => "▶";
    public string PrimaryActionToolTip => IsLoaded ? "Activate this loaded model for Lab." : "Load and activate this model for Lab.";
    public string PreloadToolTip => "Preload this model without making it the active Lab route.";
    public string ExpandGlyph => IsExpanded ? "▾" : "▸";
    public string ExpandToolTip => IsExpanded ? "Hide model details" : "Show model details";
    public string StateBadgeText => string.IsNullOrWhiteSpace(State) ? "unknown" : State;
    public string HealthBadgeText => string.IsNullOrWhiteSpace(HealthStatus) ? "unknown" : HealthStatus;
    public string AdmissionBadgeText => string.IsNullOrWhiteSpace(AdmissionStatus) ? "unknown" : AdmissionStatus;
    public string BackendBadgeText => string.IsNullOrWhiteSpace(Backend) ? "backend unknown" : Backend;
    public string StatusSummary => string.IsNullOrWhiteSpace(HealthStatus) ? State : $"{State} | {HealthStatus}";
    public string RoutesText => string.IsNullOrWhiteSpace(AssignedEndpoints)
        ? "Routes: none assigned yet."
        : $"Routes: {AssignedEndpoints}";
    public string HealthDetailText => string.IsNullOrWhiteSpace(HealthReason)
        ? $"Health: {HealthStatus}"
        : $"Health: {HealthStatus} | {HealthReason}";
    public string MemoryPolicyText => MaxMemoryMb <= 0
        ? $"Admission: {AdmissionStatus} | estimated {EstimatedMemoryMb} MB"
        : $"Admission: {AdmissionStatus} | estimated {EstimatedMemoryMb} MB / budget {MaxMemoryMb} MB | threads {MaxThreads} | ctx {ContextSize}";
    public string CompatibilityText => string.IsNullOrWhiteSpace(QuantType)
        ? $"Family: {ModelFamily}"
        : $"Family: {ModelFamily} | quant {QuantType}";

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ExpandGlyph));
        OnPropertyChanged(nameof(ExpandToolTip));
    }
}

public partial class LoadedInstanceCardViewModel : ObservableObject
{
    private readonly AppServices _services;
    private bool _suppressUpdates;
    private string _appliedMode = string.Empty;

    public LoadedInstanceCardViewModel(AppServices services, LoadedInstanceSnapshot snapshot)
    {
        _services = services;
        AvailableModes = new ObservableCollection<string>(new[] { "thinking", "instruct" });
        InstanceId = snapshot.InstanceId;
        ModelId = snapshot.ModelId;
        DisplayName = snapshot.DisplayName;
        LocalPath = snapshot.LocalPath;
        RuntimeBackend = snapshot.RuntimeBackend;
        LoadState = snapshot.LoadState;
        SupportsVision = snapshot.Capabilities.Contains("vision", StringComparer.OrdinalIgnoreCase);
        SupportsAudio = snapshot.Capabilities.Contains("audio", StringComparer.OrdinalIgnoreCase);
        IsExpanded = true;
        Apply(snapshot, syncPending: true);
    }

    public string InstanceId { get; }
    public string ModelId { get; }
    public string DisplayName { get; }
    public string LocalPath { get; }
    public string RuntimeBackend { get; }
    public string LoadState { get; }
    public bool SupportsVision { get; }
    public bool SupportsAudio { get; }
    public ObservableCollection<string> AvailableModes { get; }

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private string _selectedMode = "thinking";

    [ObservableProperty]
    private long _memoryReservedMb;

    [ObservableProperty]
    private long _pendingMemoryReservedMb;

    [ObservableProperty]
    private long _memoryActualMb;

    [ObservableProperty]
    private int _maxTokens;

    [ObservableProperty]
    private int _pendingMaxTokens;

    [ObservableProperty]
    private string _healthStatus = string.Empty;

    [ObservableProperty]
    private string _healthReason = string.Empty;

    [ObservableProperty]
    private string _consoleText = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isConsoleExpanded = true;

    public string CapabilityText => string.Join(" • ", new[]
    {
        "text",
        SupportsVision ? "vision" : null,
        SupportsAudio ? "audio" : null
    }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public string ActiveLabel => IsActive ? "Deactivate" : "Activate";
    public string PinLabel => IsPinned ? "Unpin" : "Pin";
    public string MemoryText => $"{MemoryActualMb} MB used | {MemoryReservedMb} MB reserved";
    public string MemoryMetricText => $"{MemoryActualMb} / {MemoryReservedMb} MB";
    public string MemoryAllocatedText => MemoryReservedMb <= 0 ? "allocated unknown" : $"{MemoryReservedMb} MB allocated";
    public string MemoryUsedText => $"{MemoryActualMb} MB used";
    public string MemoryUsagePercent => MemoryReservedMb <= 0 ? "unknown" : $"{Math.Min(100, (int)Math.Round((double)MemoryActualMb / MemoryReservedMb * 100))}% used";
    public double MemoryUsageRatio => MemoryReservedMb <= 0 ? 0 : Math.Clamp((double)MemoryActualMb / MemoryReservedMb, 0, 1);
    public bool HasMemoryReservation => MemoryReservedMb > 0;
    public string ShortInstanceId => InstanceId.Length > 7 ? InstanceId[..7] : InstanceId;
    public string HeaderTitle => $"{DisplayName} ({ShortInstanceId})";
    public string ActivityBadgeText => IsActive ? "active" : "standby";
    public string RuntimeDisplayText => string.IsNullOrWhiteSpace(RuntimeBackend) ? "unknown" : RuntimeBackend;
    public string CapabilitiesDisplayText => string.IsNullOrWhiteSpace(CapabilityText) ? "none" : CapabilityText;
    public string HealthDisplayText => string.IsNullOrWhiteSpace(HealthSummary) ? "unknown" : HealthSummary;
    public string HealthSummary => string.IsNullOrWhiteSpace(GetInlineHealthReason(HealthReason))
        ? HealthStatus
        : $"{HealthStatus} | {GetInlineHealthReason(HealthReason)}";
    public bool HasHealthStatus => !string.IsNullOrWhiteSpace(HealthStatus);
    public string CompactStatusText => string.Join(" | ", new[]
    {
        IsActive ? "active" : "standby",
        SelectedMode,
        string.IsNullOrWhiteSpace(HealthStatus) ? string.Empty : HealthStatus
    }.Where(value => !string.IsNullOrWhiteSpace(value)));
    public string PrimaryActionGlyph => IsActive ? "●" : "▶";
    public string PrimaryActionToolTip => IsActive ? "Deactivate this instance." : "Activate this instance for its current mode.";
    public string PinGlyph => IsPinned ? "★" : "☆";
    public string PinToolTip => IsPinned ? "Unpin this instance." : "Pin this instance.";
    public string ExpandGlyph => IsExpanded ? "▾" : "▸";
    public string ExpandToolTip => IsExpanded ? "Hide instance details" : "Show instance details";
    public string ConsoleExpandGlyph => IsConsoleExpanded ? "▾" : "▸";
    public string ConsoleToggleToolTip => IsConsoleExpanded ? "Hide console output" : "Show console output";
    public bool HasPendingSettings =>
        PendingMaxTokens != MaxTokens ||
        PendingMemoryReservedMb != MemoryReservedMb ||
        !string.Equals(SelectedMode, _appliedMode, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private Task ToggleActive()
    {
        var snapshot = _services.Library.SetInstanceActive(InstanceId, !IsActive);
        if (snapshot is not null)
        {
            Apply(snapshot, syncPending: true);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task Reload()
    {
        var snapshot = await _services.Library.ReloadInstanceAsync(InstanceId);
        if (snapshot is not null)
        {
            Apply(snapshot, syncPending: true);
        }
    }

    [RelayCommand]
    private void Delete()
    {
        _services.Library.DeleteInstance(InstanceId);
    }

    [RelayCommand]
    private void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    [RelayCommand]
    private void ToggleConsoleExpanded()
    {
        IsConsoleExpanded = !IsConsoleExpanded;
    }

    [RelayCommand]
    private void TogglePin()
    {
        var snapshot = _services.Library.PinInstance(InstanceId, !IsPinned);
        if (snapshot is not null)
        {
            Apply(snapshot, syncPending: true);
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        _suppressUpdates = true;
        SelectedMode = _appliedMode;
        PendingMaxTokens = MaxTokens;
        PendingMemoryReservedMb = MemoryReservedMb;
        _suppressUpdates = false;
        NotifySettingsChanged();
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var workingSnapshot = _services.Library.GetLoadedInstances()
            .FirstOrDefault(snapshot => string.Equals(snapshot.InstanceId, InstanceId, StringComparison.OrdinalIgnoreCase));

        if (!string.Equals(SelectedMode, _appliedMode, StringComparison.OrdinalIgnoreCase))
        {
            workingSnapshot = _services.Library.UpdateInstanceMode(InstanceId, SelectedMode) ?? workingSnapshot;
        }

        if (PendingMaxTokens != MaxTokens)
        {
            workingSnapshot = _services.Library.UpdateInstanceTokens(InstanceId, PendingMaxTokens) ?? workingSnapshot;
        }

        if (PendingMemoryReservedMb != MemoryReservedMb)
        {
            workingSnapshot = _services.Library.UpdateInstanceMemoryCapacity(InstanceId, PendingMemoryReservedMb) ?? workingSnapshot;
        }

        if (workingSnapshot is not null)
        {
            Apply(workingSnapshot, syncPending: true);
        }
    }

    partial void OnIsActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(ActiveLabel));
        OnPropertyChanged(nameof(ActivityBadgeText));
        OnPropertyChanged(nameof(CompactStatusText));
        OnPropertyChanged(nameof(PrimaryActionGlyph));
        OnPropertyChanged(nameof(PrimaryActionToolTip));
    }

    partial void OnIsPinnedChanged(bool value)
    {
        OnPropertyChanged(nameof(PinLabel));
        OnPropertyChanged(nameof(PinGlyph));
        OnPropertyChanged(nameof(PinToolTip));
    }

    partial void OnSelectedModeChanged(string value)
    {
        NotifySettingsChanged();
    }

    partial void OnMemoryReservedMbChanged(long value)
    {
        OnPropertyChanged(nameof(MemoryText));
        OnPropertyChanged(nameof(MemoryAllocatedText));
        OnPropertyChanged(nameof(MemoryUsagePercent));
        OnPropertyChanged(nameof(MemoryUsageRatio));
        OnPropertyChanged(nameof(HasMemoryReservation));
        OnPropertyChanged(nameof(HasPendingSettings));
    }

    partial void OnMemoryActualMbChanged(long value)
    {
        OnPropertyChanged(nameof(MemoryText));
        OnPropertyChanged(nameof(MemoryUsedText));
        OnPropertyChanged(nameof(MemoryUsagePercent));
        OnPropertyChanged(nameof(MemoryUsageRatio));
    }

    partial void OnPendingMaxTokensChanged(int value)
    {
        NotifySettingsChanged();
    }

    partial void OnPendingMemoryReservedMbChanged(long value)
    {
        NotifySettingsChanged();
    }

    partial void OnHealthStatusChanged(string value)
    {
        OnPropertyChanged(nameof(HasHealthStatus));
        OnPropertyChanged(nameof(HealthSummary));
        OnPropertyChanged(nameof(CompactStatusText));
    }

    partial void OnHealthReasonChanged(string value)
    {
        OnPropertyChanged(nameof(HealthSummary));
    }

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ExpandGlyph));
        OnPropertyChanged(nameof(ExpandToolTip));
    }

    partial void OnIsConsoleExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ConsoleExpandGlyph));
        OnPropertyChanged(nameof(ConsoleToggleToolTip));
    }

    public void CommitPendingMaxTokens()
    {
        if (!HasPendingSettings || PendingMaxTokens == MaxTokens)
        {
            return;
        }

        var snapshot = _services.Library.UpdateInstanceTokens(InstanceId, PendingMaxTokens);
        if (snapshot is not null)
        {
            Apply(snapshot, syncPending: true);
        }
    }

    public void CommitPendingMemoryCapacity()
    {
        if (!HasPendingSettings || PendingMemoryReservedMb == MemoryReservedMb)
        {
            return;
        }

        var snapshot = _services.Library.UpdateInstanceMemoryCapacity(InstanceId, PendingMemoryReservedMb);
        if (snapshot is not null)
        {
            Apply(snapshot, syncPending: true);
        }
    }

    public void UpdateSnapshot(LoadedInstanceSnapshot snapshot)
    {
        Apply(snapshot, syncPending: false);
    }

    public void UpdateConsole(LoadedInstanceSnapshot snapshot)
    {
        ConsoleText = BuildConsoleText(snapshot);
    }

    private void Apply(LoadedInstanceSnapshot snapshot, bool syncPending)
    {
        _suppressUpdates = true;
        IsActive = snapshot.IsActive;
        IsPinned = snapshot.IsPinned;
        _appliedMode = snapshot.PrimaryMode;
        SelectedMode = snapshot.PrimaryMode;
        MemoryReservedMb = snapshot.MemoryReservedMb;
        MemoryActualMb = snapshot.MemoryActualMb;
        MaxTokens = snapshot.MaxTokens;
        if (syncPending)
        {
            PendingMaxTokens = snapshot.MaxTokens;
            PendingMemoryReservedMb = snapshot.MemoryReservedMb;
        }
        HealthStatus = snapshot.HealthStatus;
        HealthReason = snapshot.HealthReason;
        ConsoleText = BuildConsoleText(snapshot);
        _suppressUpdates = false;
        NotifySettingsChanged();
    }

    private void NotifySettingsChanged()
    {
        if (_suppressUpdates)
        {
            return;
        }

        OnPropertyChanged(nameof(HasPendingSettings));
        OnPropertyChanged(nameof(CompactStatusText));
    }

    private static string BuildConsoleText(LoadedInstanceSnapshot snapshot)
    {
        var consoleLines = snapshot.ConsoleEntries
            .Select(entry => $"[{entry.TimestampUtc:HH:mm:ss}] {entry.Source}: {entry.Text}")
            .ToList();

        if (HasDiagnosticHealthReason(snapshot.HealthReason))
        {
            if (consoleLines.Count > 0)
            {
                consoleLines.Add(string.Empty);
            }

            consoleLines.Add("[health] verification diagnostics:");
            consoleLines.AddRange(snapshot.HealthReason
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(line => line.TrimEnd()));
        }

        return string.Join("\n", consoleLines);
    }

    private static string GetInlineHealthReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || HasDiagnosticHealthReason(reason))
        {
            return string.Empty;
        }

        return reason.Trim();
    }

    private static bool HasDiagnosticHealthReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        var trimmed = reason.Trim();
        if (trimmed.Contains('\n') || trimmed.Contains('\r'))
        {
            return true;
        }

        if (trimmed.Length > 120)
        {
            return true;
        }

        return trimmed.Contains("ggml_", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("llama_", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("tensor", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("metadata", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("kv ", StringComparison.OrdinalIgnoreCase);
    }
}
