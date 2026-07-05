using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexusCentral.V1.Core.Execution;
using NexusCentral.V1.Features.Harnesses;
using NexusCentral.V1.Features.Validation;
using NexusCentral.V1.Features.Workspaces;

namespace NexusCentral.V1.Features.Lab;

public partial class LabViewModel : ObservableObject
{
    private const string ConversationId = "lab-default";
    private readonly IWorkspaceService _workspaces;
    private readonly IWorkspaceSessionStore _workspaceSessions;
    private readonly ILabConversationStore _conversations;
    private readonly IExecutionEventStream _executionEvents;
    private readonly IHarnessImporterService? _harnesses;
    private readonly IHostedHarnessTurnRunner? _harnessTurnRunner;
    private readonly ValidationScenarioSnapshot _validationScenarios;
    private readonly IValidationScenarioRunService _validationRunner;
    private readonly IValidationScenarioJobService _validationJobs;
    private LabConversation _conversation;

    public LabViewModel(
        IWorkspaceService workspaces,
        IWorkspaceSessionStore workspaceSessions,
        ILabConversationStore conversations,
        IExecutionEventStream executionEvents,
        IHarnessImporterService? harnesses = null,
        IHostedHarnessTurnRunner? harnessTurnRunner = null,
        IValidationScenarioRunService? validationRunner = null,
        IValidationScenarioJobService? validationJobs = null)
    {
        _workspaces = workspaces;
        _workspaceSessions = workspaceSessions;
        _conversations = conversations;
        _executionEvents = executionEvents;
        _harnesses = harnesses;
        _harnessTurnRunner = harnessTurnRunner;
        _validationScenarios = ValidationScenarioCatalog.CreateDefault();
        _validationRunner = validationRunner ?? new ValidationScenarioRunService(_workspaces);
        _validationJobs = validationJobs ?? new ValidationScenarioJobService(_validationRunner);
        if (_harnesses is not null)
        {
            _harnesses.StateChanged += OnHarnessStateChanged;
        }

        _validationJobs.StateChanged += OnValidationJobStateChanged;
        _conversation = new LabConversation(ConversationId, _workspaces.ActiveWorkspace?.Id ?? string.Empty, DateTimeOffset.UtcNow, Array.Empty<ChatMessage>());
        StatusLabel = "Ready";
        ActiveWorkspaceName = _workspaces.ActiveWorkspace?.Name ?? "No workspace";
        if (_validationJobs.ActiveJob is not null)
        {
            ApplyValidationJobState(_validationJobs.ActiveJob);
        }
        else
        {
            RenderValidationJobs();
        }
        _ = InitializeAsync();
    }

    public ObservableCollection<WorkspaceTreeNodeViewModel> WorkspaceTree { get; } = new();

    public ObservableCollection<WorkspaceSummaryViewModel> Workspaces { get; } = new();

    public ObservableCollection<LabTranscriptItemViewModel> Transcript { get; } = new();

    public ObservableCollection<ChatAttachmentViewModel> PendingAttachments { get; } = new();

    public ObservableCollection<ValidationJobItemViewModel> ValidationJobs { get; } = new();

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

    [ObservableProperty]
    private ValidationJobItemViewModel? _selectedValidationJob;

    [ObservableProperty]
    private string _validationRunStatusText = "No validation run staged.";

    [ObservableProperty]
    private string _validationExecutionStatusText = "No scenarios executed.";

    [ObservableProperty]
    private string _selectedValidationJobReviewText = "Review: run validation, then review the selected output.";

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

    public string ValidationScenarioSummaryText => _validationScenarios.SummaryText;

    public string ValidationTopCapabilityText => _validationScenarios.TopCapabilityText;

    public string ValidationFullProjectText => _validationScenarios.FullProjectText;

    public string ValidationEndpointText => $"{ValidationScenarioCatalog.DefaultEndpointBaseUrl} / {ValidationScenarioCatalog.DefaultModelId} / {ValidationScenarioCatalog.DefaultMaxTokens} tokens";

    public string ValidationJobSummaryText => ValidationJobs.Count == 0
        ? "No validation jobs yet."
        : $"{ValidationJobs.Count} validation jobs tracked.";

    public string SelectedValidationJobTitle => SelectedValidationJob?.DisplayName ?? "No validation job selected";

    public string SelectedValidationJobDetail => SelectedValidationJob?.DetailText ?? "Run smoke or Run all to produce reviewable evidence.";

    public string SelectedValidationJobScoreboardText => SelectedValidationJob?.ScoreboardText ?? "Scoreboard: none";

