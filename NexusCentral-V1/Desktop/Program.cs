using System;
using Avalonia;

namespace NexusCentral.V1.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (ValidationCli.IsCommand(args))
        {
            return ValidationCli.RunAsync(args).GetAwaiter().GetResult();
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
