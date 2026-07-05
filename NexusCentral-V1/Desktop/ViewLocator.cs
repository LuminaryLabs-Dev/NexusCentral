using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using NexusCentral.V1.Desktop.ViewModels;
using NexusCentral.V1.Desktop.Views.Surfaces;

namespace NexusCentral.V1.Desktop;

public sealed class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is NavigationItemViewModel surface)
        {
            var surfaceModel = surface.SurfaceModel ?? surface;
            return surface.Kind switch
            {
                SurfaceKind.Lab => new LabSurfaceView { DataContext = surfaceModel },
                SurfaceKind.Models => new ModelsSurfaceView { DataContext = surfaceModel },
                SurfaceKind.Agents => new AgentsSurfaceView { DataContext = surfaceModel },
                SurfaceKind.Automations => new AutomationsSurfaceView { DataContext = surface },
                SurfaceKind.Devices => new DevicesSurfaceView { DataContext = surface },
                SurfaceKind.Router => new RouterSurfaceView { DataContext = surfaceModel },
                SurfaceKind.Settings => new SettingsSurfaceView { DataContext = surface },
                _ => BuildFallback(data)
            };
        }

        return BuildFallback(data);
    }

    private static Control BuildFallback(object? data)
    {
        return new TextBlock
        {
            Text = data?.ToString() ?? string.Empty
        };
    }

    public bool Match(object? data)
    {
        return data is not null;
    }
}
