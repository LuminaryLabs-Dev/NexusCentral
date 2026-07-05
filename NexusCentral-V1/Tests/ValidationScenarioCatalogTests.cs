using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V1.Core.Config;
using NexusCentral.V1.Core.Logging;
using NexusCentral.V1.Core.State;
using NexusCentral.V1.Features.Harnesses;
using NexusCentral.V1.Features.Lab;
using NexusCentral.V1.Features.Validation;
using NexusCentral.V1.Features.Workspaces;
using NexusCentral.V1.Integrations.Codex;
using Xunit;

namespace NexusCentral.V1.Tests;

public sealed class ValidationScenarioCatalogTests
{
    [Fact]
    public void DefaultCatalogStagesMoreThanOneHundredRankedScenarios()
    {
        var snapshot = ValidationScenarioCatalog.CreateDefault();

        Assert.True(snapshot.Scenarios.Count >= 100);
        Assert.True(snapshot.CapabilityRanking.Count >= 10);
        Assert.All(snapshot.Scenarios, scenario => Assert.Equal(16000, scenario.MaxTokens));
        Assert.All(snapshot.Scenarios, scenario => Assert.Equal("http://10.0.0.38:1234/v1", scenario.EndpointBaseUrl));
        Assert.All(snapshot.Scenarios, scenario => Assert.Equal("lfm2.5-8b-a1b", scenario.ModelId));
    }

    [Fact]
    public void DefaultCatalogIncludesFullProjectSandboxScenarios()
    {
        var snapshot = ValidationScenarioCatalog.CreateDefault();
        var fullProjectScenarios = snapshot.Scenarios
            .Where(scenario => scenario.ExpectedEvidence.Contains("Sandbox project files"))
            .ToList();

        Assert.True(fullProjectScenarios.Count >= 20);
        Assert.Contains(fullProjectScenarios, scenario => scenario.SandboxGoal.Contains("self-hosted operations app"));
    }

