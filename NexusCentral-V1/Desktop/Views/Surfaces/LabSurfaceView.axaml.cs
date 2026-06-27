using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using NexusCentral.V3.Features.Lab;
using System.Linq;

namespace NexusCentral.V3.Desktop.Views.Surfaces;

public partial class LabSurfaceView : UserControl
{
    public LabSurfaceView()
    {
        InitializeComponent();
    }

    private async void UploadButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not LabViewModel viewModel)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Upload files to chat",
            AllowMultiple = true
        });

        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                await viewModel.AddAttachmentFromPathAsync(path);
            }
        }
    }

    private async void OpenWorkspaceButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not LabViewModel viewModel)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open workspace folder",
            AllowMultiple = false
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.OpenWorkspaceFromPathAsync(path);
        }
    }

    private void ChatDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.TryGetFiles() is { Length: > 0 }
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void ChatDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not LabViewModel viewModel)
        {
            return;
        }

        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return;
        }

        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                await viewModel.AddAttachmentFromPathAsync(path);
            }
        }
    }
}
