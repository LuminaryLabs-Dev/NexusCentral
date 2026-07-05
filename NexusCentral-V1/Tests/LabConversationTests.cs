using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NexusCentral.V1.Core.Config;
using NexusCentral.V1.Core.Execution;
using NexusCentral.V1.Core.Logging;
using NexusCentral.V1.Core.State;
using NexusCentral.V1.Features.Harnesses;
using NexusCentral.V1.Features.Lab;
using NexusCentral.V1.Features.Validation;
using NexusCentral.V1.Features.Workspaces;
using Xunit;

namespace NexusCentral.V1.Tests;

public sealed class LabConversationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"nexuscentral-v1-lab-tests-{Guid.NewGuid():N}");

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
    public async Task StageValidationRunAsync_WritesValidationRunIntoSandbox()
    {
        var services = CreateLabServices();

        await services.Lab.InitializeAsync();
        await services.Lab.StageValidationRunAsync();

        Assert.Contains("Staged 144 validation scenarios", services.Lab.ValidationRunStatusText);
        Assert.Contains(services.Lab.WorkspaceTree, node => node.RelativePath.Contains("scenario-manifest.json"));
    }

    [Fact]
    public async Task ExecuteValidationSmokeAsync_QueuesValidationJobWithoutOwningExecution()
    {
        var jobs = new ImmediateValidationJobService();
        var services = CreateLabServices(validationJobs: jobs);

        await services.Lab.InitializeAsync();
        await services.Lab.ExecuteValidationSmokeAsync();

        Assert.Equal(6, jobs.LastMaxScenarios);
        Assert.Contains("Queued validation smoke", services.Lab.ValidationExecutionStatusText);
        Assert.Contains("max tokens: 16000", services.Lab.ValidationExecutionStatusText);
        Assert.Single(services.Lab.ValidationJobs);
        Assert.Contains("6 scenarios", services.Lab.SelectedValidationJobTitle);
        Assert.Contains("16000 max tokens", services.Lab.SelectedValidationJobDetail);
    }

    [Fact]
    public async Task ReviewSelectedValidationJobAsync_SummarizesResultIndex()
    {
        const string resultIndexPath = "validation/scenario-executions/review-run/result-index.json";
        var result = new ValidationScenarioExecutionResult(
            true,
            "review-run",
            2,
            2,
            1,
            1,
            "validation/scenario-executions/review-run/progress.json",
            "validation/scenario-executions/review-run/progress.jsonl",
            resultIndexPath,
            "validation/scenario-executions/review-run/scoreboard.md",
            "validation/scenario-executions/review-run/capability-ranking.md",
            "validation/scenario-executions/review-run/full-project-index.md",
            "Executed review run.");
        var services = CreateLabServices(validationJobs: new CompletedValidationJobService(result));
        var workspaceId = services.Workspaces.ActiveWorkspace!.Id;
        await services.Workspaces.WriteFileAsync(
            workspaceId,
            resultIndexPath,
            """
            {
              "executedCount": 2,
              "passedCount": 1,
              "fullProjectsBuilt": 1,
              "learningNotesPath": "validation/scenario-executions/review-run/learning-notes.md",
              "model": "lfm2.5-8b-a1b",
              "maxTokens": 16000,
              "results": [
                { "scenarioId": "scenario-001", "capability": "conversation", "passed": true, "score": 88, "fullProjectBuilt": true },
                { "scenarioId": "scenario-002", "capability": "testing", "passed": false, "score": 40, "fullProjectBuilt": false }
              ]
            }
            """);

        await services.Lab.InitializeAsync();
        await services.Lab.ReviewSelectedValidationJobAsync();

        Assert.Contains("1/2 passed", services.Lab.SelectedValidationJobReviewText);
        Assert.Contains("1 projects", services.Lab.SelectedValidationJobReviewText);
        Assert.Contains("top: conversation", services.Lab.SelectedValidationJobReviewText);
        Assert.Contains("watch: scenario-002", services.Lab.SelectedValidationJobReviewText);
        Assert.Contains("learning: validation/scenario-executions/review-run/learning-notes.md", services.Lab.SelectedValidationJobReviewText);
        Assert.Contains("16000 max tokens", services.Lab.SelectedValidationJobReviewText);
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
    public async Task SendMessageAsync_RunsActiveHostedHarnessWhenAttached()
    {
        var harness = new HarnessDefinition(
            "conversation-harness",
            "Conversation Harness",
            HarnessKind.Manual,
            "fake-command",
            "test",
            HarnessHealth.Discovered,
            DateTimeOffset.UtcNow,
            "Ready.",
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { new HarnessCapabilityDefinition("conversation.chat", "Chat", "Fake chat.", true, false) },
            new[] { new HarnessEndpointDefinition("POST", "/harnesses/conversation-harness/chat-turn", "conversation.chat", "safe", "Fake chat.") });
        var harnesses = new FakeHarnessImporterService(harness);
        var runner = new FakeHostedHarnessTurnRunner("Harness answer from LFM.");
        var services = CreateLabServices(harnesses: harnesses, harnessTurnRunner: runner);

        await services.Lab.InitializeAsync();
        services.Lab.InputText = "Use the harness.";
        await services.Lab.SendMessageAsync();

        var transcript = string.Join("\n", services.Lab.Transcript.Select(item => item.FormattedText));
        var events = services.Execution.GetEvents();

        Assert.Equal("Harness: Conversation Harness", services.Lab.ActiveHarnessStatusText);
        Assert.Contains("16000 max tokens", services.Lab.ActiveHarnessRouteText);
        Assert.Contains("Harness answer from LFM.", transcript);
        Assert.Contains("Use the configured local model endpoint", runner.LastRequest?.Message);
        Assert.Equal(services.Workspaces.ActiveWorkspace!.RootPath, runner.LastRequest?.WorkingDirectory);
        Assert.Contains(events, item => item.ToolName == "Conversation Harness" && item.Kind == ExecutionEventKind.ToolUseCompleted);
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

    private LabServiceSet CreateLabServices(
        IValidationScenarioRunService? validationRunner = null,
        IValidationScenarioJobService? validationJobs = null,
        IHarnessImporterService? harnesses = null,
        IHostedHarnessTurnRunner? harnessTurnRunner = null)
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "state"));
        var json = new JsonFileStore();
        var workspaces = new WorkspaceService(paths, json, new InMemoryAppLogService());
        var sessions = new FileWorkspaceSessionStore(paths, json);
        var conversations = new FileLabConversationStore(paths, json);
        var execution = new InMemoryExecutionEventStream();
        var lab = new LabViewModel(
            workspaces,
            sessions,
            conversations,
            execution,
            harnesses: harnesses,
            harnessTurnRunner: harnessTurnRunner,
            validationRunner: validationRunner,
            validationJobs: validationJobs);

        return new LabServiceSet(workspaces, sessions, execution, lab);
    }

    private sealed class FakeHarnessImporterService : IHarnessImporterService
    {
        private readonly HarnessDefinition? _activeHarness;

        public FakeHarnessImporterService(HarnessDefinition? activeHarness)
        {
            _activeHarness = activeHarness;
        }

        public event Action<HarnessImporterSnapshot>? StateChanged
        {
            add { }
            remove { }
        }

        public HarnessImporterSnapshot GetSnapshot()
        {
            var harnesses = _activeHarness is null ? Array.Empty<HarnessDefinition>() : new[] { _activeHarness };
            var steps = _activeHarness is null
                ? Array.Empty<HarnessOrchestrationStep>()
                : new[]
                {
                    new HarnessOrchestrationStep(
                        1,
                        _activeHarness.Id,
                        _activeHarness.Name,
                        "Hosted conversation turn",
                        _activeHarness.Capabilities.FirstOrDefault()?.Id ?? string.Empty,
                        _activeHarness.ProposedEndpoints.FirstOrDefault()?.Route ?? "No endpoint proposed",
                        "Ready")
                };
            return new HarnessImporterSnapshot(_activeHarness, harnesses, "Harness: fake", "1 capability", "1 endpoint", "Orchestration: fake", steps);
        }

        public Task<HarnessDefinition> DiscoverCodexCliAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<HarnessDefinition> ImportHarnessAsync(HarnessImportRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<HarnessDefinition> ImportManifestAsync(string manifestPath, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<HarnessManifestCandidate>> DiscoverManifestsAsync(string rootDirectory, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<HarnessQuestionResult> AskReadOnlyAsync(HarnessQuestionRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> SelectHarnessAsync(string harnessId, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> SetCapabilityEnabledAsync(string harnessId, string capabilityId, bool isEnabled, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> RemoveHarnessAsync(string harnessId, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeHostedHarnessTurnRunner : IHostedHarnessTurnRunner
    {
        private readonly string _answer;

        public FakeHostedHarnessTurnRunner(string answer)
        {
            _answer = answer;
        }

        public HostedHarnessTurnRequest? LastRequest { get; private set; }

        public Task<HostedHarnessTurnResult> RunTurnAsync(HarnessDefinition harness, HostedHarnessTurnRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new HostedHarnessTurnResult(
                true,
                harness.Id,
                _answer,
                "{}",
                string.Empty,
                0,
                "Fake hosted harness turn complete.",
                TimeSpan.FromMilliseconds(1)));
        }
    }

    private sealed class ImmediateValidationJobService : IValidationScenarioJobService
    {
        public event EventHandler<ValidationScenarioJobState>? StateChanged;

        public ValidationScenarioJobState? ActiveJob { get; private set; }

        public int LastMaxScenarios { get; private set; }

        public IReadOnlyList<ValidationScenarioJobState> GetJobs()
        {
            return ActiveJob is null ? Array.Empty<ValidationScenarioJobState>() : new[] { ActiveJob };
        }

        public Task<ValidationScenarioJobState> EnqueueAsync(ValidationScenarioSnapshot snapshot, int maxScenarios, System.Threading.CancellationToken cancellationToken = default)
        {
            LastMaxScenarios = maxScenarios;
            ActiveJob = new ValidationScenarioJobState(
                "test-job",
                ValidationScenarioJobStatus.Queued,
                maxScenarios,
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                "Queued test job at 16000 max tokens.");
            StateChanged?.Invoke(this, ActiveJob);
            return Task.FromResult(ActiveJob);
        }
    }

    private sealed class CompletedValidationJobService : IValidationScenarioJobService
    {
        public CompletedValidationJobService(ValidationScenarioExecutionResult result)
        {
            ActiveJob = new ValidationScenarioJobState(
                "completed-test-job",
                ValidationScenarioJobStatus.Completed,
                result.RequestedCount,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                result,
                "Completed test job.");
        }

        public event EventHandler<ValidationScenarioJobState>? StateChanged
        {
            add { }
            remove { }
        }

        public ValidationScenarioJobState? ActiveJob { get; }

        public IReadOnlyList<ValidationScenarioJobState> GetJobs()
        {
            return ActiveJob is null ? Array.Empty<ValidationScenarioJobState>() : new[] { ActiveJob };
        }

        public Task<ValidationScenarioJobState> EnqueueAsync(ValidationScenarioSnapshot snapshot, int maxScenarios, System.Threading.CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record LabServiceSet(
        IWorkspaceService Workspaces,
        IWorkspaceSessionStore Sessions,
        IExecutionEventStream Execution,
        LabViewModel Lab);
}