    public string SelectedValidationJobRankingText => SelectedValidationJob?.CapabilityRankingText ?? "Ranking: none";

    public string SelectedValidationJobProjectsText => SelectedValidationJob?.FullProjectIndexText ?? "Projects: none";

    public string SelectedValidationJobProgressText => SelectedValidationJob?.ProgressText ?? "Progress: none";

    public string SelectedValidationJobRunFolderText => SelectedValidationJob?.ArtifactRootText ?? "Run folder: none";

    public string SelectedValidationJobLearningText => SelectedValidationJob?.LearningNotesText ?? "Learning: none";

    public string ActiveHarnessStatusText
    {
        get
        {
            var harness = _harnesses?.GetSnapshot().ActiveHarness;
            return harness is null ? "Harness: none" : $"Harness: {harness.Name}";
        }
    }

    public string ActiveHarnessRouteText
    {
        get
        {
            var harness = _harnesses?.GetSnapshot().ActiveHarness;
            return harness is null
                ? "Route: workspace fallback"
                : $"Route: {harness.Kind} / {ValidationScenarioCatalog.DefaultMaxTokens} max tokens";
        }
    }

    public string ActiveHarnessDetailText
    {
        get
        {
            var snapshot = _harnesses?.GetSnapshot();
            var harness = snapshot?.ActiveHarness;
            if (harness is null)
            {
                return "Attach the Conversation Harness in Agents to make Lab turns use the local LM Studio model.";
            }

            return $"{harness.Health}; {snapshot!.CapabilitySummaryText}; {snapshot.WrapperSummaryText}";
        }
    }

    [RelayCommand]
    public async Task StageValidationRunAsync()
    {
        StatusLabel = "Staging validation";
        var result = await _validationRunner.StageSandboxRunAsync(_validationScenarios);
        ValidationRunStatusText = result.Ok
            ? $"{result.Message} Manifest: {result.ManifestPath}"
            : $"Validation staging failed: {result.Message}";
        StatusLabel = result.Ok ? "Ready" : "Validation failed";

        if (result.Ok)
        {
            await RefreshWorkspaceTreeAsync();
        }
    }

    [RelayCommand]
    public Task ExecuteValidationSmokeAsync()
    {
        return QueueValidationAsync(6, "Queued validation smoke.");
    }

    [RelayCommand]
    public Task ExecuteValidationAllAsync()
    {
        return QueueValidationAsync(_validationScenarios.Scenarios.Count, "Queued full validation.");
    }

    [RelayCommand]
    public Task OpenSelectedValidationRunAsync()
    {
        var relativePath = SelectedValidationJob?.ArtifactRootPath;
        var workspace = _workspaces.ActiveWorkspace;
        if (workspace is null || string.IsNullOrWhiteSpace(relativePath))
        {
            StatusLabel = "No validation run folder";
            return Task.CompletedTask;
        }

        var root = Path.GetFullPath(workspace.RootPath);
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(root, StringComparison.Ordinal) || !Directory.Exists(fullPath))
        {
            StatusLabel = "Validation run folder missing";
            return Task.CompletedTask;
        }

        var opener = OperatingSystem.IsMacOS()
            ? "open"
            : OperatingSystem.IsWindows()
                ? "explorer"
                : "xdg-open";

        using var _ = Process.Start(new ProcessStartInfo
        {
            FileName = opener,
            UseShellExecute = false,
            ArgumentList = { fullPath }
        });

        StatusLabel = "Opened validation run";
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task ReviewSelectedValidationJobAsync()
    {
        var workspace = _workspaces.ActiveWorkspace;
        var resultIndexPath = SelectedValidationJob?.ResultIndexPath;
        if (workspace is null || string.IsNullOrWhiteSpace(resultIndexPath))
        {
            SelectedValidationJobReviewText = "Review: no completed validation output selected.";
            StatusLabel = "No validation output";
            return;
        }

        var read = await _workspaces.ReadFileAsync(workspace.Id, resultIndexPath);
        if (!read.Ok)
        {
            SelectedValidationJobReviewText = $"Review: could not read {resultIndexPath}.";
            StatusLabel = "Validation review unavailable";
            return;
        }

        SelectedValidationJobReviewText = BuildValidationReviewText(read.Contents);
        StatusLabel = "Reviewed validation output";
    }

    private async Task QueueValidationAsync(int maxScenarios, string queuedStatus)
    {
        StatusLabel = "Validation queued";
        var job = await _validationJobs.EnqueueAsync(_validationScenarios, maxScenarios);
        ValidationExecutionStatusText = $"{queuedStatus} Job: {job.JobId}; scenarios: {job.RequestedCount}; max tokens: {ValidationScenarioCatalog.DefaultMaxTokens}.";
        RenderValidationJobs(job.JobId);
    }