    [Fact]
    public async Task StageSandboxRun_WritesScenarioArtifactsIntoActiveWorkspace()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexuscentral-v1-validation-{System.Guid.NewGuid():N}");
        try
        {
            var paths = AppPathSettings.CreateDefault(root);
            var workspaces = new WorkspaceService(paths, new JsonFileStore(), new InMemoryAppLogService());
            var service = new ValidationScenarioRunService(workspaces);
            var snapshot = ValidationScenarioCatalog.CreateDefault();

            var result = await service.StageSandboxRunAsync(snapshot);
            var manifest = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.ManifestPath);
            var ranking = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.RankingPath);

            Assert.True(result.Ok, result.Message);
            Assert.Equal(144, result.ScenarioCount);
            Assert.True(result.FullProjectScenarioCount >= 20);
            Assert.True(manifest.Ok, manifest.Error);
            Assert.Contains("\"scenarioCount\": 144", manifest.Contents);
            Assert.Contains("\"defaultMaxTokens\": 16000", manifest.Contents);
            Assert.Contains("lfm2.5-8b-a1b", manifest.Contents);
            Assert.True(ranking.Ok, ranking.Error);
            Assert.Contains("Capability Ranking", ranking.Contents);
        }
        finally
        {
            if (System.IO.Directory.Exists(root))
            {
                System.IO.Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteSandboxBatch_RunsHostedHarnessAndWritesScoreboardAndProject()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexuscentral-v1-validation-exec-{System.Guid.NewGuid():N}");
        try
        {
            var paths = AppPathSettings.CreateDefault(root);
            var json = new JsonFileStore();
            var workspaces = new WorkspaceService(paths, json, new InMemoryAppLogService());
            var harnesses = new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), new InMemoryAppLogService());
            await harnesses.ImportHarnessAsync(new HarnessImportRequest(
                "conversation-harness",
                "Conversation Harness",
                HarnessKind.Manual,
                "fake-command",
                "test",
                new[] { new HarnessCapabilityDefinition("conversation.chat", "Chat", "Fake chat.", true, false) },
                new[] { new HarnessEndpointDefinition("POST", "/harnesses/conversation-harness/chat-turn", "conversation.chat", "safe", "Fake chat.") }));
            var service = new ValidationScenarioRunService(workspaces, harnesses, new FakeHostedHarnessTurnRunner());
            var scenario = ValidationScenarioCatalog.CreateDefault().Scenarios.First(item => item.ExpectedEvidence.Contains("Sandbox project files"));
            var snapshot = new ValidationScenarioSnapshot(
                new[] { scenario },
                new[] { "1. sandbox project (1 scenarios)" },
                "1 scenario staged",
                "sandbox project",
                "1 full-project sandbox scenario");

            var result = await service.ExecuteSandboxBatchAsync(snapshot, 1);
            var scoreboard = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.ScoreboardPath);
            var progress = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.ProgressPath);
            var progressLog = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.ProgressLogPath);
            var capabilityRanking = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.CapabilityRankingPath);
            var fullProjectIndex = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.FullProjectIndexPath);
            var learningNotes = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, $"validation/scenario-executions/{result.RunId}/learning-notes.md");
            var projectReadme = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, $"validation/scenario-executions/{result.RunId}/projects/{scenario.Id}/README.md");
            var projectProof = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, $"validation/scenario-executions/{result.RunId}/projects/{scenario.Id}/project-proof.json");

            Assert.True(result.Ok, result.Message);
            Assert.Equal(1, result.ExecutedCount);
            Assert.Equal(1, result.PassedCount);
            Assert.Equal(1, result.FullProjectsBuilt);
            Assert.True(scoreboard.Ok, scoreboard.Error);
            Assert.Contains("Validation Scoreboard", scoreboard.Contents);
            Assert.True(progress.Ok, progress.Error);
            Assert.Contains("\"percentComplete\": 100", progress.Contents);
            Assert.True(progressLog.Ok, progressLog.Error);
            Assert.Contains("\"status\":\"started\"", progressLog.Contents);
            Assert.True(capabilityRanking.Ok, capabilityRanking.Error);
            Assert.Contains("Capability Execution Ranking", capabilityRanking.Contents);
            Assert.True(fullProjectIndex.Ok, fullProjectIndex.Error);
            Assert.Contains("Full Project Index", fullProjectIndex.Contents);
            Assert.Contains($"projects/{scenario.Id}", fullProjectIndex.Contents);
            Assert.True(learningNotes.Ok, learningNotes.Error);
            Assert.Contains("Validation Learning Notes", learningNotes.Contents);
            Assert.Contains("Max tokens: `16000`", learningNotes.Contents);
            Assert.True(projectReadme.Ok, projectReadme.Error);
            Assert.Contains("Harness response", projectReadme.Contents);
            Assert.True(projectProof.Ok, projectProof.Error);
            Assert.Contains("\"maxTokens\": 16000", projectProof.Contents);
        }
        finally
        {
            if (System.IO.Directory.Exists(root))
            {
                System.IO.Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteSandboxBatch_IncludesEndpointModelAndMaxTokensInHarnessPrompt()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexuscentral-v1-validation-prompt-{System.Guid.NewGuid():N}");
        try
        {
            var paths = AppPathSettings.CreateDefault(root);
            var json = new JsonFileStore();
            var workspaces = new WorkspaceService(paths, json, new InMemoryAppLogService());
            var harnesses = new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), new InMemoryAppLogService());
            await harnesses.ImportHarnessAsync(new HarnessImportRequest(
                "conversation-harness",
                "Conversation Harness",
                HarnessKind.Manual,
                "fake-command",
                "test",
                new[] { new HarnessCapabilityDefinition("conversation.chat", "Chat", "Fake chat.", true, false) },
                new[] { new HarnessEndpointDefinition("POST", "/harnesses/conversation-harness/chat-turn", "conversation.chat", "safe", "Fake chat.") }));
            var runner = new PromptCapturingHarnessTurnRunner();
            var service = new ValidationScenarioRunService(workspaces, harnesses, runner);

            var result = await service.ExecuteSandboxBatchAsync(ValidationScenarioCatalog.CreateDefault(), 1);

            Assert.True(result.Ok, result.Message);
            Assert.Contains("Endpoint: http://10.0.0.38:1234/v1", runner.LastRequest?.Message);
            Assert.Contains("Model: lfm2.5-8b-a1b", runner.LastRequest?.Message);
            Assert.Contains("Max tokens: 16000", runner.LastRequest?.Message);
        }
        finally
        {
            if (System.IO.Directory.Exists(root))
            {
                System.IO.Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteSandboxBatch_CanRunAllOneHundredFortyFourScenarios()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexuscentral-v1-validation-all-{System.Guid.NewGuid():N}");
        try
        {
            var paths = AppPathSettings.CreateDefault(root);
            var json = new JsonFileStore();
            var workspaces = new WorkspaceService(paths, json, new InMemoryAppLogService());
            var harnesses = new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), new InMemoryAppLogService());
            await harnesses.ImportHarnessAsync(new HarnessImportRequest(
                "conversation-harness",
                "Conversation Harness",
                HarnessKind.Manual,
                "fake-command",
                "test",
                new[] { new HarnessCapabilityDefinition("conversation.chat", "Chat", "Fake chat.", true, false) },
                new[] { new HarnessEndpointDefinition("POST", "/harnesses/conversation-harness/chat-turn", "conversation.chat", "safe", "Fake chat.") }));
            var snapshot = ValidationScenarioCatalog.CreateDefault();
            var service = new ValidationScenarioRunService(workspaces, harnesses, new FakeHostedHarnessTurnRunner());

            var result = await service.ExecuteSandboxBatchAsync(snapshot, snapshot.Scenarios.Count);
            var index = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.ResultIndexPath);
            var scoreboard = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.ScoreboardPath);
            var capabilityRanking = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.CapabilityRankingPath);
            var fullProjectIndex = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.FullProjectIndexPath);
            var progress = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.ProgressPath);
            var progressLog = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, result.ProgressLogPath);
            var learningNotes = await workspaces.ReadFileAsync(workspaces.ActiveWorkspace!.Id, $"validation/scenario-executions/{result.RunId}/learning-notes.md");

            Assert.True(result.Ok, result.Message);
            Assert.Equal(144, result.RequestedCount);
            Assert.Equal(144, result.ExecutedCount);
            Assert.Equal(144, result.PassedCount);
            Assert.Equal(48, result.FullProjectsBuilt);
            Assert.True(index.Ok, index.Error);
            Assert.Contains("\"executedCount\": 144", index.Contents);
            Assert.Contains("\"fullProjectsBuilt\": 48", index.Contents);
            Assert.Contains("\"learningNotesPath\":", index.Contents);
            Assert.True(scoreboard.Ok, scoreboard.Error);
            Assert.Contains("scenario-144", scoreboard.Contents);
            Assert.True(capabilityRanking.Ok, capabilityRanking.Error);
            Assert.Contains("| 1 |", capabilityRanking.Contents);
            Assert.Contains("Ranking is based on actual execution results", capabilityRanking.Contents);
            Assert.True(fullProjectIndex.Ok, fullProjectIndex.Error);
            Assert.Contains("Full projects built: `48`", fullProjectIndex.Contents);
            Assert.Contains("project-proof.json", fullProjectIndex.Contents);
            Assert.True(progress.Ok, progress.Error);
            Assert.Contains("\"executedCount\": 144", progress.Contents);
            Assert.True(progressLog.Ok, progressLog.Error);
            Assert.True(progressLog.Contents.Split('\n').Count(line => line.Contains("\"runId\"")) >= 145);
            Assert.True(learningNotes.Ok, learningNotes.Error);
            Assert.Contains("What To Improve Next", learningNotes.Contents);
            Assert.Contains("Weakest Capabilities", learningNotes.Contents);
        }
        finally
        {
            if (System.IO.Directory.Exists(root))
            {
                System.IO.Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteSandboxBatch_PenalizesInventedMetricClaims()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexuscentral-v1-validation-metric-{System.Guid.NewGuid():N}");
        try
        {
            var paths = AppPathSettings.CreateDefault(root);
            var json = new JsonFileStore();
            var workspaces = new WorkspaceService(paths, json, new InMemoryAppLogService());
            var harnesses = new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), new InMemoryAppLogService());
            await harnesses.ImportHarnessAsync(new HarnessImportRequest(
                "conversation-harness",
                "Conversation Harness",
                HarnessKind.Manual,
                "fake-command",
                "test",
                new[] { new HarnessCapabilityDefinition("conversation.chat", "Chat", "Fake chat.", true, false) },
                new[] { new HarnessEndpointDefinition("POST", "/harnesses/conversation-harness/chat-turn", "conversation.chat", "safe", "Fake chat.") }));
            var snapshot = ValidationScenarioCatalog.CreateDefault();
            var service = new ValidationScenarioRunService(workspaces, harnesses, new MetricInventingHarnessTurnRunner());

            var result = await service.ExecuteSandboxBatchAsync(snapshot, 1);

            Assert.False(result.Ok);
            Assert.Equal(1, result.ExecutedCount);
            Assert.Equal(0, result.PassedCount);
        }
        finally
        {
            if (System.IO.Directory.Exists(root))
            {
                System.IO.Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecuteSandboxBatch_PenalizesInventedTicketOrPersonEvidence()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexuscentral-v1-validation-ticket-{System.Guid.NewGuid():N}");
        try
        {
            var paths = AppPathSettings.CreateDefault(root);
            var json = new JsonFileStore();
            var workspaces = new WorkspaceService(paths, json, new InMemoryAppLogService());
            var harnesses = new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), new InMemoryAppLogService());
            await harnesses.ImportHarnessAsync(new HarnessImportRequest(
                "conversation-harness",
                "Conversation Harness",
                HarnessKind.Manual,
                "fake-command",
                "test",
                new[] { new HarnessCapabilityDefinition("conversation.chat", "Chat", "Fake chat.", true, false) },
                new[] { new HarnessEndpointDefinition("POST", "/harnesses/conversation-harness/chat-turn", "conversation.chat", "safe", "Fake chat.") }));
            var snapshot = ValidationScenarioCatalog.CreateDefault();
            var service = new ValidationScenarioRunService(workspaces, harnesses, new TicketInventingHarnessTurnRunner());

            var result = await service.ExecuteSandboxBatchAsync(snapshot, 1);

            Assert.False(result.Ok);
            Assert.Equal(1, result.ExecutedCount);
            Assert.Equal(0, result.PassedCount);
        }
        finally
        {
            if (System.IO.Directory.Exists(root))
            {
                System.IO.Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ValidationScenarioJobService_QueuesLongRunWithoutBlockingCaller()
    {
        var snapshot = ValidationScenarioCatalog.CreateDefault();
        var runner = new BlockingValidationRunner();
        var jobs = new ValidationScenarioJobService(runner);
        var stopwatch = Stopwatch.StartNew();

        var job = await jobs.EnqueueAsync(snapshot, 6);

        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds < 250);
        Assert.Equal(6, job.RequestedCount);
        Assert.Contains("16000", job.Message);

        runner.Complete(new ValidationScenarioExecutionResult(
            true,
            "job-run",
            6,
            6,
            6,
            1,
            "progress.json",
            "progress.jsonl",
            "result-index.json",
            "scoreboard.md",
            "capability-ranking.md",
            "full-projects.md",
            "Executed 6 scenarios; 6 passed; 1 sandbox projects built."));

        var completed = await WaitForJobStatusAsync(jobs, ValidationScenarioJobStatus.Completed);

        Assert.Equal(ValidationScenarioJobStatus.Completed, completed.Status);
        Assert.Equal(6, runner.LastMaxScenarios);
        Assert.NotNull(completed.Result);
        Assert.Equal("scoreboard.md", completed.Result.ScoreboardPath);
    }

    [Fact]
    public async Task ValidationScenarioJobService_RunsQueuedJobsSerially()
    {
        var snapshot = ValidationScenarioCatalog.CreateDefault();
        var runner = new SerialBlockingValidationRunner();
        var jobs = new ValidationScenarioJobService(runner);

        await jobs.EnqueueAsync(snapshot, 6);
        await WaitUntilAsync(() => runner.StartedCount == 1);
        await jobs.EnqueueAsync(snapshot, 12);
        await Task.Delay(75);

        Assert.Equal(1, runner.StartedCount);

        runner.CompleteNext(new ValidationScenarioExecutionResult(true, "first-run", 6, 6, 6, 1, "first-progress.json", "first-progress.jsonl", "first-index.json", "first-scoreboard.md", "first-ranking.md", "first-projects.md", "First complete."));
        await WaitUntilAsync(() => runner.StartedCount == 2);

        runner.CompleteNext(new ValidationScenarioExecutionResult(true, "second-run", 12, 12, 12, 1, "second-progress.json", "second-progress.jsonl", "second-index.json", "second-scoreboard.md", "second-ranking.md", "second-projects.md", "Second complete."));
        var completed = await WaitForJobStatusAsync(jobs, ValidationScenarioJobStatus.Completed);

        Assert.Equal(2, runner.StartedCount);
        Assert.Equal("second-run", completed.Result?.RunId);
    }

    [Fact]
    public async Task ValidationScenarioJobService_RestoresCompletedJobRegistry()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexuscentral-v1-validation-jobs-{System.Guid.NewGuid():N}");
        try
        {
            var paths = AppPathSettings.CreateDefault(root);
            var json = new JsonFileStore();
            var snapshot = ValidationScenarioCatalog.CreateDefault();
            var runner = new ImmediateValidationRunner();
            var jobs = new ValidationScenarioJobService(runner, paths, json);

            await jobs.EnqueueAsync(snapshot, 6);
            var completed = await WaitForJobStatusAsync(jobs, ValidationScenarioJobStatus.Completed);

            var restored = new ValidationScenarioJobService(new ImmediateValidationRunner(), paths, json);
            var active = restored.ActiveJob;

            Assert.NotNull(active);
            Assert.Equal(completed.JobId, active!.JobId);
            Assert.Equal(ValidationScenarioJobStatus.Completed, active.Status);
            Assert.Equal("restored-scoreboard.md", active.Result?.ScoreboardPath);
        }
        finally
        {
            if (System.IO.Directory.Exists(root))
            {
                System.IO.Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ValidationJobItemViewModel_ExposesRunFolderForReview()
    {
        var result = new ValidationScenarioExecutionResult(
            true,
            "execution-20260705",
            144,
            144,
            144,
            48,
            "validation/scenario-executions/execution-20260705/progress.json",
            "validation/scenario-executions/execution-20260705/progress.jsonl",
            "validation/scenario-executions/execution-20260705/result-index.json",
            "validation/scenario-executions/execution-20260705/scoreboard.md",
            "validation/scenario-executions/execution-20260705/capability-ranking.md",
            "validation/scenario-executions/execution-20260705/full-project-index.md",
            "Executed 144 scenarios.");
        var job = new ValidationScenarioJobState(
            "job-review",
            ValidationScenarioJobStatus.Completed,
            144,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            result,
            "Complete");

        var item = ValidationJobItemViewModel.FromModel(job);

        Assert.Equal("validation/scenario-executions/execution-20260705", item.ArtifactRootPath);
        Assert.Equal("Run folder: validation/scenario-executions/execution-20260705", item.ArtifactRootText);
        Assert.Equal("validation/scenario-executions/execution-20260705/learning-notes.md", item.LearningNotesPath);
        Assert.Equal("Learning: validation/scenario-executions/execution-20260705/learning-notes.md", item.LearningNotesText);
    }

    [Fact]
    public async Task ValidationScenarioJobService_MarksInterruptedQueuedOrRunningJobsFailedOnRestore()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexuscentral-v1-validation-interrupted-{System.Guid.NewGuid():N}");
        try
        {
            var paths = AppPathSettings.CreateDefault(root);
            var json = new JsonFileStore();
            await json.WriteAsync(
                paths.ValidationJobRegistryPath,
                new ValidationScenarioJobRegistry(new[]
                {
                    new ValidationScenarioJobState(
                        "interrupted-job",
                        ValidationScenarioJobStatus.Running,
                        144,
                        DateTimeOffset.UtcNow.AddMinutes(-5),
                        DateTimeOffset.UtcNow.AddMinutes(-4),
                        null,
                        null,
                        "Running before restart.")
                }));

            var restored = new ValidationScenarioJobService(new ImmediateValidationRunner(), paths, json);
            var active = restored.ActiveJob;

            Assert.NotNull(active);
            Assert.Equal(ValidationScenarioJobStatus.Failed, active!.Status);
            Assert.Contains("did not complete", active.Message);
            Assert.NotNull(active.CompletedAtUtc);
        }
        finally
        {
            if (System.IO.Directory.Exists(root))
            {
                System.IO.Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class FakeHostedHarnessTurnRunner : IHostedHarnessTurnRunner
    {
        public Task<HostedHarnessTurnResult> RunTurnAsync(HarnessDefinition harness, HostedHarnessTurnRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HostedHarnessTurnResult(
                true,
                harness.Id,
                "Plan: build the project. Evidence: project files and validation output. Risk: fail if validation is missing. The project is ready.",
                "{}",
                string.Empty,
                0,
                "Fake turn complete.",
                System.TimeSpan.FromMilliseconds(1)));
        }
    }

    private sealed class PromptCapturingHarnessTurnRunner : IHostedHarnessTurnRunner
    {
        public HostedHarnessTurnRequest? LastRequest { get; private set; }

        public Task<HostedHarnessTurnResult> RunTurnAsync(HarnessDefinition harness, HostedHarnessTurnRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new HostedHarnessTurnResult(
                true,
                harness.Id,
                "Plan: use the configured endpoint. Evidence: validation output. Risk: missing proof. Project: sandbox project path.",
                "{}",
                string.Empty,
                0,
                "Fake turn complete.",
                System.TimeSpan.FromMilliseconds(1)));
        }
    }

    private sealed class MetricInventingHarnessTurnRunner : IHostedHarnessTurnRunner
    {
        public Task<HostedHarnessTurnResult> RunTurnAsync(HarnessDefinition harness, HostedHarnessTurnRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HostedHarnessTurnResult(
                true,
                harness.Id,
                "Plan: validate it. Evidence: benchmark data. Risk: low. Project: ready with a 95% success rate.",
                "{}",
                string.Empty,
                0,
                "Fake turn complete.",
                System.TimeSpan.FromMilliseconds(1)));
        }
    }

    private sealed class TicketInventingHarnessTurnRunner : IHostedHarnessTurnRunner
    {
        public Task<HostedHarnessTurnResult> RunTurnAsync(HarnessDefinition harness, HostedHarnessTurnRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HostedHarnessTurnResult(
                true,
                harness.Id,
                "Plan: validate it. Evidence: output exists. Risk: unclear. Project: Ticket #5678, dev Alex Rivera.",
                "{}",
                string.Empty,
                0,
                "Fake turn complete.",
                System.TimeSpan.FromMilliseconds(1)));
        }
    }

    private sealed class BlockingValidationRunner : IValidationScenarioRunService
    {
        private readonly TaskCompletionSource<ValidationScenarioExecutionResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int LastMaxScenarios { get; private set; }

        public Task<ValidationScenarioRunResult> StageSandboxRunAsync(ValidationScenarioSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ValidationScenarioRunResult(true, ValidationScenarioRunStatus.Staged, "stage-run", snapshot.Scenarios.Count, 0, "manifest.json", "ranking.md", "summary.md", "Staged."));
        }

        public async Task<ValidationScenarioExecutionResult> ExecuteSandboxBatchAsync(ValidationScenarioSnapshot snapshot, int maxScenarios, CancellationToken cancellationToken = default)
        {
            LastMaxScenarios = maxScenarios;
            return await _completion.Task.WaitAsync(cancellationToken);
        }

        public void Complete(ValidationScenarioExecutionResult result)
        {
            _completion.TrySetResult(result);
        }
    }

    private sealed class SerialBlockingValidationRunner : IValidationScenarioRunService
    {
        private readonly object _gate = new();
        private readonly Queue<TaskCompletionSource<ValidationScenarioExecutionResult>> _pending = new();

        public int StartedCount { get; private set; }

        public Task<ValidationScenarioRunResult> StageSandboxRunAsync(ValidationScenarioSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ValidationScenarioRunResult(true, ValidationScenarioRunStatus.Staged, "stage-run", snapshot.Scenarios.Count, 0, "manifest.json", "ranking.md", "summary.md", "Staged."));
        }

        public async Task<ValidationScenarioExecutionResult> ExecuteSandboxBatchAsync(ValidationScenarioSnapshot snapshot, int maxScenarios, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<ValidationScenarioExecutionResult> completion;
            lock (_gate)
            {
                StartedCount++;
                completion = new TaskCompletionSource<ValidationScenarioExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pending.Enqueue(completion);
            }

            return await completion.Task.WaitAsync(cancellationToken);
        }

        public void CompleteNext(ValidationScenarioExecutionResult result)
        {
            TaskCompletionSource<ValidationScenarioExecutionResult> completion;
            lock (_gate)
            {
                completion = _pending.Dequeue();
            }

            completion.TrySetResult(result);
        }
    }

    private sealed class ImmediateValidationRunner : IValidationScenarioRunService
    {
        public Task<ValidationScenarioRunResult> StageSandboxRunAsync(ValidationScenarioSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ValidationScenarioRunResult(true, ValidationScenarioRunStatus.Staged, "stage-run", snapshot.Scenarios.Count, 0, "manifest.json", "ranking.md", "summary.md", "Staged."));
        }

        public Task<ValidationScenarioExecutionResult> ExecuteSandboxBatchAsync(ValidationScenarioSnapshot snapshot, int maxScenarios, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ValidationScenarioExecutionResult(
                true,
                "restored-run",
                maxScenarios,
                maxScenarios,
                maxScenarios,
                1,
                "restored-progress.json",
                "restored-progress.jsonl",
                "restored-index.json",
                "restored-scoreboard.md",
                "restored-ranking.md",
                "restored-projects.md",
                $"Executed {maxScenarios} scenarios."));
        }
    }

    private static async Task<ValidationScenarioJobState> WaitForJobStatusAsync(
        IValidationScenarioJobService jobs,
        ValidationScenarioJobStatus status)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var active = jobs.ActiveJob;
            if (active?.Status == status)
            {
                return active;
            }

            await Task.Delay(20);
        }

        throw new Xunit.Sdk.XunitException($"Validation job did not reach {status}.");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new Xunit.Sdk.XunitException("Condition was not reached.");
    }

    private sealed class FakeCodexCliDiscovery : ICodexCliDiscovery
    {
        public Task<CodexCliDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CodexCliDiscoveryResult(
                true,
                "/usr/local/bin/codex",
                "codex-cli 1.0.0",
                new[] { "exec" },
                new[] { "--json" },
                "Fake Codex CLI."));
        }
    }
}
