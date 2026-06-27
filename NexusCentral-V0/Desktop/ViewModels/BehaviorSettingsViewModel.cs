using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.ViewModels;

public partial class BehaviorSettingsViewModel : ViewModelBase
{
    private readonly UiStyleSettingsService _service;
    private bool _isSyncing;

    public ObservableCollection<string> DensityOptions { get; } = new();
    public ObservableCollection<string> CompressionOptions { get; } = new();
    public ObservableCollection<string> SignalOptions { get; } = new();

    [ObservableProperty]
    private string _selectedDensityPreset = UiDensityPreset.Default.ToString();

    [ObservableProperty]
    private string _selectedCompressionPreset = UiCompressionPreset.Balanced.ToString();

    [ObservableProperty]
    private string _selectedSignalPreset = UiSignalPreset.Balanced.ToString();

    [ObservableProperty]
    private string _statusText = string.Empty;

    public BehaviorSettingsViewModel(AppServices services)
    {
        _service = services.UiStyleSettings;

        foreach (var option in _service.Presets)
        {
            DensityOptions.Add(option.ToString());
        }

        foreach (var option in _service.CompressionPresets)
        {
            CompressionOptions.Add(option.ToString());
        }

        foreach (var option in _service.SignalPresets)
        {
            SignalOptions.Add(option.ToString());
        }

        _service.Changed += OnServiceChanged;
        SyncFromService();
    }

    partial void OnSelectedDensityPresetChanged(string value)
    {
        if (_isSyncing)
        {
            return;
        }

        if (Enum.TryParse<UiDensityPreset>(value, ignoreCase: true, out var preset))
        {
            _service.SetDensityPreset(preset);
        }
    }

    partial void OnSelectedCompressionPresetChanged(string value)
    {
        if (_isSyncing)
        {
            return;
        }

        if (Enum.TryParse<UiCompressionPreset>(value, ignoreCase: true, out var preset))
        {
            _service.SetCompressionPreset(preset);
        }
    }

    partial void OnSelectedSignalPresetChanged(string value)
    {
        if (_isSyncing)
        {
            return;
        }

        if (Enum.TryParse<UiSignalPreset>(value, ignoreCase: true, out var preset))
        {
            _service.SetSignalPreset(preset);
        }
    }

    private void OnServiceChanged()
    {
        SyncFromService();
    }

    private void SyncFromService()
    {
        _isSyncing = true;
        var snapshot = _service.GetSnapshot();
        SelectedDensityPreset = snapshot.DensityPreset.ToString();
        SelectedCompressionPreset = snapshot.CompressionPreset.ToString();
        SelectedSignalPreset = snapshot.SignalPreset.ToString();
        StatusText = $"Density shapes spacing, compression shapes affordances, and signal shapes emphasis.";
        _isSyncing = false;
    }
}
