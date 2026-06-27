using Avalonia.Controls;
using Avalonia.Input;

namespace NexusCentral.V3.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ShellRootPointerMoved(object? sender, PointerEventArgs e)
    {
        var bounds = ShellRoot.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var position = e.GetPosition(ShellRoot);
        var normalizedX = (position.X / bounds.Width - 0.5) * 2.0;
        var normalizedY = (position.Y / bounds.Height - 0.5) * 2.0;
        ShellBackground.SetPointerOffset(normalizedX, normalizedY);
    }

    private void ShellRootPointerExited(object? sender, PointerEventArgs e)
    {
        ShellBackground.SetPointerOffset(0, 0);
    }
}
