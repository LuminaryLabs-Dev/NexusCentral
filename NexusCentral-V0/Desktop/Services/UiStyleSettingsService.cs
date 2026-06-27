using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NexusCentral_Avalonia.Services;

public enum UiDensityPreset
{
    Default,
    Compact,
    Comfortable,
}

public enum UiCompressionPreset
{
    Compressed,
    Balanced,
    Comfortable,
}

public enum UiSignalPreset
{
    Subtle,
    Balanced,
    Strong,
}

internal static class UiStyleSettingsDefaults
{
    public const int SchemaVersion = 3;
    public const string CoreHex = "#20283A";
    public const string ContentHex = "#C7D2F4";
    public const string HighlightHex = "#7AA2F7";
    public const double OverlayOpacityPercent = 60.0;
    public const string CompressionPreset = nameof(UiCompressionPreset.Balanced);
    public const string SignalPreset = nameof(UiSignalPreset.Balanced);
}

internal static class UiStyleSettingsLegacyDefaults
{
    public const string GradientStartHex = "#101624";
    public const string GradientEndHex = "#1C2640";
    public const double GradientAngleDegrees = 135.0;
}

public sealed class UiStyleSettingsService
{
    private readonly AppPathsService _paths;
    private readonly LogService _logs;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private UiStyleSettingsDocument _settings = new();

    public UiStyleSettingsService(AppPathsService paths, LogService logs)
    {
        _paths = paths;
        _logs = logs;
        Load();
    }

    public event Action? Changed;

    public UiDensityPreset CurrentPreset
    {
        get
        {
            lock (_lock)
            {
                return ParsePreset(_settings.DensityPreset);
            }
        }
    }

    public IReadOnlyList<UiDensityPreset> Presets { get; } = Enum.GetValues<UiDensityPreset>();
    public IReadOnlyList<UiCompressionPreset> CompressionPresets { get; } = Enum.GetValues<UiCompressionPreset>();
    public IReadOnlyList<UiSignalPreset> SignalPresets { get; } = Enum.GetValues<UiSignalPreset>();

    public UiCompressionPreset CurrentCompressionPreset
    {
        get
        {
            lock (_lock)
            {
                return ParseCompressionPreset(_settings.CompressionPreset, _settings.DensityPreset);
            }
        }
    }

    public UiSignalPreset CurrentSignalPreset
    {
        get
        {
            lock (_lock)
            {
                return ParseSignalPreset(_settings.SignalPreset);
            }
        }
    }

    public void InitializeResources()
    {
        ApplyResources(CurrentCompressionPreset, CurrentSignalPreset);
    }

    public UiDensityPreset SetDensityPreset(UiDensityPreset preset)
    {
        var changed = false;
        lock (_lock)
        {
            var nextValue = preset.ToString();
            if (!string.Equals(_settings.DensityPreset, nextValue, StringComparison.Ordinal))
            {
                _settings.DensityPreset = nextValue;
                _settings.CompressionPreset = MapDensityToCompression(preset).ToString();
                PersistUnsafe();
                changed = true;
            }
        }

        if (changed)
        {
            ApplyResources(CurrentCompressionPreset, CurrentSignalPreset);
            Changed?.Invoke();
        }

        return preset;
    }

    public void SetThemeSettings(string? coreHex, string? contentHex, string? highlightHex)
    {
        var changed = false;

        lock (_lock)
        {
            if (TryNormalizeHex(coreHex, out var normalizedCore) &&
                !string.Equals(_settings.CoreHex, normalizedCore, StringComparison.OrdinalIgnoreCase))
            {
                _settings.CoreHex = normalizedCore;
                changed = true;
            }

            if (TryNormalizeHex(contentHex, out var normalizedContent) &&
                !string.Equals(_settings.ContentHex, normalizedContent, StringComparison.OrdinalIgnoreCase))
            {
                _settings.ContentHex = normalizedContent;
                changed = true;
            }

            if (TryNormalizeHex(highlightHex, out var normalizedHighlight) &&
                !string.Equals(_settings.HighlightHex, normalizedHighlight, StringComparison.OrdinalIgnoreCase))
            {
                _settings.HighlightHex = normalizedHighlight;
                changed = true;
            }

            if (changed)
            {
                PersistUnsafe();
            }
        }

        if (changed)
        {
            ApplyResources(CurrentCompressionPreset, CurrentSignalPreset);
            Changed?.Invoke();
        }
    }

    public UiCompressionPreset SetCompressionPreset(UiCompressionPreset preset)
    {
        var changed = false;
        lock (_lock)
        {
            var nextValue = preset.ToString();
            if (!string.Equals(_settings.CompressionPreset, nextValue, StringComparison.Ordinal))
            {
                _settings.CompressionPreset = nextValue;
                _settings.DensityPreset = MapCompressionToDensity(preset).ToString();
                PersistUnsafe();
                changed = true;
            }
        }

        if (changed)
        {
            ApplyResources(CurrentCompressionPreset, CurrentSignalPreset);
            Changed?.Invoke();
        }

        return preset;
    }

    public UiSignalPreset SetSignalPreset(UiSignalPreset preset)
    {
        var changed = false;
        lock (_lock)
        {
            var nextValue = preset.ToString();
            if (!string.Equals(_settings.SignalPreset, nextValue, StringComparison.Ordinal))
            {
                _settings.SignalPreset = nextValue;
                PersistUnsafe();
                changed = true;
            }
        }

        if (changed)
        {
            ApplyResources(CurrentCompressionPreset, CurrentSignalPreset);
            Changed?.Invoke();
        }

        return preset;
    }

