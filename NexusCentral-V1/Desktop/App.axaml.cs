using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NexusCentral.V1.Desktop.ViewModels;
using NexusCentral.V1.Desktop.Views;

namespace NexusCentral.V1.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IClassicDesktopStyleApplicationLifetime? desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop is not null)
        {
            var services = AppCompositionRoot.CreateDefault(ResolveStartupSurface());
            var mainWindow = new MainWindow
            {
                DataContext = services.Shell
            };
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();

        if (desktop?.MainWindow is { } window)
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    private static SurfaceKind? ResolveStartupSurface()
    {
        var value = Environment.GetEnvironmentVariable("NEXUSCENTRAL_START_SURFACE");
        return Enum.TryParse<SurfaceKind>(value, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }
}
