using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NexusCentral.V1.Desktop.ViewModels;

public enum SurfaceKind
{
    Lab,
    Setup,
    Review,
    Models,
    Agents,
    Automations,
    Devices,
    Router,
    Settings
}

public partial class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(
        SurfaceKind kind,
        string name,
        string description,
        string glyph,
        string statusLabel,
        string capabilitySummary,
        string primaryActionLabel,
        string secondaryActionLabel,
        IReadOnlyList<ToolSurfaceItemViewModel>? tools = null,
        object? surfaceModel = null)
    {
        Kind = kind;
        Name = name;
        Description = description;
        Glyph = glyph;
        StatusLabel = statusLabel;
        CapabilitySummary = capabilitySummary;
        PrimaryActionLabel = primaryActionLabel;
        SecondaryActionLabel = secondaryActionLabel;
        Tools = tools ?? Array.Empty<ToolSurfaceItemViewModel>();
        SurfaceModel = surfaceModel;
    }

    public SurfaceKind Kind { get; }

    public string Name { get; }

    public string Description { get; }

    public string Glyph { get; }

    public string StatusLabel { get; }

    public string CapabilitySummary { get; }

    public string PrimaryActionLabel { get; }

    public string SecondaryActionLabel { get; }

    public IReadOnlyList<ToolSurfaceItemViewModel> Tools { get; }

    public object? SurfaceModel { get; }

    public string ToolSurfaceSummaryText => Tools.Count == 0
        ? "No tool contracts staged"
        : $"{Tools.Count} tool contracts staged";

    [ObservableProperty]
    private bool _isSelected;
}
