using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NexusCentral.V3.Core.Config;
using NexusCentral.V3.Core.Execution;
using NexusCentral.V3.Core.Logging;
using NexusCentral.V3.Core.State;
using NexusCentral.V3.Features.Lab;
using NexusCentral.V3.Features.Workspaces;
using Xunit;

namespace NexusCentral.V3.Tests;

public sealed class LabConversationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"nexuscentral-v3-lab-tests-{Guid.NewGuid():N}");

    [Fact]
    public void LabTranscriptFormatter_RendersToolStepsCompactly()
    {
        var message = new ChatMessage(
            "msg-1",
            ChatMessageRole.Assistant,
            "Done.",
            DateTimeOffset.UtcNow,
            new[]
            {
                new ToolUseStep("WorkspaceContext", "completed", "Workspace context attached.", DateTimeOffset.UtcNow)
            },
            Array.Empty<ChatAttachment>());

        var text = LabTranscriptFormatter.FormatMessage(message);

        Assert.Contains("Assistant", text);
        Assert.Contains("Using WorkspaceContext", text);
        Assert.Contains("Done.", text);
        Assert.DoesNotContain("card", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bubble", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddAttachmentFromPathAsync_CreatesChatUploadRecord()
    {
        var services = CreateLabServices();
        var uploadPath = Path.Combine(_root, "upload.txt");
        await File.WriteAllTextAsync(uploadPath, "upload");

        await services.Lab.InitializeAsync();
        await services.Lab.AddAttachmentFromPathAsync(uploadPath);

        Assert.Single(services.Lab.PendingAttachments);
        Assert.Equal("upload.txt", services.Lab.PendingAttachments.Single().FileName);

        var session = await services.Sessions.LoadAsync(services.Workspaces.ActiveWorkspace!.Id);
        Assert.Contains(uploadPath, session.RecentUploads);
    }

    [Fact]
    public async Task OpenWorkspaceFromPathAsync_RefreshesWorkspaceListAndTree()
    {
        var services = CreateLabServices();
        var workspaceRoot = Path.Combine(_root, "opened-workspace");
        Directory.CreateDirectory(workspaceRoot);
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "notes.md"), "notes");

        await services.Lab.InitializeAsync();
        await services.Lab.OpenWorkspaceFromPathAsync(workspaceRoot);

        Assert.Equal("opened-workspace", services.Lab.ActiveWorkspaceName);
        Assert.NotNull(services.Lab.SelectedWorkspace);
        Assert.Contains(services.Lab.Workspaces, workspace => workspace.Name == "opened-workspace");
        Assert.Contains(services.Lab.WorkspaceTree, node => node.Name == "notes.md");
    }

    [Fact]
    public async Task CreateSandboxWorkspaceAsync_AddsSelectableWorkspace()
    {
        var services = CreateLabServices();

        await services.Lab.InitializeAsync();
        await services.Lab.CreateSandboxWorkspaceAsync();

        Assert.Equal("Sandbox", services.Lab.ActiveWorkspaceName);
        Assert.NotNull(services.Lab.SelectedWorkspace);
        Assert.Contains(services.Lab.Workspaces, workspace => workspace.Name == "Sandbox" && workspace.IsActive);
    }

    [Fact]
    public async Task SendMessageAsync_AppendsInlineToolStepAndPublishesEvents()
    {
        var services = CreateLabServices();
        await services.Lab.InitializeAsync();
        services.Lab.InputText = "Summarize the workspace.";

        await services.Lab.SendMessageAsync();

        var transcript = string.Join("\n", services.Lab.Transcript.Select(item => item.FormattedText));
        var events = services.Execution.GetEvents();

        Assert.Contains("User", transcript);
        Assert.Contains("Summarize the workspace.", transcript);
        Assert.Contains("Using WorkspaceContext", transcript);
        Assert.Contains(events, item => item.Kind == ExecutionEventKind.ToolUseStarted);
        Assert.Contains(events, item => item.Kind == ExecutionEventKind.ToolUseCompleted);
    }

    [Fact]
    public async Task SendMessageAsync_RendersSentAttachmentInTranscript()
    {
        var services = CreateLabServices();
        var uploadPath = Path.Combine(_root, "uploaded-context.md");
        await File.WriteAllTextAsync(uploadPath, "context");

        await services.Lab.InitializeAsync();
        await services.Lab.AddAttachmentFromPathAsync(uploadPath);
        services.Lab.InputText = "Use this file.";

        await services.Lab.SendMessageAsync();

        var transcript = string.Join("\n", services.Lab.Transcript.Select(item => item.FormattedText));
        Assert.Contains("uploaded-context.md", transcript);
        Assert.Empty(services.Lab.PendingAttachments);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private LabServiceSet CreateLabServices()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "state"));
        var json = new JsonFileStore();
        var workspaces = new WorkspaceService(paths, json, new InMemoryAppLogService());
        var sessions = new FileWorkspaceSessionStore(paths, json);
        var conversations = new FileLabConversationStore(paths, json);
        var execution = new InMemoryExecutionEventStream();
        var lab = new LabViewModel(workspaces, sessions, conversations, execution);

        return new LabServiceSet(workspaces, sessions, execution, lab);
    }

    private sealed record LabServiceSet(
        IWorkspaceService Workspaces,
        IWorkspaceSessionStore Sessions,
        IExecutionEventStream Execution,
        LabViewModel Lab);
}
