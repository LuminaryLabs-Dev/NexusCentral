using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexusCentral.V3.Core.Execution;
using NexusCentral.V3.Features.Workspaces;

namespace NexusCentral.V3.Features.Lab;

public partial class LabViewModel : ObservableObject
{
    private const string ConversationId = "lab-default";
    private readonly IWorkspaceService _workspaces;
    private readonly IWorkspaceSessionStore _workspaceSessions;
    private readonly ILabConversationStore _conversations;
    private readonly IExecutionEventStream _executionEvents;
    private LabConversation _conversation;

    public LabViewModel(
        IWorkspaceService workspaces,
        IWorkspaceSessionStore workspaceSessions,
        ILabConversationStore conversations,
        IExecutionEventStream executionEvents)
    {
        _workspaces = workspaces;
        _workspaceSessions = workspaceSessions;
        _conversations = conversations;
        _executionEvents = executionEvents;
        _conversation = new LabConversation(ConversationId, _workspaces.ActiveWorkspace?.Id ?? string.Empty, DateTimeOffset.UtcNow, Array.Empty<ChatMessage>());
        StatusLabel = "Ready";
        ActiveWorkspaceName = _workspaces.ActiveWorkspace?.Name ?? "No workspace";
        _ = InitializeAsync();
    }

    public ObservableCollection<WorkspaceTreeNodeViewModel> WorkspaceTree { get; } = new();

    public ObservableCollection<WorkspaceSummaryViewModel> Workspaces { get; } = new();

    public ObservableCollection<LabTranscriptItemViewModel> Transcript { get; } = new();

    public ObservableCollection<ChatAttachmentViewModel> PendingAttachments { get; } = new();

    [ObservableProperty]
    private string _statusLabel = string.Empty;

    [ObservableProperty]
    private string _activeWorkspaceName = string.Empty;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isDropTargetActive;

    [ObservableProperty]
    private WorkspaceSummaryViewModel? _selectedWorkspace;

    public string WorkspaceSummaryText => WorkspaceTree.Count == 0
        ? "Browse-only workspace tree"
        : $"{WorkspaceTree.Count} visible nodes";

    public string SessionName => $"lab-session-{DateTimeOffset.Now:yyyy-MM-dd}";

    public string SelectedPathText
    {
        get
        {
            var active = _workspaces.ActiveWorkspace;
            return active is null ? "Path: none" : $"Path: {active.Name}";
        }
    }

    public string SelectedPathSizeText => $"{WorkspaceTree.Count} nodes";

    public string TranscriptStatsText => $"Messages: {Transcript.Count}";

    public async Task InitializeAsync()
    {
        RefreshWorkspaceList();
        await RefreshWorkspaceTreeAsync();
        var activeWorkspace = _workspaces.ActiveWorkspace;
        _conversation = await _conversations.LoadAsync(ConversationId)
            ?? new LabConversation(ConversationId, activeWorkspace?.Id ?? string.Empty, DateTimeOffset.UtcNow, CreateInitialMessages());
        RenderTranscript();
    }

    [RelayCommand]
    public Task RefreshWorkspaceTreeAsync()
    {
        WorkspaceTree.Clear();
        var active = _workspaces.ActiveWorkspace;
        ActiveWorkspaceName = active?.Name ?? "No workspace";
        RefreshWorkspaceList();
        OnPropertyChanged(nameof(SelectedPathText));
        if (active is null)
        {
            OnPropertyChanged(nameof(WorkspaceSummaryText));
            OnPropertyChanged(nameof(SelectedPathSizeText));
            return Task.CompletedTask;
        }

        return RefreshWorkspaceTreeCoreAsync(active);
    }

    [RelayCommand]
    public async Task CreateSandboxWorkspaceAsync()
    {
        StatusLabel = "Creating workspace";
        await _workspaces.CreateSandboxAsync("Sandbox");
        RefreshWorkspaceList();
        await RefreshWorkspaceTreeAsync();
        StatusLabel = "Ready";
    }