    public void ResetThemeSettings()
    {
        lock (_lock)
        {
            _settings.SchemaVersion = UiStyleSettingsDefaults.SchemaVersion;
            _settings.DensityPreset = string.IsNullOrWhiteSpace(_settings.DensityPreset)
                ? UiDensityPreset.Default.ToString()
                : ParsePreset(_settings.DensityPreset).ToString();
            _settings.CompressionPreset = string.IsNullOrWhiteSpace(_settings.CompressionPreset)
                ? UiStyleSettingsDefaults.CompressionPreset
                : ParseCompressionPreset(_settings.CompressionPreset, _settings.DensityPreset).ToString();
            _settings.SignalPreset = string.IsNullOrWhiteSpace(_settings.SignalPreset)
                ? UiStyleSettingsDefaults.SignalPreset
                : ParseSignalPreset(_settings.SignalPreset).ToString();
            _settings.CoreHex = UiStyleSettingsDefaults.CoreHex;
            _settings.ContentHex = UiStyleSettingsDefaults.ContentHex;
            _settings.HighlightHex = UiStyleSettingsDefaults.HighlightHex;
            _settings.OverlayOpacityPercent = Clamp(
                _settings.OverlayOpacityPercent,
                20.0,
                100.0,
                UiStyleSettingsDefaults.OverlayOpacityPercent);
            ClearLegacyGradientFieldsUnsafe();
            PersistUnsafe();
        }

        ApplyResources(CurrentCompressionPreset, CurrentSignalPreset);
        Changed?.Invoke();
    }

    public UiStyleSettingsSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var coreHex = TryNormalizeHex(_settings.CoreHex, out var normalizedCore)
                ? normalizedCore
                : UiStyleSettingsDefaults.CoreHex;
            var contentHex = TryNormalizeHex(_settings.ContentHex, out var normalizedContent)
                ? normalizedContent
                : UiStyleSettingsDefaults.ContentHex;
            var highlightHex = TryNormalizeHex(_settings.HighlightHex, out var normalizedHighlight)
                ? normalizedHighlight
                : UiStyleSettingsDefaults.HighlightHex;
            var core = ParseColor(coreHex, ParseColor(UiStyleSettingsDefaults.CoreHex, Colors.Black));
            var content = ParseColor(contentHex, ParseColor(UiStyleSettingsDefaults.ContentHex, Colors.White));
            var panel = Darken(core, 0.06);

