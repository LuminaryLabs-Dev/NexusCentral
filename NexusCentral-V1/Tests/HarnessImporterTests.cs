using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V1.Core.Config;
using NexusCentral.V1.Core.Logging;
using NexusCentral.V1.Core.State;
using NexusCentral.V1.Features.Harnesses;
using NexusCentral.V1.Integrations.Codex;
using Xunit;

namespace NexusCentral.V1.Tests;

public sealed class HarnessImporterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"nexuscentral-v1-harness-tests-{Guid.NewGuid():N}");

    [Fact]
    public void EmptyHarnessRegistry_ReportsInactive()
    {
        var service = CreateService(new FakeCodexCliDiscovery());

        var snapshot = service.GetSnapshot();

        Assert.Null(snapshot.ActiveHarness);
        Assert.Equal("Harness: inactive", snapshot.HarnessStatusText);
        Assert.Equal("No harnesses imported", snapshot.CapabilitySummaryText);
    }

    [Fact]
    public async Task DiscoverCodexCli_ImportsHarnessAndCapabilities()
    {
        var service = CreateService(new FakeCodexCliDiscovery());

        var harness = await service.DiscoverCodexCliAsync();
        var snapshot = service.GetSnapshot();

        Assert.Equal("codex-cli", harness.Id);
        Assert.Equal(HarnessHealth.Discovered, harness.Health);
        Assert.Equal("Harness: Codex CLI", snapshot.HarnessStatusText);
        Assert.Contains(harness.Capabilities, capability => capability.Id == "codex.cli.exec.readonly" && capability.IsEnabled);
        Assert.Contains(harness.ProposedEndpoints, endpoint => endpoint.Route == "/harnesses/codex-cli/exec/read-only");
    }

    [Fact]
    public async Task CapabilitySelection_PersistsToRegistry()
    {
        var paths = AppPathSettings.CreateDefault(_root);
        var service = CreateService(new FakeCodexCliDiscovery(), paths);

        await service.DiscoverCodexCliAsync();
        await service.SetCapabilityEnabledAsync("codex-cli", "codex.cli.exec.workspace_write", true);

        var reloaded = CreateService(new FakeCodexCliDiscovery(), paths);
        var capability = reloaded
            .GetSnapshot()
            .ActiveHarness
            ?.Capabilities
            .FirstOrDefault(candidate => candidate.Id == "codex.cli.exec.workspace_write");

        Assert.NotNull(capability);
        Assert.True(capability.IsEnabled);
    }

    [Fact]
    public async Task ImportManualHarness_AttachesAndRemovesHostedHarness()
    {
        var service = CreateService(new FakeCodexCliDiscovery());
        var harness = await service.ImportHarnessAsync(new HarnessImportRequest(
            "conversation-harness",
            "Conversation Harness",
            HarnessKind.Manual,
            "PYTHONPATH=src python3 -m ConversationHarness.cli chat-turn",
            "0.1.0",
            new[]
            {
                new HarnessCapabilityDefinition(
                    "conversation.chat",
                    "Chat turn",
                    "Run one hosted harness turn.",
                    true,
                    false)
            },
            new[]
            {
                new HarnessEndpointDefinition(
                    "POST",
                    "/harnesses/conversation-harness/chat-turn",
                    "conversation.chat",
                    "safe",
                    "Send one message to the hosted harness.")
            }));

        Assert.Equal("conversation-harness", service.GetSnapshot().ActiveHarness?.Id);
        Assert.Equal("Conversation Harness", harness.Name);

        var removed = await service.RemoveHarnessAsync(harness.Id);

        Assert.True(removed);
        Assert.Null(service.GetSnapshot().ActiveHarness);
        Assert.Empty(service.GetSnapshot().Harnesses);
    }

    [Fact]
    public async Task OrchestrationPlan_IncludesEnabledStepsAcrossHarnesses()
    {
        var service = CreateService(new FakeCodexCliDiscovery());
        await service.ImportHarnessAsync(new HarnessImportRequest(
            "conversation-harness",
            "Conversation Harness",
            HarnessKind.Manual,
            "fake-command",
            "test",
            new[] { new HarnessCapabilityDefinition("conversation.chat", "Chat", "Fake chat.", true, false) },
            new[] { new HarnessEndpointDefinition("POST", "/harnesses/conversation-harness/chat-turn", "conversation.chat", "safe", "Fake chat.") }));
        await service.DiscoverCodexCliAsync();
        await service.SelectHarnessAsync("conversation-harness");

        var snapshot = service.GetSnapshot();

        Assert.Contains("enabled steps", snapshot.OrchestrationSummaryText);
        Assert.Contains(snapshot.OrchestrationSteps, step =>
            step.HarnessId == "conversation-harness"
            && step.Role == "Hosted conversation turn"
            && step.EndpointRoute == "POST /harnesses/conversation-harness/chat-turn"
            && step.StatusText == "Ready");
        Assert.Contains(snapshot.OrchestrationSteps, step =>
            step.HarnessId == "codex-cli"
            && step.Role == "Read-only Codex action"
            && step.EndpointRoute == "POST /harnesses/codex-cli/exec/read-only");
    }

    [Fact]
    public async Task OrchestrationSmoke_RunsManualAndCodexReadOnlySteps()
    {
        var paths = AppPathSettings.CreateDefault(_root);
        var json = new JsonFileStore();
        var service = CreateService(new FakeCodexCliDiscovery(), paths, questionRunner: new FakeCodexCliQuestionRunner("codex ready"), json: json);
        await service.ImportHarnessAsync(new HarnessImportRequest(
            "conversation-harness",
            "Conversation Harness",
            HarnessKind.Manual,
            "fake-command",
            "test",
            new[] { new HarnessCapabilityDefinition("conversation.chat", "Chat", "Fake chat.", true, false) },
            new[] { new HarnessEndpointDefinition("POST", "/harnesses/conversation-harness/chat-turn", "conversation.chat", "safe", "Fake chat.") }));
        await service.DiscoverCodexCliAsync();
        await service.SelectHarnessAsync("conversation-harness");
        var runner = new FakeHostedHarnessTurnRunner("manual ready");
        var smoke = new HarnessOrchestrationSmokeService(service, runner, paths, json);

        var result = await smoke.SmokeAsync(new HarnessOrchestrationSmokeRequest(
            "Reply exactly ready.",
            Directory.GetCurrentDirectory(),
            TimeSpan.FromSeconds(5)));

        Assert.True(result.Ok, result.Message);
        Assert.Equal(2, result.ExecutedCount);
        Assert.Equal(2, result.PassedCount);
        Assert.Contains(result.Steps, step => step.HarnessId == "conversation-harness" && step.Output == "manual ready");
        Assert.Contains(result.Steps, step => step.HarnessId == "codex-cli" && step.Output == "codex ready");
        Assert.Equal("Reply exactly ready.", runner.LastRequest?.Message);

        var reloadedSmoke = new HarnessOrchestrationSmokeService(service, runner, paths, json);
        Assert.NotNull(reloadedSmoke.LastResult);
        Assert.Equal(result.RunId, reloadedSmoke.LastResult!.RunId);
        Assert.Equal(2, reloadedSmoke.LastResult.PassedCount);
    }

    [Fact]
    public async Task ImportManifest_AttachesHarnessFolderWithEnvironmentAndEndpoints()
    {
        var service = CreateService(new FakeCodexCliDiscovery());
        var harnessRoot = Path.Combine(_root, "Conversation-Harness");
        Directory.CreateDirectory(harnessRoot);
        var manifestPath = Path.Combine(harnessRoot, "harness.manifest.json");
        await WriteConversationManifestAsync(manifestPath, harnessRoot);

        var harness = await service.ImportManifestAsync(manifestPath);

        Assert.Equal("conversation-harness", harness.Id);
        Assert.Equal(HarnessKind.Manual, harness.Kind);
        Assert.Contains($"cd '{harnessRoot}'", harness.CommandPath);
        Assert.Contains("CONVERSATION_HARNESS_BASE_URL='http://10.0.0.38:1234'", harness.CommandPath);
        Assert.Contains("CONVERSATION_HARNESS_MODEL='lfm2.5-8b-a1b'", harness.CommandPath);
        Assert.Contains("CONVERSATION_HARNESS_MAX_TOKENS='16000'", harness.CommandPath);
        Assert.Contains("PYTHONPATH=src python3 -m ConversationHarness.cli chat-turn", harness.CommandPath);
        Assert.Contains(harness.Capabilities, capability => capability.Id == "conversation.chat" && capability.IsEnabled);
        Assert.Contains(harness.ProposedEndpoints, endpoint => endpoint.Route == "/harnesses/conversation-harness/chat-turn");
    }

    [Fact]
    public async Task DiscoverManifests_FindsHarnessManifestCandidatesUnderRoot()
    {
        var service = CreateService(new FakeCodexCliDiscovery());
        var harnessRoot = Path.Combine(_root, "NexusAgent", "Conversation-Harness");
        Directory.CreateDirectory(harnessRoot);
        var manifestPath = Path.Combine(harnessRoot, "harness.manifest.json");
        await WriteConversationManifestAsync(manifestPath, harnessRoot);

        var candidates = await service.DiscoverManifestsAsync(Path.Combine(_root, "NexusAgent"));

        var candidate = Assert.Single(candidates);
        Assert.Equal(manifestPath, candidate.ManifestPath);
        Assert.Equal("conversation-harness", candidate.Id);
        Assert.Equal("Conversation Harness", candidate.Name);
        Assert.Equal("Ready to import.", candidate.StatusMessage);
    }

    [Fact]
    public async Task HarnessViewModel_ImportsSelectedDiscoveredManifest()
    {
        var service = CreateService(new FakeCodexCliDiscovery());
        var harnessRoot = Path.Combine(_root, "NexusAgent", "Conversation-Harness");
        Directory.CreateDirectory(harnessRoot);
        await WriteConversationManifestAsync(Path.Combine(harnessRoot, "harness.manifest.json"), harnessRoot);
        var viewModel = new HarnessImporterViewModel(service)
        {
            ManifestSearchRoot = Path.Combine(_root, "NexusAgent")
        };

        await viewModel.DiscoverManifestsCommand.ExecuteAsync(null);
        await viewModel.ImportSelectedManifestCommand.ExecuteAsync(null);

        Assert.Single(viewModel.ManifestCandidates);
        Assert.Equal("conversation-harness", service.GetSnapshot().ActiveHarness?.Id);
        Assert.Equal("Conversation Harness", viewModel.ActiveHarnessTitle);
        Assert.Contains("Imported Conversation Harness manifest.", viewModel.NewHarnessStatusText);
    }

    [Fact]
    public async Task HarnessViewModel_ReflectsDiscoveryState()
    {
        var service = CreateService(new FakeCodexCliDiscovery());
        var viewModel = new HarnessImporterViewModel(service);

        await viewModel.DiscoverCodexCliCommand.ExecuteAsync(null);

        Assert.Equal("Discovered", viewModel.StatusLabel);
        Assert.Equal("Codex CLI", viewModel.ActiveHarnessTitle);
        Assert.NotEmpty(viewModel.Capabilities);
        Assert.NotEmpty(viewModel.ProposedEndpoints);
        Assert.NotEmpty(viewModel.OrchestrationSteps);
        Assert.Contains("Orchestration:", viewModel.OrchestrationSummaryText);
    }

    [Fact]
    public async Task HarnessViewModel_TestSelectedHarnessRunsActiveManualHarness()
    {
        var service = CreateService(new FakeCodexCliDiscovery());
        await service.ImportHarnessAsync(new HarnessImportRequest(
            "conversation-harness",
            "Conversation Harness",
            HarnessKind.Manual,
            "fake-command",
            "test",
            new[] { new HarnessCapabilityDefinition("conversation.chat", "Chat", "Fake chat.", true, false) },
            new[] { new HarnessEndpointDefinition("POST", "/harnesses/conversation-harness/chat-turn", "conversation.chat", "safe", "Fake chat.") }));
        var runner = new FakeHostedHarnessTurnRunner("harness ready");
        var viewModel = new HarnessImporterViewModel(service, runner);

        await viewModel.TestSelectedHarnessCommand.ExecuteAsync(null);

        Assert.Equal("Passed: harness ready", viewModel.HarnessTestStatusText);
        Assert.Equal("Reply with exactly: harness ready", runner.LastRequest?.Message);
        Assert.Equal("conversation-harness", runner.LastHarnessId);
    }

    [Fact]
    public async Task HarnessViewModel_SmokeOrchestrationReportsServiceSummary()
    {
        var service = CreateService(new FakeCodexCliDiscovery(), questionRunner: new FakeCodexCliQuestionRunner("codex ready"));
        await service.ImportHarnessAsync(new HarnessImportRequest(
            "conversation-harness",
            "Conversation Harness",
            HarnessKind.Manual,
            "fake-command",
            "test",
            new[] { new HarnessCapabilityDefinition("conversation.chat", "Chat", "Fake chat.", true, false) },
            new[] { new HarnessEndpointDefinition("POST", "/harnesses/conversation-harness/chat-turn", "conversation.chat", "safe", "Fake chat.") }));
        var runner = new FakeHostedHarnessTurnRunner("manual ready");
        var smoke = new HarnessOrchestrationSmokeService(service, runner);
        var viewModel = new HarnessImporterViewModel(service, runner, smoke)
        {
            OrchestrationSmokePrompt = "Reply exactly ready."
        };

        await viewModel.SmokeOrchestrationCommand.ExecuteAsync(null);

        Assert.Contains("1/1 executed steps passed", viewModel.OrchestrationSmokeStatusText);
        Assert.Contains("Last smoke: 1/1 passed", viewModel.LastOrchestrationSmokeText);
        Assert.Equal("Reply exactly ready.", runner.LastRequest?.Message);
    }

    [Fact]
    public void HarnessViewModel_DefaultAttachCommandUsesLfmEndpointAndSixteenKTokens()
    {
        var service = CreateService(new FakeCodexCliDiscovery());
        var viewModel = new HarnessImporterViewModel(service);

        Assert.Contains("10.0.0.38:1234", viewModel.NewHarnessCommand);
        Assert.Contains("CONVERSATION_HARNESS_MODEL=lfm2.5-8b-a1b", viewModel.NewHarnessCommand);
        Assert.Contains("CONVERSATION_HARNESS_MAX_TOKENS=16000", viewModel.NewHarnessCommand);
    }

    [Fact]
    public async Task AskReadOnly_UsesCodexCliEndpointAndRunner()
    {
        var service = CreateService(new FakeCodexCliDiscovery(), questionRunner: new FakeCodexCliQuestionRunner("fake answer"));

        await service.DiscoverCodexCliAsync();
        var result = await service.AskReadOnlyAsync(new HarnessQuestionRequest(
            "codex-cli",
            "say hi",
            Directory.GetCurrentDirectory(),
            TimeSpan.FromSeconds(5)));

        Assert.True(result.Ok);
        Assert.Equal("POST /harnesses/codex-cli/exec/read-only", result.EndpointRoute);
        Assert.Equal("fake answer", result.Answer);
    }

    [Fact]
    public async Task HostedHarnessTurnRunner_InjectsSixteenKMaxTokenEnvironment()
    {
        var runner = new HostedHarnessTurnRunner();
        var harness = new HarnessDefinition(
            "env-harness",
            "Env Harness",
            HarnessKind.Manual,
            "python3 - <<'PY'\nimport json, os\nprint(json.dumps({'assistant': os.environ.get('CONVERSATION_HARNESS_MAX_TOKENS', '')}))\nPY",
            "test",
            HarnessHealth.Discovered,
            DateTimeOffset.UtcNow,
            "Ready.",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<HarnessCapabilityDefinition>(),
            Array.Empty<HarnessEndpointDefinition>());

        var result = await runner.RunTurnAsync(
            harness,
            new HostedHarnessTurnRequest("env", Directory.GetCurrentDirectory(), TimeSpan.FromSeconds(10)));

        Assert.True(result.Ok, result.Message);
        Assert.Equal("16000", result.Assistant);
    }

    [Fact]
    public async Task HostedHarnessTurnRunner_UsesNextBestWhenAssistantFieldIsEmpty()
    {
        var runner = new HostedHarnessTurnRunner();
        var harness = new HarnessDefinition(
            "fallback-harness",
            "Fallback Harness",
            HarnessKind.Manual,
            "python3 -c 'import json,sys; sys.stdin.read(); print(json.dumps({\"ok\": True, \"assistant\": \"\", \"nextBest\": \"Plan: recover\\nEvidence: fallback output\\nRisk: parse split\\nProject: sandbox\"}))'",
            "test",
            HarnessHealth.Discovered,
            DateTimeOffset.UtcNow,
            "Ready.",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<HarnessCapabilityDefinition>(),
            Array.Empty<HarnessEndpointDefinition>());

        var result = await runner.RunTurnAsync(
            harness,
            new HostedHarnessTurnRequest("fallback", Directory.GetCurrentDirectory(), TimeSpan.FromSeconds(10)));

        Assert.True(result.Ok, result.Message);
        Assert.Contains("Plan: recover", result.Assistant);
        Assert.Contains("Project: sandbox", result.Assistant);
    }

    [Fact]
    public async Task LiveCodexHarnessSmoke_AsksReadOnlyQuestionWhenEnabled()
    {
        if (Environment.GetEnvironmentVariable("NEXUSCENTRAL_RUN_LIVE_CODEX_HARNESS") != "1")
        {
            return;
        }

        var proofPath = "/tmp/nexuscentral-proof/codex-harness-answer.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(proofPath)!);

        var appPaths = AppPathSettings.CreateDefault();
        var service = CreateService(new CodexCliDiscovery(), paths: appPaths, questionRunner: new CodexCliQuestionRunner());
        await service.DiscoverCodexCliAsync();
        var result = await service.AskReadOnlyAsync(new HarnessQuestionRequest(
            "codex-cli",
            "Reply with one short sentence: NexusCentral Codex harness is online.",
            Directory.GetCurrentDirectory(),
            TimeSpan.FromMinutes(3)));

        await File.WriteAllTextAsync(
            proofPath,
            $"ok={result.Ok}\nregistry={appPaths.HarnessRegistryPath}\nendpoint={result.EndpointRoute}\nexit={result.ExitCode}\nmessage={result.Message}\nanswer={result.Answer}\n");

        Assert.True(result.Ok, result.Message);
        Assert.Contains("NexusCentral", result.Answer, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static HarnessImporterService CreateService(
        ICodexCliDiscovery discovery,
        AppPathSettings? paths = null,
        ICodexCliQuestionRunner? questionRunner = null,
        JsonFileStore? json = null)
    {
        return new HarnessImporterService(
            paths ?? AppPathSettings.CreateDefault(Path.Combine(Path.GetTempPath(), $"nexuscentral-v1-harness-{Guid.NewGuid():N}")),
            json ?? new JsonFileStore(),
            discovery,
            questionRunner ?? new FakeCodexCliQuestionRunner("unused"),
            new InMemoryAppLogService());
    }

    private static Task WriteConversationManifestAsync(string manifestPath, string harnessRoot)
    {
        return File.WriteAllTextAsync(manifestPath, """
            {
              "id": "conversation-harness",
              "name": "Conversation Harness",
              "kind": "Manual",
              "version": "0.1.0",
              "workingDirectory": "__ROOT__",
              "command": "PYTHONPATH=src python3 -m ConversationHarness.cli chat-turn",
              "environment": {
                "CONVERSATION_HARNESS_BASE_URL": "http://10.0.0.38:1234",
                "CONVERSATION_HARNESS_MODEL": "lfm2.5-8b-a1b",
                "CONVERSATION_HARNESS_MAX_TOKENS": "16000"
              },
              "capabilities": [
                {
                  "id": "conversation.chat",
                  "name": "Chat turn",
                  "description": "Run one feed-forward conversational turn.",
                  "enabled": true,
                  "requiresApproval": false
                }
              ],
              "endpoints": [
                {
                  "method": "POST",
                  "route": "/harnesses/conversation-harness/chat-turn",
                  "capabilityId": "conversation.chat",
                  "safety": "safe",
                  "description": "Send one message."
                }
              ]
            }
            """.Replace("__ROOT__", harnessRoot.Replace("\\", "\\\\"), StringComparison.Ordinal));
    }

    private sealed class FakeCodexCliDiscovery : ICodexCliDiscovery
    {
        public Task<CodexCliDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CodexCliDiscoveryResult(
                true,
                "/usr/local/bin/codex",
                "codex-cli 1.0.0",
                new[] { "exec", "app-server", "mcp-server", "resume" },
                new[] { "--json", "--sandbox", "--output-schema" },
                "Fake Codex CLI discovered."));
        }
    }

    private sealed class FakeCodexCliQuestionRunner : ICodexCliQuestionRunner
    {
        private readonly string _answer;

        public FakeCodexCliQuestionRunner(string answer)
        {
            _answer = answer;
        }

        public Task<CodexCliQuestionResult> AskReadOnlyAsync(CodexCliQuestionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CodexCliQuestionResult(
                true,
                0,
                _answer,
                _answer,
                string.Empty,
                "Fake Codex CLI answered.",
                TimeSpan.FromMilliseconds(1)));
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

        public string LastHarnessId { get; private set; } = string.Empty;

        public Task<HostedHarnessTurnResult> RunTurnAsync(HarnessDefinition harness, HostedHarnessTurnRequest request, CancellationToken cancellationToken = default)
        {
            LastHarnessId = harness.Id;
            LastRequest = request;
            return Task.FromResult(new HostedHarnessTurnResult(
                true,
                harness.Id,
                _answer,
                "{}",
                string.Empty,
                0,
                "Fake harness answered.",
                TimeSpan.FromMilliseconds(1)));
        }
    }
}
