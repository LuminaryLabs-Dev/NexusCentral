using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace NexusCentral_Avalonia.Services;

public record ScreenshotCaptureResult(
    string ImageBase64,
    string Format,
    int Width,
    int Height,
    DateTimeOffset CapturedAt,
    string ActiveTab
);

public class ScreenshotService
{
    private readonly AppStateService _appState;

    public ScreenshotService(AppStateService appState)
    {
        _appState = appState;
    }

    public Task<ScreenshotCaptureResult?> CaptureAsync()
    {
        var tcs = new TaskCompletionSource<ScreenshotCaptureResult?>();

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow != null)
                {
                    var width = (int)desktop.MainWindow.Bounds.Width;
                    var height = (int)desktop.MainWindow.Bounds.Height;

                    if (width > 0 && height > 0)
                    {
                        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
                        bitmap.Render(desktop.MainWindow);

                        using var ms = new MemoryStream();
                        bitmap.Save(ms);
                        tcs.SetResult(new ScreenshotCaptureResult(
                            Convert.ToBase64String(ms.ToArray()),
                            "png",
                            width,
                            height,
                            DateTimeOffset.UtcNow,
                            _appState.CurrentTab ?? string.Empty));
                        return;
                    }
                }

                tcs.SetResult(null);
            }
            catch
            {
                tcs.SetResult(null);
            }
        });

        return tcs.Task;
    }
}