            return new UiStyleSettingsSnapshot(
                ParsePreset(_settings.DensityPreset),
                ParseCompressionPreset(_settings.CompressionPreset, _settings.DensityPreset),
                ParseSignalPreset(_settings.SignalPreset),
                coreHex,
                contentHex,
                highlightHex,
                Clamp(_settings.OverlayOpacityPercent, 20.0, 100.0, UiStyleSettingsDefaults.OverlayOpacityPercent),
                CalculateContrastRatio(content, panel));
        }
    }

    public static bool TryNormalizeHex(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith('#'))
        {
            trimmed = $"#{trimmed}";
        }

        try
        {
            var color = Color.Parse(trimmed);
            normalized = FormatColorHex(color);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_paths.UiStyleSettingsPath))
            {
                var parsed = JsonSerializer.Deserialize<UiStyleSettingsDocument>(File.ReadAllText(_paths.UiStyleSettingsPath));
                if (parsed is not null)
                {
                    _settings = parsed;
                }
            }
        }
        catch (Exception ex)
        {
            _logs.Append("warning", "ui-style", $"Could not load UI style settings. {ex.Message}");
        }

        lock (_lock)
        {
            MigrateAndNormalizeUnsafe();
            PersistUnsafe();
        }
    }

    private void MigrateAndNormalizeUnsafe()
    {
        _settings.SchemaVersion = UiStyleSettingsDefaults.SchemaVersion;
        var densityPreset = ParsePreset(_settings.DensityPreset);
        _settings.DensityPreset = densityPreset.ToString();
        _settings.CompressionPreset = ParseCompressionPreset(_settings.CompressionPreset, _settings.DensityPreset).ToString();
        _settings.SignalPreset = ParseSignalPreset(_settings.SignalPreset).ToString();
        _settings.OverlayOpacityPercent = Clamp(
            _settings.OverlayOpacityPercent,
            20.0,
            100.0,
            UiStyleSettingsDefaults.OverlayOpacityPercent);

        if (!TryNormalizeHex(_settings.CoreHex, out var normalizedCore))
        {
            normalizedCore = DeriveCoreFromLegacyGradientUnsafe();
        }

        if (!TryNormalizeHex(_settings.ContentHex, out var normalizedContent))
        {
            normalizedContent = UiStyleSettingsDefaults.ContentHex;
        }

        if (!TryNormalizeHex(_settings.HighlightHex, out var normalizedHighlight))
        {
            normalizedHighlight = UiStyleSettingsDefaults.HighlightHex;
        }

        _settings.CoreHex = normalizedCore;
        _settings.ContentHex = normalizedContent;
        _settings.HighlightHex = normalizedHighlight;

        ClearLegacyGradientFieldsUnsafe();
    }

    private string DeriveCoreFromLegacyGradientUnsafe()
    {
        if (TryNormalizeHex(_settings.GradientStartHex, out var startHex) &&
            TryNormalizeHex(_settings.GradientEndHex, out var endHex))
        {
            var start = ParseColor(startHex, ParseColor(UiStyleSettingsDefaults.CoreHex, Colors.Black));
            var end = ParseColor(endHex, ParseColor(UiStyleSettingsDefaults.CoreHex, Colors.Black));
            return FormatColorHex(Midpoint(start, end));
        }

        return UiStyleSettingsDefaults.CoreHex;
    }

    private void ClearLegacyGradientFieldsUnsafe()
    {
        _settings.GradientStartHex = null;
        _settings.GradientEndHex = null;
        _settings.GradientAngleDegrees = null;
    }

    private void PersistUnsafe()
    {
        File.WriteAllText(_paths.UiStyleSettingsPath, JsonSerializer.Serialize(_settings, _jsonOptions));
    }

    private void ApplyResources(UiCompressionPreset compressionPreset, UiSignalPreset signalPreset)
    {
        if (Application.Current is null)
        {
            return;
        }

        var snapshot = GetSnapshot();
        var core = ParseColor(snapshot.CoreHex, ParseColor(UiStyleSettingsDefaults.CoreHex, Colors.Black));
        var content = ParseColor(snapshot.ContentHex, ParseColor(UiStyleSettingsDefaults.ContentHex, Colors.White));
        var highlight = ParseColor(snapshot.HighlightHex, ParseColor(UiStyleSettingsDefaults.HighlightHex, Colors.SkyBlue));

        var bg = Darken(core, 0.20);
        var shell = Darken(core, 0.18);
        var surface = Darken(core, 0.10);
        var panel = Darken(core, 0.06);
        var panelAlt = core;
        var input = Darken(core, 0.14);
        var surfaceHighlight = Blend(core, highlight, 0.12);
        var secondary = Blend(core, content, 0.48);
        var divider = Blend(core, content, 0.24);
        var dividerStrong = Blend(core, content, 0.38);
        var accent = highlight;
        var accentHover = Lighten(highlight, 0.08);
        var selectionFill = Blend(highlight, core, 0.35);
        var fg = content;
        var fgBright = Lighten(content, 0.08);
        var fgDim = Blend(content, core, 0.25);
        var fgMuted = Blend(content, core, 0.40);
        var success = ParseColor("#9ECE6A", Colors.LightGreen);
        var warning = ParseColor("#E0AF68", Colors.Goldenrod);
        var error = ParseColor("#F7768E", Colors.IndianRed);

        var resources = Application.Current.Resources;
        resources["UiShellBackgroundBrush"] = BuildShellBackgroundBrush(Lighten(core, 0.08), Darken(core, 0.12));
        ApplyDerivedSurfaceBrushes(resources, core, content, highlight, bg, shell, surface, panel, panelAlt, input, warning, success);

        SetSolidToken(resources, "TokyoBG", bg);
        SetOverlayToken(resources, "TokyoShell", shell, GetOverlayAlpha(0.90));
        SetOverlayToken(resources, "TokyoSurface", surface, GetOverlayAlpha(0.82));
        SetOverlayToken(resources, "TokyoPanel", panel, GetOverlayAlpha(0.86));
        SetOverlayToken(resources, "TokyoPanelAlt", panelAlt, GetOverlayAlpha(0.92));
        SetSolidToken(resources, "TokyoSecondary", secondary);
        SetSolidToken(resources, "TokyoDivider", divider);
        SetSolidToken(resources, "TokyoDividerStrong", dividerStrong);
        SetSolidToken(resources, "TokyoAccent", accent);
        SetSolidToken(resources, "TokyoAccentHover", accentHover);
        SetSolidToken(resources, "TokyoSelectionFill", selectionFill);
        SetSolidToken(resources, "TokyoFG", fg);
        SetSolidToken(resources, "TokyoFGBright", fgBright);
        SetSolidToken(resources, "TokyoFGDim", fgDim);
        SetSolidToken(resources, "TokyoFGMuted", fgMuted);
        SetOverlayToken(resources, "TokyoInput", input, GetOverlayAlpha(0.88));
        SetSolidToken(resources, "TokyoSuccess", success);
        SetSolidToken(resources, "TokyoWarning", warning);
        SetSolidToken(resources, "TokyoError", error);
        resources["TokyoSurfaceHighlightBrush"] = BuildOverlayBrush(surfaceHighlight, GetOverlayAlpha(0.96));
        resources["TokyoThinkingRailBrush"] = BuildThinkingRailBrush(divider);
        resources["TokyoACCENTBrush"] = new SolidColorBrush(accent);
        resources["TokyoDangerBrush"] = new SolidColorBrush(error);
        resources["TokyoFGBrighterBrush"] = new SolidColorBrush(fgBright);

        resources["UiSectionMetaBrush"] = new SolidColorBrush(GetSectionMetaColor(content, core, signalPreset));
        resources["UiFieldLabelBrush"] = new SolidColorBrush(GetFieldLabelColor(content, core, signalPreset));
        resources["UiInlineLabelBrush"] = new SolidColorBrush(GetInlineLabelColor(content, core, signalPreset));
        resources["UiRoleLabelBrush"] = new SolidColorBrush(GetRoleLabelColor(content, signalPreset));

        var values = GetValues(compressionPreset, signalPreset);
        resources["UiButtonPadding"] = values.ButtonPadding;
        resources["UiButtonMinHeight"] = values.ButtonMinHeight;
        resources["UiToggleButtonPadding"] = values.ToggleButtonPadding;
        resources["UiToggleButtonMinHeight"] = values.ToggleButtonMinHeight;
        resources["UiTextBoxPadding"] = values.TextBoxPadding;
        resources["UiTextBoxMinHeight"] = values.TextBoxMinHeight;
        resources["UiComboBoxPadding"] = values.ComboBoxPadding;
        resources["UiComboBoxMinHeight"] = values.ComboBoxMinHeight;
        resources["UiNumericUpDownMinHeight"] = values.NumericUpDownMinHeight;
        resources["UiNavTabPadding"] = values.NavTabPadding;
        resources["UiNavTabMinHeight"] = values.NavTabMinHeight;
        resources["UiPaneStackSpacing"] = values.PaneStackSpacing;
        resources["UiSplitWorkspaceGutter"] = values.SplitWorkspaceGutter;
        resources["UiSectionHeaderPadding"] = values.SectionHeaderPadding;
        resources["UiSectionHeaderCompactPadding"] = values.SectionHeaderCompactPadding;
        resources["UiSectionBodyPadding"] = values.SectionBodyPadding;
        resources["UiSectionBodySubtlePadding"] = values.SectionBodySubtlePadding;
        resources["UiSubSectionPadding"] = values.SubSectionPadding;
        resources["UiInlineSectionPadding"] = values.InlineSectionPadding;
        resources["UiPageHeaderPadding"] = values.PageHeaderPadding;
        resources["UiToolbarStripPadding"] = values.ToolbarStripPadding;
        resources["UiComposerBarPadding"] = values.ComposerBarPadding;
        resources["UiPaneSectionPadding"] = values.PaneSectionPadding;
        resources["UiCompactActionPadding"] = values.CompactActionPadding;
        resources["UiCompactActionMinHeight"] = values.CompactActionMinHeight;
        resources["UiFlatButtonPadding"] = values.FlatButtonPadding;
        resources["UiFlatButtonMinHeight"] = values.FlatButtonMinHeight;
        resources["UiInlineBadgePadding"] = values.InlineBadgePadding;
        resources["UiInlineFieldPadding"] = values.InlineFieldPadding;
        resources["UiBannerClusterPadding"] = values.BannerClusterPadding;
        resources["UiInspectorGroupPadding"] = values.InspectorGroupPadding;
        resources["UiInspectorGroupMargin"] = values.InspectorGroupMargin;
        resources["UiTranscriptRowPadding"] = values.TranscriptRowPadding;
        resources["UiWorkspaceSectionPadding"] = values.WorkspaceSectionPadding;
        resources["UiCompactComboPadding"] = values.CompactComboPadding;
        resources["UiCompactComboMinHeight"] = values.CompactComboMinHeight;
        resources["UiCompactComboFontSize"] = values.CompactComboFontSize;
        resources["UiHeaderActionSize"] = values.HeaderActionSize;
        resources["UiHeaderActionFontSize"] = values.HeaderActionFontSize;
        resources["UiIconButtonSize"] = values.IconButtonSize;
        resources["UiIconButtonFontSize"] = values.IconButtonFontSize;
        resources["UiToolbarControlWidthSmall"] = values.ToolbarControlWidthSmall;
        resources["UiToolbarControlWidthMedium"] = values.ToolbarControlWidthMedium;
        resources["UiToolbarControlWidthLarge"] = values.ToolbarControlWidthLarge;
        resources["UiInspectorLabelWidth"] = values.InspectorLabelWidth;
        resources["UiInspectorEditorMinWidth"] = values.InspectorEditorMinWidth;
        resources["UiInspectorCardWidth"] = values.InspectorCardWidth;
        resources["UiDiagnosticsMaxHeight"] = values.DiagnosticsMaxHeight;
        resources["UiLogPanelMaxHeight"] = values.LogPanelMaxHeight;
        resources["UiRunDetailsMaxHeight"] = values.RunDetailsMaxHeight;
        resources["UiCompactPreviewMaxHeight"] = values.CompactPreviewMaxHeight;
        resources["UiHeaderClusterGap"] = values.HeaderClusterGap;
        resources["UiInlineControlGap"] = values.InlineControlGap;
        resources["UiRoleLabelWidth"] = values.RoleLabelWidth;
        resources["UiRoleLabelFontSize"] = values.RoleLabelFontSize;
        resources["UiFieldLabelFontSize"] = values.FieldLabelFontSize;
        resources["UiInlineLabelFontSize"] = values.InlineLabelFontSize;
        resources["UiSectionMetaFontSize"] = values.SectionMetaFontSize;
        resources["UiSignalStandardBorderThickness"] = values.SignalStandardBorderThickness;
        resources["UiSignalEmphasisBorderThickness"] = values.SignalEmphasisBorderThickness;
    }

    private void ApplyDerivedSurfaceBrushes(
        IResourceDictionary resources,
        Color core,
        Color content,
        Color highlight,
        Color bg,
        Color shell,
        Color surface,
        Color panel,
        Color panelAlt,
        Color input,
        Color warning,
        Color success)
    {
        var paneAlpha = GetOverlayAlpha(0.92);
        var cardAlpha = GetOverlayAlpha(0.96);
        var controlAlpha = GetOverlayAlpha(0.90);
        var railBase = Blend(highlight, content, 0.12);
        var userRailBase = Blend(success, content, 0.08);

        resources["UiShellBackgroundBrush"] = BuildGradient(
            new RelativePoint(0, 1, RelativeUnit.Relative),
            new RelativePoint(1, 0, RelativeUnit.Relative),
            (Darken(core, 0.22), 0),
            (Darken(core, 0.16), 0.52),
            (Darken(Blend(core, bg, 0.35), 0.10), 1));

        resources["UiPaneGradientBrush"] = BuildGradient(
            new RelativePoint(0, 0, RelativeUnit.Relative),
            new RelativePoint(1, 1, RelativeUnit.Relative),
            (WithAlpha(panelAlt, paneAlpha), 0),
            (WithAlpha(panel, paneAlpha), 0.54),
            (WithAlpha(shell, paneAlpha), 1));

        resources["UiHeaderGradientBrush"] = BuildGradient(
            new RelativePoint(0, 0, RelativeUnit.Relative),
            new RelativePoint(1, 0, RelativeUnit.Relative),
            (WithAlpha(Lighten(panel, 0.02), paneAlpha), 0),
            (WithAlpha(panel, paneAlpha), 0.58),
            (WithAlpha(shell, paneAlpha), 1));

        resources["UiCardGradientBrush"] = BuildGradient(
            new RelativePoint(0, 0, RelativeUnit.Relative),
            new RelativePoint(0, 1, RelativeUnit.Relative),
            (WithAlpha(Lighten(panelAlt, 0.02), cardAlpha), 0),
            (WithAlpha(panel, cardAlpha), 0.62),
            (WithAlpha(surface, cardAlpha), 1));

        resources["UiCardSubtleGradientBrush"] = BuildGradient(
            new RelativePoint(0, 0, RelativeUnit.Relative),
            new RelativePoint(0, 1, RelativeUnit.Relative),
            (WithAlpha(panel, cardAlpha), 0),
            (WithAlpha(surface, cardAlpha), 1));

        resources["UiCardSelectedGradientBrush"] = BuildGradient(
            new RelativePoint(0, 0, RelativeUnit.Relative),
            new RelativePoint(1, 1, RelativeUnit.Relative),
            (WithAlpha(Blend(core, highlight, 0.24), byte.MaxValue), 0),
            (WithAlpha(Blend(panelAlt, highlight, 0.16), byte.MaxValue), 0.58),
            (WithAlpha(Blend(panel, highlight, 0.10), byte.MaxValue), 1));

        resources["UiControlGradientBrush"] = BuildGradient(
            new RelativePoint(0, 0, RelativeUnit.Relative),
            new RelativePoint(0, 1, RelativeUnit.Relative),
            (WithAlpha(input, controlAlpha), 0),
            (WithAlpha(Darken(input, 0.06), controlAlpha), 1));

        resources["ThoughtCardBackgroundBrush"] = BuildGradient(
            new RelativePoint(0, 0, RelativeUnit.Relative),
            new RelativePoint(0, 1, RelativeUnit.Relative),
            (WithAlpha(Blend(panelAlt, highlight, 0.18), cardAlpha), 0),
            (WithAlpha(surface, cardAlpha), 1));

        resources["ActionCardBackgroundBrush"] = BuildGradient(
            new RelativePoint(0, 0, RelativeUnit.Relative),
            new RelativePoint(0, 1, RelativeUnit.Relative),
            (WithAlpha(Blend(panelAlt, warning, 0.14), cardAlpha), 0),
            (WithAlpha(surface, cardAlpha), 1));

        resources["AiRailBrush"] = BuildGradient(
            new RelativePoint(0, 0, RelativeUnit.Relative),
            new RelativePoint(0, 1, RelativeUnit.Relative),
            (railBase, 0),
            (WithAlpha(highlight, 0), 1));

        resources["UserRailBrush"] = BuildGradient(
            new RelativePoint(0, 0, RelativeUnit.Relative),
            new RelativePoint(0, 1, RelativeUnit.Relative),
            (userRailBase, 0),
            (WithAlpha(success, 0), 1));
    }

    private static void SetSolidToken(IResourceDictionary resources, string tokenName, Color color)
    {
        resources[tokenName] = color;
        resources[$"{tokenName}Brush"] = new SolidColorBrush(color);
    }

    private static void SetOverlayToken(IResourceDictionary resources, string tokenName, Color color, byte alpha)
    {
        resources[tokenName] = color;
        resources[$"{tokenName}Brush"] = BuildOverlayBrush(color, alpha);
    }

    private static Brush BuildShellBackgroundBrush(Color startColor, Color endColor)
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(startColor, 0),
                new GradientStop(endColor, 1),
            },
        };
    }

    private static SolidColorBrush BuildOverlayBrush(Color baseColor, byte alpha)
    {
        return new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
    }

    private static Brush BuildThinkingRailBrush(Color baseColor)
    {
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.FromArgb(255, baseColor.R, baseColor.G, baseColor.B), 0),
                new GradientStop(Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B), 1),
            },
        };
    }

    private static Brush BuildGradient(
        RelativePoint startPoint,
        RelativePoint endPoint,
        params (Color Color, double Offset)[] stops)
    {
        var gradientStops = new GradientStops();
        foreach (var stop in stops)
        {
            gradientStops.Add(new GradientStop(stop.Color, stop.Offset));
        }

        return new LinearGradientBrush
        {
            StartPoint = startPoint,
            EndPoint = endPoint,
            GradientStops = gradientStops,
        };
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private byte GetOverlayAlpha(double opacityMultiplier)
    {
        lock (_lock)
        {
            var opacity = Clamp(_settings.OverlayOpacityPercent, 20.0, 100.0, UiStyleSettingsDefaults.OverlayOpacityPercent) / 100.0;
            var computed = Clamp(opacity * opacityMultiplier, 0.2, 1.0, opacity);
            return (byte)Math.Round(computed * 255.0);
        }
    }

    private static Color ParseColor(string? value, Color fallback)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            try
            {
                return Color.Parse(value);
            }
            catch
            {
            }
        }

        return fallback;
    }

    private static string FormatColorHex(Color color)
    {
        return color.A == byte.MaxValue
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static double Clamp(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return fallback;
        }

        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static UiDensityPreset ParsePreset(string? value)
    {
        return Enum.TryParse(value, ignoreCase: true, out UiDensityPreset preset)
            ? preset
            : UiDensityPreset.Default;
    }

    private static UiCompressionPreset ParseCompressionPreset(string? value, string? densityFallback)
    {
        if (Enum.TryParse(value, ignoreCase: true, out UiCompressionPreset preset))
        {
            return preset;
        }

        return MapDensityToCompression(ParsePreset(densityFallback));
    }

    private static UiSignalPreset ParseSignalPreset(string? value)
    {
        return Enum.TryParse(value, ignoreCase: true, out UiSignalPreset preset)
            ? preset
            : UiSignalPreset.Balanced;
    }

    private static UiCompressionPreset MapDensityToCompression(UiDensityPreset preset)
    {
        return preset switch
        {
            UiDensityPreset.Compact => UiCompressionPreset.Compressed,
            UiDensityPreset.Comfortable => UiCompressionPreset.Comfortable,
            _ => UiCompressionPreset.Balanced,
        };
    }

    private static UiDensityPreset MapCompressionToDensity(UiCompressionPreset preset)
    {
        return preset switch
        {
            UiCompressionPreset.Compressed => UiDensityPreset.Compact,
            UiCompressionPreset.Comfortable => UiDensityPreset.Comfortable,
            _ => UiDensityPreset.Default,
        };
    }

    private static Color GetSectionMetaColor(Color content, Color core, UiSignalPreset signalPreset)
    {
        return signalPreset switch
        {
            UiSignalPreset.Subtle => Blend(content, core, 0.40),
            UiSignalPreset.Strong => Blend(content, core, 0.26),
            _ => Blend(content, core, 0.33),
        };
    }

    private static Color GetFieldLabelColor(Color content, Color core, UiSignalPreset signalPreset)
    {
        return signalPreset switch
        {
            UiSignalPreset.Subtle => Blend(content, core, 0.25),
            UiSignalPreset.Strong => Blend(content, core, 0.10),
            _ => Blend(content, core, 0.16),
        };
    }

    private static Color GetInlineLabelColor(Color content, Color core, UiSignalPreset signalPreset)
    {
        return signalPreset switch
        {
            UiSignalPreset.Subtle => Blend(content, core, 0.28),
            UiSignalPreset.Strong => Blend(content, core, 0.12),
            _ => Blend(content, core, 0.18),
        };
    }

    private static Color GetRoleLabelColor(Color content, UiSignalPreset signalPreset)
    {
        return signalPreset switch
        {
            UiSignalPreset.Subtle => content,
            UiSignalPreset.Strong => Lighten(content, 0.08),
            _ => Lighten(content, 0.02),
        };
    }

    private static Color Midpoint(Color left, Color right)
    {
        return Color.FromRgb(
            (byte)((left.R + right.R) / 2),
            (byte)((left.G + right.G) / 2),
            (byte)((left.B + right.B) / 2));
    }

    private static Color Blend(Color first, Color second, double secondWeight)
    {
        var weight = Clamp(secondWeight, 0.0, 1.0, 0.0);
        var firstWeight = 1.0 - weight;
        return Color.FromRgb(
            (byte)Math.Round((first.R * firstWeight) + (second.R * weight)),
            (byte)Math.Round((first.G * firstWeight) + (second.G * weight)),
            (byte)Math.Round((first.B * firstWeight) + (second.B * weight)));
    }

    private static Color Lighten(Color color, double amount)
    {
        ToHsl(color, out var h, out var s, out var l);
        l = Clamp(l + amount, 0.0, 1.0, l);
        return FromHsl(h, s, l);
    }

    private static Color Darken(Color color, double amount)
    {
        ToHsl(color, out var h, out var s, out var l);
        l = Clamp(l - amount, 0.0, 1.0, l);
        return FromHsl(h, s, l);
    }

    private static void ToHsl(Color color, out double h, out double s, out double l)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        h = 0;
        s = 0;
        l = (max + min) / 2.0;

        if (Math.Abs(max - min) < 0.0001)
        {
            return;
        }

        var delta = max - min;
        s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

        if (Math.Abs(max - r) < 0.0001)
        {
            h = ((g - b) / delta) + (g < b ? 6 : 0);
        }
        else if (Math.Abs(max - g) < 0.0001)
        {
            h = ((b - r) / delta) + 2;
        }
        else
        {
            h = ((r - g) / delta) + 4;
        }

        h /= 6.0;
    }

    private static Color FromHsl(double h, double s, double l)
    {
        if (s <= 0.0001)
        {
            var channel = (byte)Math.Round(l * 255.0);
            return Color.FromRgb(channel, channel, channel);
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - (l * s);
        var p = 2 * l - q;
        var r = HueToRgb(p, q, h + (1.0 / 3.0));
        var g = HueToRgb(p, q, h);
        var b = HueToRgb(p, q, h - (1.0 / 3.0));
        return Color.FromRgb(
            (byte)Math.Round(r * 255.0),
            (byte)Math.Round(g * 255.0),
            (byte)Math.Round(b * 255.0));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }

        if (t > 1)
        {
            t -= 1;
        }

        if (t < 1.0 / 6.0)
        {
            return p + ((q - p) * 6 * t);
        }

        if (t < 1.0 / 2.0)
        {
            return q;
        }

        if (t < 2.0 / 3.0)
        {
            return p + ((q - p) * ((2.0 / 3.0) - t) * 6);
        }

        return p;
    }

    private static double CalculateContrastRatio(Color foreground, Color background)
    {
        var lighter = Math.Max(RelativeLuminance(foreground), RelativeLuminance(background));
        var darker = Math.Min(RelativeLuminance(foreground), RelativeLuminance(background));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.03928
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        var r = Channel(color.R);
        var g = Channel(color.G);
        var b = Channel(color.B);
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    private static UiStylePresetValues GetValues(UiCompressionPreset preset, UiSignalPreset signalPreset)
    {
        var values = preset switch
        {
            UiCompressionPreset.Compressed => new UiStylePresetValues
            {
                ButtonPadding = new Thickness(6, 3),
                ButtonMinHeight = 24,
                ToggleButtonPadding = new Thickness(7, 3),
                ToggleButtonMinHeight = 26,
                TextBoxPadding = new Thickness(7, 5),
                TextBoxMinHeight = 28,
                ComboBoxPadding = new Thickness(6, 3),
                ComboBoxMinHeight = 26,
                NumericUpDownMinHeight = 28,
                NavTabPadding = new Thickness(8, 4),
                NavTabMinHeight = 30,
                PaneStackSpacing = 5,
                SplitWorkspaceGutter = new GridLength(6),
                SectionHeaderPadding = new Thickness(10, 8),
                SectionHeaderCompactPadding = new Thickness(6, 4),
                SectionBodyPadding = new Thickness(10),
                SectionBodySubtlePadding = new Thickness(8),
                SubSectionPadding = new Thickness(8),
                InlineSectionPadding = new Thickness(8, 6),
                PageHeaderPadding = new Thickness(10, 8),
                ToolbarStripPadding = new Thickness(12, 8),
                ComposerBarPadding = new Thickness(12, 10),
                PaneSectionPadding = new Thickness(5, 2),
                CompactActionPadding = new Thickness(5, 2),
                CompactActionMinHeight = 22,
                FlatButtonPadding = new Thickness(5, 2),
                FlatButtonMinHeight = 22,
                InlineBadgePadding = new Thickness(4, 1),
                InlineFieldPadding = new Thickness(6, 4),
                BannerClusterPadding = new Thickness(6, 4),
                InspectorGroupPadding = new Thickness(0, 0, 0, 10),
                InspectorGroupMargin = new Thickness(0, 0, 0, 10),
                TranscriptRowPadding = new Thickness(12, 10),
                WorkspaceSectionPadding = new Thickness(8, 8, 8, 10),
                CompactComboPadding = new Thickness(3, 1),
                CompactComboMinHeight = 22,
                CompactComboFontSize = 10,
                HeaderActionSize = 22,
                HeaderActionFontSize = 11,
                IconButtonSize = 24,
                IconButtonFontSize = 12,
                ToolbarControlWidthSmall = 108,
                ToolbarControlWidthMedium = 148,
                ToolbarControlWidthLarge = 184,
                InspectorLabelWidth = 112,
                InspectorEditorMinWidth = 124,
                InspectorCardWidth = 268,
                DiagnosticsMaxHeight = 176,
                LogPanelMaxHeight = 176,
                RunDetailsMaxHeight = 220,
                CompactPreviewMaxHeight = 38,
                HeaderClusterGap = 4,
                InlineControlGap = 3,
                RoleLabelWidth = 52,
                RoleLabelFontSize = 10,
                FieldLabelFontSize = 10,
                InlineLabelFontSize = 10,
                SectionMetaFontSize = 10,
            },
            UiCompressionPreset.Comfortable => new UiStylePresetValues
            {
                ButtonPadding = new Thickness(8, 5),
                ButtonMinHeight = 28,
                ToggleButtonPadding = new Thickness(9, 5),
                ToggleButtonMinHeight = 30,
                TextBoxPadding = new Thickness(10, 7),
                TextBoxMinHeight = 32,
                ComboBoxPadding = new Thickness(9, 5),
                ComboBoxMinHeight = 30,
                NumericUpDownMinHeight = 32,
                NavTabPadding = new Thickness(12, 7),
                NavTabMinHeight = 34,
                PaneStackSpacing = 8,
                SplitWorkspaceGutter = new GridLength(12),
                SectionHeaderPadding = new Thickness(16, 12),
                SectionHeaderCompactPadding = new Thickness(10, 6),
                SectionBodyPadding = new Thickness(16),
                SectionBodySubtlePadding = new Thickness(12),
                SubSectionPadding = new Thickness(12),
                InlineSectionPadding = new Thickness(12, 8),
                PageHeaderPadding = new Thickness(16, 12),
                ToolbarStripPadding = new Thickness(18, 12),
                ComposerBarPadding = new Thickness(18, 14),
                PaneSectionPadding = new Thickness(10),
                CompactActionPadding = new Thickness(7, 3),
                CompactActionMinHeight = 26,
                FlatButtonPadding = new Thickness(7, 3),
                FlatButtonMinHeight = 26,
                InlineBadgePadding = new Thickness(6, 2),
                InlineFieldPadding = new Thickness(8, 6),
                BannerClusterPadding = new Thickness(8, 6),
                InspectorGroupPadding = new Thickness(0, 0, 0, 14),
                InspectorGroupMargin = new Thickness(0, 0, 0, 14),
                TranscriptRowPadding = new Thickness(18, 14),
                WorkspaceSectionPadding = new Thickness(14, 12, 14, 14),
                CompactComboPadding = new Thickness(5, 2),
                CompactComboMinHeight = 28,
                CompactComboFontSize = 12,
                HeaderActionSize = 28,
                HeaderActionFontSize = 14,
                IconButtonSize = 32,
                IconButtonFontSize = 15,
                ToolbarControlWidthSmall = 132,
                ToolbarControlWidthMedium = 176,
                ToolbarControlWidthLarge = 220,
                InspectorLabelWidth = 148,
                InspectorEditorMinWidth = 156,
                InspectorCardWidth = 336,
                DiagnosticsMaxHeight = 236,
                LogPanelMaxHeight = 236,
                RunDetailsMaxHeight = 292,
                CompactPreviewMaxHeight = 56,
                HeaderClusterGap = 8,
                InlineControlGap = 6,
                RoleLabelWidth = 62,
                RoleLabelFontSize = 12,
                FieldLabelFontSize = 11,
                InlineLabelFontSize = 11,
                SectionMetaFontSize = 11,
            },
            _ => new UiStylePresetValues
            {
                ButtonPadding = new Thickness(6, 4),
                ButtonMinHeight = 25,
                ToggleButtonPadding = new Thickness(7, 4),
                ToggleButtonMinHeight = 27,
                TextBoxPadding = new Thickness(8, 5),
                TextBoxMinHeight = 29,
                ComboBoxPadding = new Thickness(7, 4),
                ComboBoxMinHeight = 27,
                NumericUpDownMinHeight = 29,
                NavTabPadding = new Thickness(9, 5),
                NavTabMinHeight = 31,
                PaneStackSpacing = 5,
                SplitWorkspaceGutter = new GridLength(8),
                SectionHeaderPadding = new Thickness(11, 9),
                SectionHeaderCompactPadding = new Thickness(6, 4),
                SectionBodyPadding = new Thickness(10),
                SectionBodySubtlePadding = new Thickness(8),
                SubSectionPadding = new Thickness(8),
                InlineSectionPadding = new Thickness(8),
                PageHeaderPadding = new Thickness(11, 9),
                ToolbarStripPadding = new Thickness(14, 10),
                ComposerBarPadding = new Thickness(14, 10),
                PaneSectionPadding = new Thickness(10),
                CompactActionPadding = new Thickness(6, 2),
                CompactActionMinHeight = 24,
                FlatButtonPadding = new Thickness(6, 3),
                FlatButtonMinHeight = 24,
                InlineBadgePadding = new Thickness(5, 1),
                InlineFieldPadding = new Thickness(6, 4),
                BannerClusterPadding = new Thickness(6, 4),
                InspectorGroupPadding = new Thickness(0, 0, 0, 12),
                InspectorGroupMargin = new Thickness(0, 0, 0, 12),
                TranscriptRowPadding = new Thickness(14, 11),
                WorkspaceSectionPadding = new Thickness(9, 9, 9, 11),
                CompactComboPadding = new Thickness(4, 1),
                CompactComboMinHeight = 24,
                CompactComboFontSize = 11,
                HeaderActionSize = 24,
                HeaderActionFontSize = 12,
                IconButtonSize = 28,
                IconButtonFontSize = 13,
                ToolbarControlWidthSmall = 116,
                ToolbarControlWidthMedium = 160,
                ToolbarControlWidthLarge = 196,
                InspectorLabelWidth = 128,
                InspectorEditorMinWidth = 140,
                InspectorCardWidth = 300,
                DiagnosticsMaxHeight = 200,
                LogPanelMaxHeight = 200,
                RunDetailsMaxHeight = 248,
                CompactPreviewMaxHeight = 44,
                HeaderClusterGap = 6,
                InlineControlGap = 4,
                RoleLabelWidth = 56,
                RoleLabelFontSize = 11,
                FieldLabelFontSize = 10,
                InlineLabelFontSize = 10,
                SectionMetaFontSize = 10,
            }
        };

        values.SignalStandardBorderThickness = new Thickness(0.7);
        values.SignalEmphasisBorderThickness = signalPreset == UiSignalPreset.Strong ? new Thickness(2) : new Thickness(1);
        if (signalPreset == UiSignalPreset.Subtle)
        {
            values.SectionMetaFontSize = Math.Max(10, values.SectionMetaFontSize - 0.25);
        }
        else if (signalPreset == UiSignalPreset.Strong)
        {
            values.RoleLabelFontSize += 1;
            values.FieldLabelFontSize += 1;
            values.InlineLabelFontSize += 1;
            values.HeaderActionFontSize += 1;
            values.IconButtonFontSize += 1;
        }

        return values;
    }
}

