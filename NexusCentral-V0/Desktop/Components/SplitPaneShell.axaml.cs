using Avalonia;
using Avalonia.Controls;
using NexusCentral_Avalonia;
using NexusCentral_Avalonia.Views;
using NexusCentral_Avalonia.Services;

namespace NexusCentral_Avalonia.Components;

public enum ShellSurfaceMode
{
    PerPane,
    OuterWrapper,
    None,
}

public enum PaneSurfaceVariant
{
    PagePane,
    PaneColumn,
    None,
}

public partial class SplitPaneShell : UserControl
{
    private bool _splitAttached;

    public static readonly StyledProperty<string> LayoutKeyProperty =
        AvaloniaProperty.Register<SplitPaneShell, string>(nameof(LayoutKey), "default");

    public static readonly StyledProperty<double> FallbackRatioProperty =
        AvaloniaProperty.Register<SplitPaneShell, double>(nameof(FallbackRatio), 0.25);

    public static readonly StyledProperty<object?> LeftHeaderContentProperty =
        AvaloniaProperty.Register<SplitPaneShell, object?>(nameof(LeftHeaderContent));

    public static readonly StyledProperty<object?> RightHeaderContentProperty =
        AvaloniaProperty.Register<SplitPaneShell, object?>(nameof(RightHeaderContent));

    public static readonly StyledProperty<object?> LeftContentProperty =
        AvaloniaProperty.Register<SplitPaneShell, object?>(nameof(LeftContent));

    public static readonly StyledProperty<object?> RightContentProperty =
        AvaloniaProperty.Register<SplitPaneShell, object?>(nameof(RightContent));

    public static readonly StyledProperty<double> SplitterWidthProperty =
        AvaloniaProperty.Register<SplitPaneShell, double>(nameof(SplitterWidth), 2d);

    public static readonly StyledProperty<bool> ShowsPreviewProperty =
        AvaloniaProperty.Register<SplitPaneShell, bool>(nameof(ShowsPreview));

    public static readonly StyledProperty<ShellSurfaceMode> SurfaceModeProperty =
        AvaloniaProperty.Register<SplitPaneShell, ShellSurfaceMode>(nameof(SurfaceMode), ShellSurfaceMode.PerPane);

    public static readonly StyledProperty<PaneSurfaceVariant> LeftPaneSurfaceProperty =
        AvaloniaProperty.Register<SplitPaneShell, PaneSurfaceVariant>(nameof(LeftPaneSurface), PaneSurfaceVariant.PagePane);

    public static readonly StyledProperty<PaneSurfaceVariant> RightPaneSurfaceProperty =
        AvaloniaProperty.Register<SplitPaneShell, PaneSurfaceVariant>(nameof(RightPaneSurface), PaneSurfaceVariant.PagePane);

    static SplitPaneShell()
    {
        SurfaceModeProperty.Changed.AddClassHandler<SplitPaneShell>((shell, _) => shell.UpdateSurfaces());
        LeftPaneSurfaceProperty.Changed.AddClassHandler<SplitPaneShell>((shell, _) => shell.UpdateSurfaces());
        RightPaneSurfaceProperty.Changed.AddClassHandler<SplitPaneShell>((shell, _) => shell.UpdateSurfaces());
    }

    public SplitPaneShell()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        UpdateSurfaces();
    }

    public string LayoutKey
    {
        get => GetValue(LayoutKeyProperty);
        set => SetValue(LayoutKeyProperty, value);
    }

    public double FallbackRatio
    {
        get => GetValue(FallbackRatioProperty);
        set => SetValue(FallbackRatioProperty, value);
    }

    public object? LeftHeaderContent
    {
        get => GetValue(LeftHeaderContentProperty);
        set => SetValue(LeftHeaderContentProperty, value);
    }

    public object? RightHeaderContent
    {
        get => GetValue(RightHeaderContentProperty);
        set => SetValue(RightHeaderContentProperty, value);
    }

    public object? LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    public object? RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }

    public double SplitterWidth
    {
        get => GetValue(SplitterWidthProperty);
        set => SetValue(SplitterWidthProperty, value);
    }

    public bool ShowsPreview
    {
        get => GetValue(ShowsPreviewProperty);
        set => SetValue(ShowsPreviewProperty, value);
    }

    public ShellSurfaceMode SurfaceMode
    {
        get => GetValue(SurfaceModeProperty);
        set => SetValue(SurfaceModeProperty, value);
    }

    public PaneSurfaceVariant LeftPaneSurface
    {
        get => GetValue(LeftPaneSurfaceProperty);
        set => SetValue(LeftPaneSurfaceProperty, value);
    }

    public PaneSurfaceVariant RightPaneSurface
    {
        get => GetValue(RightPaneSurfaceProperty);
        set => SetValue(RightPaneSurfaceProperty, value);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_splitAttached || SplitRoot is null || SplitBar is null || Application.Current is not App app)
        {
            return;
        }

        SplitWorkspaceLayoutHelper.Attach(SplitRoot, SplitBar, LayoutKey, app.Services.SplitWorkspaceLayout, fallbackRatio: FallbackRatio);
        _splitAttached = true;
        UpdateSurfaces();
    }

    private void UpdateSurfaces()
    {
        if (OuterSurface is null || LeftSurface is null || RightSurface is null)
        {
            return;
        }

        ApplyPaneSurface(OuterSurface, SurfaceMode == ShellSurfaceMode.OuterWrapper ? PaneSurfaceVariant.PagePane : PaneSurfaceVariant.None);
        ApplyPaneSurface(LeftSurface, SurfaceMode == ShellSurfaceMode.PerPane ? LeftPaneSurface : PaneSurfaceVariant.None);
        ApplyPaneSurface(RightSurface, SurfaceMode == ShellSurfaceMode.PerPane ? RightPaneSurface : PaneSurfaceVariant.None);
    }

    private static void ApplyPaneSurface(Border border, PaneSurfaceVariant variant)
    {
        EnsureClass(border, "pagePane", variant == PaneSurfaceVariant.PagePane);
        EnsureClass(border, "paneColumn", variant == PaneSurfaceVariant.PaneColumn);
    }

    private static void EnsureClass(StyledElement element, string className, bool enabled)
    {
        if (enabled)
        {
            if (!element.Classes.Contains(className))
            {
                element.Classes.Add(className);
            }

            return;
        }

        if (element.Classes.Contains(className))
        {
            element.Classes.Remove(className);
        }
    }
}
