using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.ViewModels;

public partial class ThemeSettingsViewModel : ViewModelBase
{
    private readonly UiStyleSettingsService _service;
    private bool _isSyncing;

    public ObservableCollection<string> CompressionOptions { get; } = new();
    public ObservableCollection<string> SignalOptions { get; } = new();

    [ObservableProperty]
    private string _selectedCompressionPreset = UiCompressionPreset.Balanced.ToString();

    [ObservableProperty]
    private string _selectedSignalPreset = UiSignalPreset.Balanced.ToString();

    [ObservableProperty]
    private string _coreHex = UiStyleSettingsDefaults.CoreHex;

    [ObservableProperty]
    private string _contentHex = UiStyleSettingsDefaults.ContentHex;

    [ObservableProperty]
    private string _highlightHex = UiStyleSettingsDefaults.HighlightHex;

    [ObservableProperty]
    private Color _coreColor = Color.Parse(UiStyleSettingsDefaults.CoreHex);

    [ObservableProperty]
    private Color _contentColor = Color.Parse(UiStyleSettingsDefaults.ContentHex);

    [ObservableProperty]
    private Color _highlightColor = Color.Parse(UiStyleSettingsDefaults.HighlightHex);

    [ObservableProperty]
    private IBrush _coreBrush = new SolidColorBrush(Color.Parse(UiStyleSettingsDefaults.CoreHex));

    [ObservableProperty]
    private IBrush _contentBrush = new SolidColorBrush(Color.Parse(UiStyleSettingsDefaults.ContentHex));

    [ObservableProperty]
    private IBrush _highlightBrush = new SolidColorBrush(Color.Parse(UiStyleSettingsDefaults.HighlightHex));

    [ObservableProperty]
    private bool _corePickerOpen;

    [ObservableProperty]
    private bool _contentPickerOpen;

    [ObservableProperty]
    private bool _highlightPickerOpen;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public ThemeSettingsViewModel(AppServices services)
    {
        _service = services.UiStyleSettings;
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

    partial void OnCoreHexChanged(string value) => ApplyThemeSettings();
    partial void OnContentHexChanged(string value) => ApplyThemeSettings();
    partial void OnHighlightHexChanged(string value) => ApplyThemeSettings();

    partial void OnCoreColorChanged(Color value) => ApplyThemeColorSettings();
    partial void OnContentColorChanged(Color value) => ApplyThemeColorSettings();
    partial void OnHighlightColorChanged(Color value) => ApplyThemeColorSettings();

    [RelayCommand]
    private void ResetTheme()
    {
        _service.ResetThemeSettings();
    }

    [RelayCommand]
    private void ResetCoreColor()
    {
        _service.SetThemeSettings(UiStyleSettingsDefaults.CoreHex, ContentHex, HighlightHex);
    }

    [RelayCommand]
    private void ResetContentColor()
    {
        _service.SetThemeSettings(CoreHex, UiStyleSettingsDefaults.ContentHex, HighlightHex);
    }

    [RelayCommand]
    private void ResetHighlightColor()
    {
        _service.SetThemeSettings(CoreHex, ContentHex, UiStyleSettingsDefaults.HighlightHex);
    }

    private void OnServiceChanged()
    {
        SyncFromService();
    }

    private void ApplyThemeSettings()
    {
        if (_isSyncing)
        {
            return;
        }

        var invalidFields = new List<string>();
        if (!UiStyleSettingsService.TryNormalizeHex(CoreHex, out _))
        {
            invalidFields.Add("Core");
        }

        if (!UiStyleSettingsService.TryNormalizeHex(ContentHex, out _))
        {
            invalidFields.Add("Content");
        }

        if (!UiStyleSettingsService.TryNormalizeHex(HighlightHex, out _))
        {
            invalidFields.Add("Highlight");
        }

        _service.SetThemeSettings(CoreHex, ContentHex, HighlightHex);
        UpdateStatus(_service.GetSnapshot(), invalidFields);
    }

    private void ApplyThemeColorSettings()
    {
        if (_isSyncing)
        {
            return;
        }

        _service.SetThemeSettings(ToHex(CoreColor), ToHex(ContentColor), ToHex(HighlightColor));
    }

    private void SyncFromService()
    {
        _isSyncing = true;
        var snapshot = _service.GetSnapshot();
        SelectedCompressionPreset = snapshot.CompressionPreset.ToString();
        SelectedSignalPreset = snapshot.SignalPreset.ToString();
        CoreHex = snapshot.CoreHex;
        ContentHex = snapshot.ContentHex;
        HighlightHex = snapshot.HighlightHex;
        CoreColor = Color.Parse(snapshot.CoreHex);
        ContentColor = Color.Parse(snapshot.ContentHex);
        HighlightColor = Color.Parse(snapshot.HighlightHex);
        CoreBrush = new SolidColorBrush(CoreColor);
        ContentBrush = new SolidColorBrush(ContentColor);
        HighlightBrush = new SolidColorBrush(HighlightColor);
        UpdateStatus(snapshot, Array.Empty<string>());
        _isSyncing = false;
    }

    private void UpdateStatus(UiStyleSettingsSnapshot snapshot, IReadOnlyCollection<string> invalidFields)
    {
        if (invalidFields.Count > 0)
        {
            StatusText = $"Invalid hex for {string.Join(", ", invalidFields)}. Use values like #20283A.";
            return;
        }

        if (snapshot.ContrastRatio < 4.5)
        {
            StatusText = $"Contrast warning: panel text is {snapshot.ContrastRatio:F1}:1. Aim for 4.5:1 or higher.";
            return;
        }

        StatusText = $"Theme live. Panel text contrast is {snapshot.ContrastRatio:F1}:1.";
    }

    private static string ToHex(Color color)
    {
        return color.A == byte.MaxValue
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
