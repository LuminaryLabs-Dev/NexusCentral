using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NexusCentral.V3.Desktop.Components;

public partial class StatusDock : UserControl
{
    public StatusDock()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