    private void OnValidationJobStateChanged(object? sender, ValidationScenarioJobState job)
    {
        ApplyValidationJobState(job);
    }

    private void OnHarnessStateChanged(HarnessImporterSnapshot snapshot)
    {
        OnPropertyChanged(nameof(ActiveHarnessStatusText));
        OnPropertyChanged(nameof(ActiveHarnessRouteText));
        OnPropertyChanged(nameof(ActiveHarnessDetailText));
    }

    private void ApplyValidationJobState(ValidationScenarioJobState job)
    {
        ValidationExecutionStatusText = job.Status switch
        {
            ValidationScenarioJobStatus.Queued => $"Queued validation job {job.JobId}; scenarios: {job.RequestedCount}; max tokens: {ValidationScenarioCatalog.DefaultMaxTokens}.",
            ValidationScenarioJobStatus.Running => $"Running validation job {job.JobId}; scenarios: {job.RequestedCount}; max tokens: {ValidationScenarioCatalog.DefaultMaxTokens}.",
            ValidationScenarioJobStatus.Completed when job.Result is not null => $"{job.Message} Ranking: {job.Result.CapabilityRankingPath}; Scoreboard: {job.Result.ScoreboardPath}",
            ValidationScenarioJobStatus.Failed when job.Result is not null => $"Validation execution incomplete: {job.Message} Progress: {job.Result.ProgressPath}",
            ValidationScenarioJobStatus.Failed => $"Validation execution incomplete: {job.Message}",
            _ => job.Message
        };

        RenderValidationJobs(job.JobId);

        StatusLabel = job.Status is ValidationScenarioJobStatus.Running
            ? "Validation running"
            : "Ready";

        if (job.Status is ValidationScenarioJobStatus.Completed or ValidationScenarioJobStatus.Failed)
        {
            _ = RefreshWorkspaceTreeAsync();
        }
    }