    public async Task OpenWorkspaceFromPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        StatusLabel = "Opening workspace";
        await _workspaces.OpenWorkspaceAsync(path);
        RefreshWorkspaceList();
        await RefreshWorkspaceTreeAsync();
        StatusLabel = "Ready";
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        var text = InputText.Trim();
        if (string.IsNullOrWhiteSpace(text) && PendingAttachments.Count == 0)
        {
            return;
        }

        var attachments = PendingAttachments
            .Select(item => item.ToModel())
            .ToList();

        AppendMessage(new ChatMessage(
            Guid.NewGuid().ToString("N"),
            ChatMessageRole.User,
            text,
            DateTimeOffset.UtcNow,
            Array.Empty<ToolUseStep>(),
            attachments));

        InputText = string.Empty;
        PendingAttachments.Clear();

        var started = _executionEvents.Publish(ExecutionEventKind.ToolUseStarted, ConversationId, "WorkspaceContext", "Preparing workspace context.");
        AppendMessage(new ChatMessage(
            Guid.NewGuid().ToString("N"),
            ChatMessageRole.Assistant,
            "Recorded the request. Model execution is not wired in this slice.",
            DateTimeOffset.UtcNow,
            new[]
            {
                new ToolUseStep(started.ToolName, "completed", "Workspace context attached.", DateTimeOffset.UtcNow)
            },
            Array.Empty<ChatAttachment>()));
        _executionEvents.Publish(ExecutionEventKind.ToolUseCompleted, ConversationId, "WorkspaceContext", "Workspace context attached.");

