using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NexusCentral_Avalonia.Services;
using NexusCentral_Avalonia.ViewModels;
using NexusCentral_Avalonia.Views;

namespace NexusCentral_Avalonia;

public partial class App : Application
{
    public AppServices Services { get; } = new();
    private IntegrationServer? _server;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Services.UiStyleSettings.InitializeResources();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = new MainViewModel(Services);
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
            
            _server = new IntegrationServer(Services);
            _server.Start(3005);
            Services.StartLanServices();
            
            desktop.Exit += (sender, args) => 
            {
                Services.StopLanServices();
                _server?.Stop();
                Services.WebLab.Stop();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