public record UiStyleSettingsSnapshot(
    UiDensityPreset DensityPreset,
    UiCompressionPreset CompressionPreset,
    UiSignalPreset SignalPreset,
    string CoreHex,
    string ContentHex,
    string HighlightHex,
    double OverlayOpacityPercent,
    double ContrastRatio);

public class UiStyleSettingsDocument
{
    public int SchemaVersion { get; set; } = UiStyleSettingsDefaults.SchemaVersion;
    public string DensityPreset { get; set; } = UiDensityPreset.Default.ToString();
    public string CompressionPreset { get; set; } = UiStyleSettingsDefaults.CompressionPreset;
    public string SignalPreset { get; set; } = UiStyleSettingsDefaults.SignalPreset;
    public string? CoreHex { get; set; } = UiStyleSettingsDefaults.CoreHex;
    public string? ContentHex { get; set; } = UiStyleSettingsDefaults.ContentHex;
    public string? HighlightHex { get; set; } = UiStyleSettingsDefaults.HighlightHex;
    public double OverlayOpacityPercent { get; set; } = UiStyleSettingsDefaults.OverlayOpacityPercent;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GradientStartHex { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GradientEndHex { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? GradientAngleDegrees { get; set; }
}

sealed class UiStylePresetValues
{
    public Thickness ButtonPadding { get; init; }
    public double ButtonMinHeight { get; init; }
    public Thickness ToggleButtonPadding { get; init; }
    public double ToggleButtonMinHeight { get; init; }
    public Thickness TextBoxPadding { get; init; }
    public double TextBoxMinHeight { get; init; }
    public Thickness ComboBoxPadding { get; init; }
    public double ComboBoxMinHeight { get; init; }
    public double NumericUpDownMinHeight { get; init; }
    public Thickness NavTabPadding { get; init; }
    public double NavTabMinHeight { get; init; }
    public double PaneStackSpacing { get; init; }
    public GridLength SplitWorkspaceGutter { get; init; }
    public Thickness SectionHeaderPadding { get; init; }
    public Thickness SectionHeaderCompactPadding { get; init; }
    public Thickness SectionBodyPadding { get; init; }
    public Thickness SectionBodySubtlePadding { get; init; }
    public Thickness SubSectionPadding { get; init; }
    public Thickness InlineSectionPadding { get; init; }
    public Thickness PageHeaderPadding { get; init; }
    public Thickness ToolbarStripPadding { get; init; }
    public Thickness ComposerBarPadding { get; init; }
    public Thickness PaneSectionPadding { get; init; }
    public Thickness CompactActionPadding { get; init; }
    public double CompactActionMinHeight { get; init; }
    public Thickness FlatButtonPadding { get; init; }
    public double FlatButtonMinHeight { get; init; }
    public Thickness InlineBadgePadding { get; init; }
    public Thickness InlineFieldPadding { get; init; }
    public Thickness BannerClusterPadding { get; init; }
    public Thickness InspectorGroupPadding { get; init; }
    public Thickness InspectorGroupMargin { get; init; }
    public Thickness TranscriptRowPadding { get; init; }
    public Thickness WorkspaceSectionPadding { get; init; }
    public Thickness CompactComboPadding { get; set; }
    public double CompactComboMinHeight { get; set; }
    public double CompactComboFontSize { get; set; }
    public double HeaderActionSize { get; set; }
    public double HeaderActionFontSize { get; set; }
    public double IconButtonSize { get; set; }
    public double IconButtonFontSize { get; set; }
    public double ToolbarControlWidthSmall { get; set; }
    public double ToolbarControlWidthMedium { get; set; }
    public double ToolbarControlWidthLarge { get; set; }
    public double InspectorLabelWidth { get; set; }
    public double InspectorEditorMinWidth { get; set; }
    public double InspectorCardWidth { get; set; }
    public double DiagnosticsMaxHeight { get; set; }
    public double LogPanelMaxHeight { get; set; }
    public double RunDetailsMaxHeight { get; set; }
    public double CompactPreviewMaxHeight { get; set; }
    public double HeaderClusterGap { get; set; }
    public double InlineControlGap { get; set; }
    public double RoleLabelWidth { get; set; }
    public double RoleLabelFontSize { get; set; }
    public double FieldLabelFontSize { get; set; }
    public double InlineLabelFontSize { get; set; }
    public double SectionMetaFontSize { get; set; }
    public Thickness SignalStandardBorderThickness { get; set; }
    public Thickness SignalEmphasisBorderThickness { get; set; }
}