        await PersistConversationAsync();
    }

    public async Task AddAttachmentFromPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var info = new FileInfo(path);
        var attachment = new ChatAttachment(
            Guid.NewGuid().ToString("N"),
            info.Name,
            info.FullName,
            info.Length,
            DateTimeOffset.UtcNow);

        PendingAttachments.Add(ChatAttachmentViewModel.FromModel(attachment));
        await PersistWorkspaceUploadAsync(attachment);
    }

    partial void OnSelectedWorkspaceChanged(WorkspaceSummaryViewModel? value)
    {
        if (value is null || string.Equals(value.Id, _workspaces.ActiveWorkspace?.Id, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = SelectWorkspaceAsync(value.Id);
    }

    private async Task SelectWorkspaceAsync(string workspaceId)
    {
        StatusLabel = "Selecting workspace";
        if (await _workspaces.SelectWorkspaceAsync(workspaceId))
        {
            await RefreshWorkspaceTreeAsync();
        }

        StatusLabel = "Ready";
    }

    private void RefreshWorkspaceList()
    {
        var active = _workspaces.ActiveWorkspace;
        Workspaces.Clear();
        foreach (var workspace in _workspaces.GetWorkspaces().OrderBy(workspace => workspace.Name))
        {
            Workspaces.Add(WorkspaceSummaryViewModel.FromModel(workspace, active?.Id));
        }

        SelectedWorkspace = Workspaces.FirstOrDefault(workspace => string.Equals(workspace.Id, active?.Id, StringComparison.OrdinalIgnoreCase));
        ActiveWorkspaceName = active?.Name ?? "No workspace";
        OnPropertyChanged(nameof(WorkspaceSummaryText));
        OnPropertyChanged(nameof(SelectedPathText));
    }

    private async Task RefreshWorkspaceTreeCoreAsync(WorkspaceDefinition active)
    {
        var tree = await _workspaces.GetTreeAsync(active.Id);
        foreach (var node in Flatten(tree))
        {
            WorkspaceTree.Add(WorkspaceTreeNodeViewModel.FromModel(node));
        }

        OnPropertyChanged(nameof(WorkspaceSummaryText));
        OnPropertyChanged(nameof(SelectedPathSizeText));
    }

    private async Task PersistWorkspaceUploadAsync(ChatAttachment attachment)
    {
        var active = _workspaces.ActiveWorkspace;
        if (active is null)
        {
            return;
        }

        var session = await _workspaceSessions.LoadAsync(active.Id);
        var uploads = session.RecentUploads.Concat(new[] { attachment.SourcePath }).Distinct(StringComparer.OrdinalIgnoreCase).TakeLast(20).ToList();
        await _workspaceSessions.SaveAsync(session with { RecentUploads = uploads });
    }

    private void AppendMessage(ChatMessage message)
    {
        _conversation = _conversation with
        {
            WorkspaceId = _workspaces.ActiveWorkspace?.Id ?? string.Empty,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Messages = _conversation.Messages.Concat(new[] { message }).ToList()
        };
        Transcript.Add(LabTranscriptItemViewModel.FromModel(message));
        OnPropertyChanged(nameof(TranscriptStatsText));
    }

    private async Task PersistConversationAsync()
    {
        await _conversations.SaveAsync(_conversation);
    }

    private void RenderTranscript()
    {
        Transcript.Clear();
        foreach (var message in _conversation.Messages)
        {
            Transcript.Add(LabTranscriptItemViewModel.FromModel(message));
        }

        OnPropertyChanged(nameof(TranscriptStatsText));
    }

    private static IReadOnlyList<ChatMessage> CreateInitialMessages()
    {
        return new[]
        {
            new ChatMessage(
                Guid.NewGuid().ToString("N"),
                ChatMessageRole.System,
                "Lab ready. Chat is compact output; workspace files are browsed from the left tree.",
                DateTimeOffset.UtcNow,
                Array.Empty<ToolUseStep>(),
                Array.Empty<ChatAttachment>())
        };
    }

    private static IEnumerable<WorkspaceTreeNode> Flatten(IEnumerable<WorkspaceTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}

public sealed class WorkspaceTreeNodeViewModel
{
    private WorkspaceTreeNodeViewModel(WorkspaceTreeNode node)
    {
        Name = node.Name;
        RelativePath = node.RelativePath;
        IsDirectory = node.IsDirectory;
        Depth = node.Depth;
        Indent = Math.Max(0, node.Depth) * 14;
        Glyph = node.IsDirectory ? "folder" : "file";
        DisplayText = $"{new string(' ', Math.Max(0, node.Depth) * 2)}{Glyph} {node.Name}";
    }

    public string Name { get; }

    public string RelativePath { get; }

    public bool IsDirectory { get; }

    public int Depth { get; }

    public int Indent { get; }

    public string Glyph { get; }

    public string DisplayText { get; }

    public static WorkspaceTreeNodeViewModel FromModel(WorkspaceTreeNode node) => new(node);
}

public sealed class WorkspaceSummaryViewModel
{
    private WorkspaceSummaryViewModel(WorkspaceDefinition workspace, string? activeWorkspaceId)
    {
        Id = workspace.Id;
        Name = workspace.Name;
        Kind = workspace.Kind;
        RootPath = workspace.RootPath;
        IsActive = string.Equals(workspace.Id, activeWorkspaceId, StringComparison.OrdinalIgnoreCase);
        DisplayText = IsActive ? $"{workspace.Name}  active" : workspace.Name;
    }

    public string Id { get; }

    public string Name { get; }

    public string Kind { get; }

    public string RootPath { get; }

    public bool IsActive { get; }

    public string DisplayText { get; }

    public static WorkspaceSummaryViewModel FromModel(WorkspaceDefinition workspace, string? activeWorkspaceId) => new(workspace, activeWorkspaceId);
}

public sealed class LabTranscriptItemViewModel
{
    private LabTranscriptItemViewModel(ChatMessage message)
    {
        Role = message.Role.ToString();
        FormattedText = LabTranscriptFormatter.FormatMessage(message);
    }

    public string Role { get; }

    public string FormattedText { get; }

    public static LabTranscriptItemViewModel FromModel(ChatMessage message) => new(message);
}

public sealed class ChatAttachmentViewModel
{
    private readonly ChatAttachment _model;

    private ChatAttachmentViewModel(ChatAttachment model)
    {
        _model = model;
        FileName = model.FileName;
        SizeText = FormatSize(model.SizeBytes);
    }

    public string FileName { get; }

    public string SizeText { get; }

    public ChatAttachment ToModel() => _model;

    public static ChatAttachmentViewModel FromModel(ChatAttachment model) => new(model);

    private static string FormatSize(long bytes)
    {
        return bytes < 1024
            ? $"{bytes} B"
            : $"{bytes / 1024d:0.0} KB";
    }
}