    partial void OnSelectedValidationJobChanged(ValidationJobItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedValidationJobTitle));
        OnPropertyChanged(nameof(SelectedValidationJobDetail));
        OnPropertyChanged(nameof(SelectedValidationJobScoreboardText));
        OnPropertyChanged(nameof(SelectedValidationJobRankingText));
        OnPropertyChanged(nameof(SelectedValidationJobProjectsText));
        OnPropertyChanged(nameof(SelectedValidationJobProgressText));
        OnPropertyChanged(nameof(SelectedValidationJobRunFolderText));
        OnPropertyChanged(nameof(SelectedValidationJobLearningText));
        if (value is null || string.IsNullOrWhiteSpace(value.ResultIndexPath))
        {
            SelectedValidationJobReviewText = "Review: run validation, then review the selected output.";
        }
    }

    private void RenderValidationJobs(string? selectedJobId = null)
    {
        selectedJobId ??= SelectedValidationJob?.JobId ?? _validationJobs.ActiveJob?.JobId;
        ValidationJobs.Clear();

        foreach (var job in _validationJobs.GetJobs().OrderByDescending(job => job.CreatedAtUtc).Take(12))
        {
            ValidationJobs.Add(ValidationJobItemViewModel.FromModel(job));
        }

        SelectedValidationJob = ValidationJobs.FirstOrDefault(job => string.Equals(job.JobId, selectedJobId, StringComparison.OrdinalIgnoreCase))
            ?? ValidationJobs.FirstOrDefault();

        OnPropertyChanged(nameof(ValidationJobSummaryText));
        OnPropertyChanged(nameof(SelectedValidationJobTitle));
        OnPropertyChanged(nameof(SelectedValidationJobDetail));
        OnPropertyChanged(nameof(SelectedValidationJobScoreboardText));
        OnPropertyChanged(nameof(SelectedValidationJobRankingText));
        OnPropertyChanged(nameof(SelectedValidationJobProjectsText));
        OnPropertyChanged(nameof(SelectedValidationJobProgressText));
        OnPropertyChanged(nameof(SelectedValidationJobRunFolderText));
        OnPropertyChanged(nameof(SelectedValidationJobLearningText));
    }

    public static string BuildValidationReviewText(string resultIndexJson)
    {
        try
        {
            using var document = JsonDocument.Parse(resultIndexJson);
            var root = document.RootElement;
            var executed = ReadInt(root, "executedCount");
            var passed = ReadInt(root, "passedCount");
            var projects = ReadInt(root, "fullProjectsBuilt");
            var model = ReadString(root, "model");
            var maxTokens = ReadInt(root, "maxTokens");
            var learningNotesPath = ReadString(root, "learningNotesPath");
            var results = root.TryGetProperty("results", out var resultsElement) && resultsElement.ValueKind == JsonValueKind.Array
                ? resultsElement.EnumerateArray().Select(ReadReviewEntry).ToList()
                : new List<ValidationReviewEntry>();

            var top = results
                .GroupBy(result => result.Capability)
                .Select(group => new
                {
                    Capability = group.Key,
                    Count = group.Count(),
                    Passed = group.Count(result => result.Passed),
                    AverageScore = group.Average(result => result.Score),
                    Projects = group.Count(result => result.FullProjectBuilt)
                })
                .OrderByDescending(group => group.Passed)
                .ThenByDescending(group => group.AverageScore)
                .ThenByDescending(group => group.Projects)
                .ThenBy(group => group.Capability, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            var failed = results
                .Where(result => !result.Passed)
                .Take(3)
                .Select(result => result.ScenarioId)
                .ToList();
            var watch = failed.Count == 0 ? "none" : string.Join(", ", failed);
            var topText = top is null
                ? "top: none"
                : $"top: {top.Capability} {top.Passed}/{top.Count} avg {top.AverageScore:0}";
            var learningText = string.IsNullOrWhiteSpace(learningNotesPath)
                ? "learning notes pending"
                : $"learning: {learningNotesPath}";

            return $"Review: {passed}/{executed} passed; {projects} projects; {topText}; watch: {watch}; {learningText}; model {model}; {maxTokens} max tokens.";
        }
        catch (JsonException)
        {
            return "Review: result index is not readable JSON.";
        }
    }

    private static ValidationReviewEntry ReadReviewEntry(JsonElement element)
    {
        return new ValidationReviewEntry(
            ReadString(element, "scenarioId"),
            ReadString(element, "capability"),
            ReadBool(element, "passed"),
            ReadInt(element, "score"),
            ReadBool(element, "fullProjectBuilt"));
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var number)
            ? number
            : 0;
    }

    private static bool ReadBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;
    }

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

        var harness = _harnesses?.GetSnapshot().ActiveHarness;
        if (harness is not null && _harnessTurnRunner is not null && harness.Kind == HarnessKind.Manual)
        {
            await RunHostedHarnessTurnAsync(text, attachments, harness);
        }
        else
        {
            var started = _executionEvents.Publish(ExecutionEventKind.ToolUseStarted, ConversationId, "WorkspaceContext", "Preparing workspace context.");
            AppendMessage(new ChatMessage(
                Guid.NewGuid().ToString("N"),
                ChatMessageRole.Assistant,
                "Recorded the request. No hosted harness is active for this lab chat.",
                DateTimeOffset.UtcNow,
                new[]
                {
                    new ToolUseStep(started.ToolName, "completed", "Workspace context attached.", DateTimeOffset.UtcNow)
                },
                Array.Empty<ChatAttachment>()));
            _executionEvents.Publish(ExecutionEventKind.ToolUseCompleted, ConversationId, "WorkspaceContext", "Workspace context attached.");
        }

        await PersistConversationAsync();
    }

    private async Task RunHostedHarnessTurnAsync(string text, IReadOnlyList<ChatAttachment> attachments, HarnessDefinition harness)
    {
        var activeWorkspace = _workspaces.ActiveWorkspace;
        var workspaceRoot = activeWorkspace?.RootPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var prompt = BuildHarnessPrompt(text, attachments, activeWorkspace?.Name ?? "No workspace");
        var started = _executionEvents.Publish(ExecutionEventKind.ToolUseStarted, ConversationId, harness.Name, $"Sending lab turn to {harness.Name}.");

        StatusLabel = "Harness running";
        var turn = await _harnessTurnRunner!.RunTurnAsync(
            harness,
            new HostedHarnessTurnRequest(prompt, workspaceRoot, TimeSpan.FromMinutes(4)));

        AppendMessage(new ChatMessage(
            Guid.NewGuid().ToString("N"),
            ChatMessageRole.Assistant,
            turn.Ok ? turn.Assistant : $"Harness failed: {turn.Message}",
            DateTimeOffset.UtcNow,
            new[]
            {
                new ToolUseStep(started.ToolName, turn.Ok ? "completed" : "failed", turn.Message, DateTimeOffset.UtcNow)
            },
            Array.Empty<ChatAttachment>()));
        _executionEvents.Publish(
            turn.Ok ? ExecutionEventKind.ToolUseCompleted : ExecutionEventKind.ExecutionFailed,
            ConversationId,
            harness.Name,
            turn.Message);
        StatusLabel = "Ready";
    }

    private static string BuildHarnessPrompt(string text, IReadOnlyList<ChatAttachment> attachments, string workspaceName)
    {
        var attachmentSummary = attachments.Count == 0
            ? "none"
            : string.Join(", ", attachments.Select(attachment => attachment.FileName));

        return string.Join(Environment.NewLine, new[]
        {
            "You are the active NexusCentral hosted harness.",
            $"Workspace: {workspaceName}",
            $"Attachments: {attachmentSummary}",
            "Use the configured local model endpoint. Do not download models.",
            "Answer the user's lab request directly and keep the answer concise.",
            string.Empty,
            text
        });
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

public sealed class ValidationJobItemViewModel
{
    private ValidationJobItemViewModel(ValidationScenarioJobState job)
    {
        JobId = job.JobId;
        Status = job.Status.ToString();
        DisplayName = $"{Status}  {job.RequestedCount} scenarios";
        CreatedText = $"Created: {job.CreatedAtUtc.LocalDateTime:g}";

        if (job.Result is null)
        {
            DetailText = $"{job.Message} Created: {job.CreatedAtUtc.LocalDateTime:g}";
            ScoreboardText = "Scoreboard: none";
            CapabilityRankingText = "Ranking: none";
            FullProjectIndexText = "Projects: none";
            ProgressText = "Progress: none";
            ArtifactRootPath = string.Empty;
            ArtifactRootText = "Run folder: none";
            ResultIndexPath = string.Empty;
            LearningNotesPath = string.Empty;
            LearningNotesText = "Learning: none";
            return;
        }

        DetailText = $"{job.Result.ExecutedCount}/{job.Result.RequestedCount} executed; {job.Result.PassedCount} passed; {job.Result.FullProjectsBuilt} projects built.";
        ScoreboardText = string.IsNullOrWhiteSpace(job.Result.ScoreboardPath)
            ? "Scoreboard: none"
            : $"Scoreboard: {job.Result.ScoreboardPath}";
        CapabilityRankingText = string.IsNullOrWhiteSpace(job.Result.CapabilityRankingPath)
            ? "Ranking: none"
            : $"Ranking: {job.Result.CapabilityRankingPath}";
        FullProjectIndexText = string.IsNullOrWhiteSpace(job.Result.FullProjectIndexPath)
            ? "Projects: none"
            : $"Projects: {job.Result.FullProjectIndexPath}";
        ProgressText = string.IsNullOrWhiteSpace(job.Result.ProgressPath)
            ? "Progress: none"
            : $"Progress: {job.Result.ProgressPath}";
        ResultIndexPath = job.Result.ResultIndexPath;
        ArtifactRootPath = ExtractArtifactRootPath(job.Result);
        ArtifactRootText = string.IsNullOrWhiteSpace(ArtifactRootPath)
            ? "Run folder: none"
            : $"Run folder: {ArtifactRootPath}";
        LearningNotesPath = string.IsNullOrWhiteSpace(ArtifactRootPath)
            ? string.Empty
            : $"{ArtifactRootPath}/learning-notes.md";
        LearningNotesText = string.IsNullOrWhiteSpace(LearningNotesPath)
            ? "Learning: none"
            : $"Learning: {LearningNotesPath}";
    }

    public string JobId { get; }

    public string Status { get; }

    public string DisplayName { get; }

    public string CreatedText { get; }

    public string DetailText { get; }

    public string ScoreboardText { get; }

    public string CapabilityRankingText { get; }

    public string FullProjectIndexText { get; }

    public string ProgressText { get; }

    public string ResultIndexPath { get; }

    public string LearningNotesPath { get; }

    public string LearningNotesText { get; }

    public string ArtifactRootPath { get; }

    public string ArtifactRootText { get; }

    public static ValidationJobItemViewModel FromModel(ValidationScenarioJobState job) => new(job);

    public static string ExtractArtifactRootPath(ValidationScenarioExecutionResult result)
    {
        var path = FirstPath(
            result.ResultIndexPath,
            result.ScoreboardPath,
            result.CapabilityRankingPath,
            result.FullProjectIndexPath,
            result.ProgressPath);
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim('/');
        if (normalized.Contains("../", StringComparison.Ordinal) || normalized.Equals("..", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex <= 0 ? string.Empty : normalized[..separatorIndex];
    }

    private static string FirstPath(params string[] paths)
    {
        return paths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty;
    }
}

internal sealed record ValidationReviewEntry(
    string ScenarioId,
    string Capability,
    bool Passed,
    int Score,
    bool FullProjectBuilt);

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
