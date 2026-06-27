using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NexusCentral_Avalonia.ViewModels;

namespace NexusCentral_Avalonia.Views;

public partial class ChatView : UserControl
{
    private static WeakReference<TextBox>? _currentInputBox;
    private static WeakReference<ChatView>? _currentView;
    private TextBox? _chatInputBox;
    private ScrollViewer? _transcriptScrollViewer;
    private bool _isTranscriptPinnedToBottom = true;
    private const double SingleLineHeight = 34;
    private const double LineHeight = 16;
    private const double VerticalPadding = 14;
    private const int MaxVisibleLines = 4;

    public ChatView()
    {
        InitializeComponent();
        _chatInputBox = this.FindControl<TextBox>("ChatInputBox");
        if (_chatInputBox is not null)
        {
            _currentInputBox = new WeakReference<TextBox>(_chatInputBox);
        }
        AttachedToVisualTree += (_, _) =>
        {
            _currentView = new WeakReference<ChatView>(this);
            var inputBox = _chatInputBox ?? this.FindControl<TextBox>("ChatInputBox");
            if (inputBox is not null)
            {
                _chatInputBox = inputBox;
                _currentInputBox = new WeakReference<TextBox>(inputBox);
            }
            _transcriptScrollViewer = this.FindControl<ScrollViewer>("TranscriptScrollViewer");
            UpdateInputHeight();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _currentInputBox = null;
            _currentView = null;
        };
    }

    public static void SyncSemanticInputText(string text)
    {
        if (_currentInputBox is null || !_currentInputBox.TryGetTarget(out var inputBox))
        {
            return;
        }

        if (!string.Equals(inputBox.Text, text, StringComparison.Ordinal))
        {
            inputBox.Text = text;
        }
    }

    public static string GetSemanticInputText()
    {
        if (_currentInputBox is null || !_currentInputBox.TryGetTarget(out var inputBox))
        {
            return string.Empty;
        }

        return inputBox.Text ?? string.Empty;
    }

    public static void RequestTranscriptScrollToBottom()
    {
        if (_currentView is null || !_currentView.TryGetTarget(out var view))
        {
            return;
        }

        view.ScrollTranscriptToBottomIfPinned();
    }

    private void OnChatInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateInputHeight();
    }

    private void OnChatInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter and not Key.Return)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        if (DataContext is not ChatViewModel chatViewModel || !chatViewModel.SendMessageCommand.CanExecute(null))
        {
            return;
        }

        e.Handled = true;
        chatViewModel.SendMessageCommand.Execute(null);
        UpdateInputHeight();
    }

    private void OnTranscriptScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer viewer)
        {
            return;
        }

        var maxOffsetY = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height);
        _isTranscriptPinnedToBottom = maxOffsetY <= 0 || viewer.Offset.Y >= maxOffsetY - 12;
    }

    private void ScrollTranscriptToBottomIfPinned()
    {
        if (_transcriptScrollViewer is null || !_isTranscriptPinnedToBottom)
        {
            return;
        }

        _transcriptScrollViewer.ScrollToEnd();
    }

    private void UpdateInputHeight()
    {
        if (this.FindControl<TextBox>("ChatInputBox") is not { } inputBox)
        {
            return;
        }

        var text = inputBox.Text ?? string.Empty;
        var lineCount = Math.Max(1, text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Length);
        var visibleLines = Math.Min(MaxVisibleLines, lineCount);
        inputBox.Height = Math.Max(SingleLineHeight, VerticalPadding + (visibleLines * LineHeight));
    }

    private async void OnOpenWorkspaceFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ChatViewModel viewModel)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        IStorageFolder? suggestedStartLocation = null;
        if (!string.IsNullOrWhiteSpace(viewModel.ActiveWorkspaceRootDirectory))
        {
            suggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(viewModel.ActiveWorkspaceRootDirectory);
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Workspace Folder",
            AllowMultiple = false,
            SuggestedStartLocation = suggestedStartLocation
        });

        var selectedPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        if (!viewModel.TryOpenWorkspace(selectedPath, out var error))
        {
            viewModel.WorkspaceStatusText = error;
            return;
        }

        viewModel.WorkspaceStatusText = $"Opened workspace {viewModel.ActiveWorkspaceName}.";
    }
}
