using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NexusCentral_Avalonia.ViewModels;

namespace NexusCentral_Avalonia.Views;

public partial class ModelsView : UserControl
{
    public ModelsView()
    {
        InitializeComponent();
    }

    private void OnInstanceTokenSliderCommit(object? sender, RoutedEventArgs e)
    {
        if (sender is Slider { DataContext: LoadedInstanceCardViewModel card })
        {
            card.CommitPendingMaxTokens();
        }
    }

    private void OnInstanceTokenSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Slider { DataContext: LoadedInstanceCardViewModel card })
        {
            card.CommitPendingMaxTokens();
        }
    }

    private void OnInstanceTokenSliderKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End))
        {
            return;
        }

        if (sender is Slider { DataContext: LoadedInstanceCardViewModel card })
        {
            card.CommitPendingMaxTokens();
        }
    }

    private void OnInstanceMemorySliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Slider { DataContext: LoadedInstanceCardViewModel card })
        {
            card.CommitPendingMemoryCapacity();
        }
    }

    private void OnInstanceMemorySliderKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Left or Key.Right or Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End))
        {
            return;
        }

        if (sender is Slider { DataContext: LoadedInstanceCardViewModel card })
        {
            card.CommitPendingMemoryCapacity();
        }
    }
}
