using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.ViewModels;

public sealed class SystemColorSummaryViewModel
{
    public SystemColorSummaryViewModel(string name, string hex)
    {
        Name = name;
        Hex = hex;
        Brush = new SolidColorBrush(Color.Parse(hex));
    }

    public string Name { get; }
    public string Hex { get; }
    public IBrush Brush { get; }
}

public partial class SystemSettingsViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly ObservableCollection<SystemColorSummaryViewModel> _semanticColors = new();

    public ObservableCollection<SystemColorSummaryViewModel> SemanticColors => _semanticColors;

    [ObservableProperty]
    private string _settingsPath = string.Empty;

    [ObservableProperty]
    private string _schemaVersion = string.Empty;

    [ObservableProperty]
    private string _overlayOpacity = string.Empty;

    [ObservableProperty]
    private string _contrastRatio = string.Empty;

    [ObservableProperty]
    private string _themeSummary = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public SystemSettingsViewModel(AppServices services)
    {
        _services = services;
        _services.UiStyleSettings.Changed += OnServiceChanged;
        SyncFromService();
    }

    private void OnServiceChanged()
    {
        SyncFromService();
    }

    private void SyncFromService()
    {
        var snapshot = _services.UiStyleSettings.GetSnapshot();
        SettingsPath = _services.Paths.UiStyleSettingsPath;
        SchemaVersion = UiStyleSettingsDefaults.SchemaVersion.ToString();
        OverlayOpacity = $"{snapshot.OverlayOpacityPercent:F0}%";
        ContrastRatio = $"{snapshot.ContrastRatio:F1}:1";
        ThemeSummary = $"{snapshot.CoreHex} / {snapshot.ContentHex} / {snapshot.HighlightHex}";
        StatusText = "Read-only diagnostics and semantic color tokens.";

        _semanticColors.Clear();
        _semanticColors.Add(new SystemColorSummaryViewModel("Success", "#9ECE6A"));
        _semanticColors.Add(new SystemColorSummaryViewModel("Warning", "#E0AF68"));
        _semanticColors.Add(new SystemColorSummaryViewModel("Error", "#F7768E"));
    }
}
