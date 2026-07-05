using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V1.Core.Config;
using NexusCentral.V1.Core.Execution;
using NexusCentral.V1.Core.Logging;
using NexusCentral.V1.Core.State;
using NexusCentral.V1.Features.Harnesses;
using NexusCentral.V1.Features.Lab;
using NexusCentral.V1.Features.Models;
using NexusCentral.V1.Features.Router;
using NexusCentral.V1.Features.Workspaces;
using NexusCentral.V1.Integrations.Codex;
using NexusCentral.V1.Integrations.Providers;
using NexusCentral.V1.Integrations.Providers.HuggingFace;
using NexusCentral.V1.Desktop.ViewModels;
using Xunit;

namespace NexusCentral.V1.Tests;

public sealed class ModelEndpointAndRouterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"nexuscentral-v1-model-router-tests-{Guid.NewGuid():N}");

    [Fact]
    public void EmptyModelRegistry_ReportsInactive()
    {
        var service = CreateModelService(new FakeModelEndpointProbe(Array.Empty<string>()));

        var snapshot = service.GetSnapshot();

        Assert.Null(snapshot.ActiveEndpoint);
        Assert.Equal("Model: inactive", snapshot.ModelStatusText);
        Assert.Equal("Runtime: inactive", snapshot.RuntimeStatusText);
    }

    [Fact]
    public async Task DevTestLmStudioEndpoint_ProbesModelsAndRecordsHealthyState()
    {
        var service = CreateModelService(new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }));
        var endpoint = await service.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());

        var probed = await service.ProbeEndpointAsync(endpoint.Id);

        Assert.NotNull(probed);
        Assert.Equal(ModelEndpointHealth.Healthy, probed.Health);
        Assert.Equal(ModelEndpointTestFixture.LmStudioBaseUrl, probed.BaseUrl);
        Assert.Contains(ModelEndpointTestFixture.LmStudioModelId, probed.AvailableModelIds);
    }

    [Fact]
    public void ModelsViewModel_DefaultEndpointUsesLfmStudioTarget()
    {
        var service = CreateModelService(new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }));
        var viewModel = new ModelsViewModel(service, new FakeHuggingFaceSearchService());

        Assert.Equal("http://10.0.0.38:1234/v1", viewModel.NewEndpointBaseUrl);
        Assert.Equal("lfm2.5-8b-a1b", viewModel.NewEndpointModelId);
    }

    [Fact]
    public async Task ModelsViewModel_AutoProbesActiveUnprobedEndpoint()
    {
        var service = CreateModelService(new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }));
        await service.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());

        _ = new ModelsViewModel(service, new FakeHuggingFaceSearchService());
        await WaitUntilAsync(() => service.GetSnapshot().ActiveEndpoint?.Health == ModelEndpointHealth.Healthy);

        Assert.Equal("Model: lfm2.5-8b-a1b", service.GetSnapshot().ModelStatusText);
    }

    [Fact]
    public async Task RemoveEndpoint_RemovesRecordAndClearsInactiveWhenLastEndpoint()
    {
        var service = CreateModelService(new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }));
        var endpoint = await service.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());

        var removed = await service.RemoveEndpointAsync(endpoint.Id);
        var snapshot = service.GetSnapshot();

        Assert.True(removed);
        Assert.Empty(snapshot.Endpoints);
        Assert.Null(snapshot.ActiveEndpoint);
        Assert.Equal("Model: inactive", snapshot.ModelStatusText);
    }

    [Fact]
    public async Task AddOrUpdateEndpoint_ReusesExistingEndpointWithSameTarget()
    {
        var service = CreateModelService(new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }));
        var first = await service.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());

        var second = await service.AddOrUpdateEndpointAsync(new ModelEndpointCreateRequest(
            "lm-studio-lfm-a1b",
            "LM Studio LFM",
            ProviderKind.LmStudio,
            ModelEndpointTestFixture.LmStudioBaseUrl,
            ModelEndpointTestFixture.LmStudioModelId));
        var snapshot = service.GetSnapshot();

        Assert.Equal(first.Id, second.Id);
        Assert.Single(snapshot.Endpoints);
        Assert.Equal("LM Studio LAN", snapshot.ActiveEndpoint?.Name);
    }

    [Fact]
    public void RouterReportsInactiveWhenNoHealthyModelEndpointExists()
    {
        var modelService = CreateModelService(new FakeModelEndpointProbe(Array.Empty<string>()));
        var router = CreateRouterService(modelService);

        var snapshot = router.GetSnapshot();

        Assert.Equal(RouterAvailability.Inactive, snapshot.ActiveRoute?.Availability);
        Assert.Equal("Route: inactive", snapshot.RouteStatusText);
        Assert.Equal("Queue inactive", snapshot.QueueSummaryText);
    }

    [Fact]
    public async Task RouterReportsAvailableWhenSelectedModelEndpointIsHealthy()
    {
        var modelService = CreateModelService(new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }));
        var router = CreateRouterService(modelService);
        var endpoint = await modelService.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());

        await modelService.ProbeEndpointAsync(endpoint.Id);
        var snapshot = router.GetSnapshot();

        Assert.Equal(RouterAvailability.Available, snapshot.ActiveRoute?.Availability);
        Assert.Equal("Route: LM Studio LAN", snapshot.RouteStatusText);
        Assert.Equal("0 waiting", snapshot.QueueSummaryText);
    }

    [Fact]
    public async Task RouterIncludesHostedHarnessRouteWhenHarnessAndModelAreReady()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "harness-route-ready"));
        var json = new JsonFileStore();
        var modelService = new ModelEndpointService(
            paths,
            json,
            new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }),
            new InMemoryAppLogService());
        var harnesses = new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), new InMemoryAppLogService());
        var router = new RouterService(paths, json, modelService, harnesses, new InMemoryAppLogService());
        var endpoint = await modelService.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());
        await harnesses.ImportHarnessAsync(CreateManualHarnessRequest());

        await modelService.ProbeEndpointAsync(endpoint.Id);
        var snapshot = router.GetSnapshot();

        var hostedRoute = Assert.Single(snapshot.Routes, route => route.Id == "hosted-harness");
        Assert.Equal(RouterAvailability.Available, hostedRoute.Availability);
        Assert.Equal("Conversation Harness", hostedRoute.EndpointName);
        Assert.Equal(ModelEndpointTestFixture.LmStudioModelId, hostedRoute.TargetModelId);
        Assert.Contains("Hosted harness route available", hostedRoute.StatusMessage);
    }

    [Fact]
    public async Task RouterSmokeSelectedRoute_RunsActiveHostedHarnessRoute()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "harness-route-smoke"));
        var json = new JsonFileStore();
        var modelService = new ModelEndpointService(
            paths,
            json,
            new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }),
            new InMemoryAppLogService());
        var harnesses = new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), new InMemoryAppLogService());
        var runner = new FakeHostedHarnessTurnRunner("route ready");
        var router = new RouterService(paths, json, modelService, harnesses, new InMemoryAppLogService(), runner);
        var endpoint = await modelService.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());
        await harnesses.ImportHarnessAsync(CreateManualHarnessRequest());
        await modelService.ProbeEndpointAsync(endpoint.Id);
        await router.SelectRouteAsync("hosted-harness");

        var result = await router.SmokeSelectedRouteAsync("Reply with route ready.");

        Assert.True(result.Ok, result.Message);
        Assert.Equal("hosted-harness", result.RouteId);
        Assert.Equal("route ready", result.Answer);
        Assert.Equal("conversation-harness", runner.LastHarnessId);
        Assert.Contains("Reply with route ready", runner.LastRequest?.Message);
    }

    [Fact]
    public async Task RouterSelectBestAvailableRoute_PrefersHostedHarnessWhenReady()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "harness-route-best"));
        var json = new JsonFileStore();
        var modelService = new ModelEndpointService(
            paths,
            json,
            new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }),
            new InMemoryAppLogService());
        var harnesses = new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), new InMemoryAppLogService());
        var router = new RouterService(paths, json, modelService, harnesses, new InMemoryAppLogService());
        var endpoint = await modelService.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());
        await harnesses.ImportHarnessAsync(CreateManualHarnessRequest());
        await modelService.ProbeEndpointAsync(endpoint.Id);

        var snapshot = await router.SelectBestAvailableRouteAsync();

        Assert.Equal("hosted-harness", snapshot.ActiveRoute?.Id);
        Assert.Equal(RouterAvailability.Available, snapshot.ActiveRoute?.Availability);
        Assert.Equal("Conversation Harness", snapshot.ActiveRoute?.EndpointName);
    }

    [Fact]
    public async Task RouterSelectBestAvailableRoute_UsesLocalProviderWhenNoHostedHarnessExists()
    {
        var modelService = CreateModelService(new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }));
        var router = CreateRouterService(modelService);
        var endpoint = await modelService.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());
        await modelService.ProbeEndpointAsync(endpoint.Id);

        var snapshot = await router.SelectBestAvailableRouteAsync();

        Assert.Equal("local-provider", snapshot.ActiveRoute?.Id);
        Assert.Equal(RouterAvailability.Available, snapshot.ActiveRoute?.Availability);
    }

    [Fact]
    public async Task RouterViewModel_SmokeSelectedRouteReportsHarnessAnswer()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "harness-route-vm-smoke"));
        var json = new JsonFileStore();
        var modelService = new ModelEndpointService(
            paths,
            json,
            new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }),
            new InMemoryAppLogService());
        var harnesses = new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), new InMemoryAppLogService());
        var router = new RouterService(paths, json, modelService, harnesses, new InMemoryAppLogService(), new FakeHostedHarnessTurnRunner("route ready"));
        var viewModel = new RouterViewModel(router);
        var endpoint = await modelService.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());
        await harnesses.ImportHarnessAsync(CreateManualHarnessRequest());
        await modelService.ProbeEndpointAsync(endpoint.Id);
        await router.SelectRouteAsync("hosted-harness");

        await viewModel.SmokeSelectedRouteCommand.ExecuteAsync(null);

        Assert.Contains("Route smoke passed", viewModel.RouteSmokeStatusText);
        Assert.Contains("route ready", viewModel.RouteSmokeStatusText);
    }

    [Fact]
    public async Task RouterMarksHostedHarnessRouteInactiveWithoutHealthyModelEndpoint()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "harness-route-inactive"));
        var json = new JsonFileStore();
        var modelService = new ModelEndpointService(
            paths,
            json,
            new FakeModelEndpointProbe(Array.Empty<string>()),
            new InMemoryAppLogService());
        var harnesses = new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), new InMemoryAppLogService());
        var router = new RouterService(paths, json, modelService, harnesses, new InMemoryAppLogService());
        await modelService.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());
        await harnesses.ImportHarnessAsync(CreateManualHarnessRequest());

        var snapshot = router.GetSnapshot();

        var hostedRoute = Assert.Single(snapshot.Routes, route => route.Id == "hosted-harness");
        Assert.Equal(RouterAvailability.Inactive, hostedRoute.Availability);
        Assert.Equal("Hosted harness route needs an active manual harness and healthy model endpoint.", hostedRoute.StatusMessage);
    }

    [Fact]
    public async Task MainShellStatusReflectsModelAndRouterServices()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "state"));
        var json = new JsonFileStore();
        var modelService = new ModelEndpointService(
            paths,
            json,
            new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }),
            new InMemoryAppLogService());
        var searchService = new FakeHuggingFaceSearchService();
        var routerService = new RouterService(paths, json, modelService, new InMemoryAppLogService());
        var models = new ModelsViewModel(modelService, searchService);
        var router = new RouterViewModel(routerService);
        var harnesses = new HarnessImporterViewModel(new HarnessImporterService(
            paths,
            json,
            new FakeCodexCliDiscovery(),
            new InMemoryAppLogService()));
        var execution = new InMemoryExecutionEventStream();
        var workspaces = new WorkspaceService(paths, json, new InMemoryAppLogService());
        var sessions = new FileWorkspaceSessionStore(paths, json);
        var conversations = new FileLabConversationStore(paths, json);
        var lab = new LabViewModel(workspaces, sessions, conversations, execution);
        var shell = new MainShellViewModel(lab, models, router, harnesses, new InMemoryAppLogService(), execution);
        var endpoint = await modelService.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());

        await modelService.ProbeEndpointAsync(endpoint.Id);

        Assert.Equal("Model: lfm2.5-8b-a1b", shell.ModelStatusText);
        Assert.Equal("Route: LM Studio LAN", shell.RouteStatusText);
        Assert.Equal("0 waiting", shell.QueueSummaryText);
        Assert.Equal("Setup: import hosted harness", shell.SetupReadinessText);
        Assert.Equal("Agents", shell.SetupActionText);
    }

    [Fact]
    public void MainShellCanStartOnSetupSurfaceForQaProof()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "shell-startup-surface"));
        var json = new JsonFileStore();
        var logs = new InMemoryAppLogService();
        var modelService = new ModelEndpointService(
            paths,
            json,
            new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }),
            logs);
        var routerService = new RouterService(paths, json, modelService, logs);
        var models = new ModelsViewModel(modelService, new FakeHuggingFaceSearchService());
        var router = new RouterViewModel(routerService);
        var harnesses = new HarnessImporterViewModel(new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), logs));
        var execution = new InMemoryExecutionEventStream();
        var workspaces = new WorkspaceService(paths, json, logs);
        var lab = new LabViewModel(
            workspaces,
            new FileWorkspaceSessionStore(paths, json),
            new FileLabConversationStore(paths, json),
            execution);

        var shell = new MainShellViewModel(lab, models, router, harnesses, logs, execution, SurfaceKind.Models);

        Assert.Equal(SurfaceKind.Models, shell.SelectedSurface?.Kind);
    }

    [Fact]
    public async Task MainShellSetupReadinessReportsReadyWhenHostedRouteIsSelected()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "shell-ready"));
        var json = new JsonFileStore();
        var logs = new InMemoryAppLogService();
        var modelService = new ModelEndpointService(
            paths,
            json,
            new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }),
            logs);
        var harnessService = new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), logs);
        var routerService = new RouterService(paths, json, modelService, harnessService, logs);
        var models = new ModelsViewModel(modelService, new FakeHuggingFaceSearchService());
        var harnesses = new HarnessImporterViewModel(harnessService);
        var router = new RouterViewModel(routerService);
        var execution = new InMemoryExecutionEventStream();
        var workspaces = new WorkspaceService(paths, json, logs);
        var lab = new LabViewModel(
            workspaces,
            new FileWorkspaceSessionStore(paths, json),
            new FileLabConversationStore(paths, json),
            execution,
            harnessService);
        var shell = new MainShellViewModel(lab, models, router, harnesses, logs, execution);
        var endpoint = await modelService.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());
        await harnessService.ImportHarnessAsync(CreateManualHarnessRequest());

        await modelService.ProbeEndpointAsync(endpoint.Id);
        await routerService.SelectBestAvailableRouteAsync();

        Assert.Equal("Route: Conversation Harness", shell.RouteStatusText);
        Assert.Equal("Harness: Conversation Harness", shell.HarnessStatusText);
        Assert.Equal("Ready: hosted route + validation", shell.SetupReadinessText);
    }

    [Fact]
    public async Task MainShellResolveSetup_OpensAgentsWhenHarnessIsMissing()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "shell-resolve-agents"));
        var json = new JsonFileStore();
        var logs = new InMemoryAppLogService();
        var modelService = new ModelEndpointService(
            paths,
            json,
            new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }),
            logs);
        var routerService = new RouterService(paths, json, modelService, logs);
        var models = new ModelsViewModel(modelService, new FakeHuggingFaceSearchService());
        var router = new RouterViewModel(routerService);
        var harnesses = new HarnessImporterViewModel(new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), logs));
        var execution = new InMemoryExecutionEventStream();
        var workspaces = new WorkspaceService(paths, json, logs);
        var lab = new LabViewModel(
            workspaces,
            new FileWorkspaceSessionStore(paths, json),
            new FileLabConversationStore(paths, json),
            execution);
        var shell = new MainShellViewModel(lab, models, router, harnesses, logs, execution);
        var endpoint = await modelService.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());
        await modelService.ProbeEndpointAsync(endpoint.Id);

        await shell.ResolveSetupAsync();

        Assert.Equal(SurfaceKind.Agents, shell.SelectedSurface?.Kind);
    }

    [Fact]
    public async Task MainShellResolveSetup_SelectsHostedRouteWhenModelAndHarnessAreReady()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "shell-resolve-route"));
        var json = new JsonFileStore();
        var logs = new InMemoryAppLogService();
        var modelService = new ModelEndpointService(
            paths,
            json,
            new FakeModelEndpointProbe(new[] { ModelEndpointTestFixture.LmStudioModelId }),
            logs);
        var harnessService = new HarnessImporterService(paths, json, new FakeCodexCliDiscovery(), logs);
        var routerService = new RouterService(paths, json, modelService, harnessService, logs);
        var models = new ModelsViewModel(modelService, new FakeHuggingFaceSearchService());
        var harnesses = new HarnessImporterViewModel(harnessService);
        var router = new RouterViewModel(routerService);
        var execution = new InMemoryExecutionEventStream();
        var workspaces = new WorkspaceService(paths, json, logs);
        var lab = new LabViewModel(
            workspaces,
            new FileWorkspaceSessionStore(paths, json),
            new FileLabConversationStore(paths, json),
            execution,
            harnessService);
        var shell = new MainShellViewModel(lab, models, router, harnesses, logs, execution);
        var endpoint = await modelService.AddOrUpdateEndpointAsync(ModelEndpointTestFixture.CreateLmStudioLanRequest());
        await harnessService.ImportHarnessAsync(CreateManualHarnessRequest());
        await modelService.ProbeEndpointAsync(endpoint.Id);

        Assert.Equal("Use best route", shell.SetupActionText);

        await shell.ResolveSetupAsync();

        Assert.Equal("Route: Conversation Harness", shell.RouteStatusText);
        Assert.Equal("Ready: hosted route + validation", shell.SetupReadinessText);
        Assert.Equal("Lab", shell.SetupActionText);
    }

    [Fact]
    public async Task HuggingFaceSearchService_FindsAndSortsModels()
    {
        using var client = new HttpClient(new FakeHuggingFaceSearchHandler())
        {
            BaseAddress = new Uri("https://huggingface.co")
        };
        var service = new HuggingFaceModelSearchService(client);

        var snapshot = await service.SearchAsync(new HuggingFaceModelSearchRequest("qwen", 20));

        Assert.Equal(HuggingFaceSearchState.Ready, snapshot.State);
        Assert.Equal("qwen", snapshot.Query);
        Assert.Single(snapshot.Results);
        Assert.Equal("nvidia/Qwen3.6-27B-NVFP4", snapshot.Results[0].Id);
        Assert.Equal(232, snapshot.Results[0].Likes);
    }

    [Fact]
    public async Task OpenAiCompatibleProbeParsesModelIds()
    {
        using var client = new HttpClient(new FakeModelsHandler())
        {
            BaseAddress = new Uri("http://localhost")
        };
        var probe = new OpenAiCompatibleModelProbe(client);
        var endpoint = new ModelEndpointDefinition(
            "local",
            "Local",
            ProviderKind.OpenAiCompatible,
            "http://localhost/v1",
            "lfm2.5-8b-a1b",
            ModelEndpointHealth.Unknown,
            null,
            "Not probed",
            Array.Empty<string>());

        var result = await probe.ProbeAsync(endpoint);

        Assert.True(result.Ok);
        Assert.Contains("lfm2.5-8b-a1b", result.ModelIds);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ModelEndpointService CreateModelService(IModelEndpointProbe probe)
    {
        return new ModelEndpointService(
            AppPathSettings.CreateDefault(Path.Combine(_root, Guid.NewGuid().ToString("N"))),
            new JsonFileStore(),
            probe,
            new InMemoryAppLogService());
    }

    private RouterService CreateRouterService(IModelEndpointService models)
    {
        return new RouterService(
            AppPathSettings.CreateDefault(Path.Combine(_root, Guid.NewGuid().ToString("N"))),
            new JsonFileStore(),
            models,
            new InMemoryAppLogService());
    }

    private static HarnessImportRequest CreateManualHarnessRequest()
    {
        return new HarnessImportRequest(
            "conversation-harness",
            "Conversation Harness",
            HarnessKind.Manual,
            "fake-command",
            "test",
            new[] { new HarnessCapabilityDefinition("conversation.chat", "Chat", "Fake chat.", true, false) },
            new[] { new HarnessEndpointDefinition("POST", "/harnesses/conversation-harness/chat-turn", "conversation.chat", "safe", "Fake chat.") });
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

    private sealed class FakeModelEndpointProbe : IModelEndpointProbe
    {
        private readonly string[] _modelIds;

        public FakeModelEndpointProbe(string[] modelIds)
        {
            _modelIds = modelIds;
        }

        public Task<ModelEndpointProbeResult> ProbeAsync(ModelEndpointDefinition endpoint, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ModelEndpointProbeResult(_modelIds.Length > 0, _modelIds, "Fake probe."));
        }
    }

    private sealed class FakeModelsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "object": "list",
                      "data": [
                        { "id": "lfm2.5-8b-a1b", "object": "model" }
                      ]
                    }
                    """)
            };

            return Task.FromResult(response);
        }
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

    private sealed class FakeHostedHarnessTurnRunner : IHostedHarnessTurnRunner
    {
        private readonly string _answer;

        public FakeHostedHarnessTurnRunner(string answer)
        {
            _answer = answer;
        }

        public string LastHarnessId { get; private set; } = string.Empty;

        public HostedHarnessTurnRequest? LastRequest { get; private set; }

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
                "Fake route smoke complete.",
                TimeSpan.FromMilliseconds(1)));
        }
    }

    private sealed class FakeHuggingFaceSearchService : IHuggingFaceModelSearchService
    {
        private HuggingFaceModelSearchSnapshot _snapshot = new(
            string.Empty,
            HuggingFaceSearchState.Idle,
            "Fake search service.",
            null,
            Array.Empty<HuggingFaceModelSearchEntry>());

        public event Action<HuggingFaceModelSearchSnapshot>? StateChanged;

        public HuggingFaceModelSearchSnapshot GetSnapshot() => _snapshot;

        public Task<HuggingFaceModelSearchSnapshot> SearchAsync(HuggingFaceModelSearchRequest request, CancellationToken cancellationToken = default)
        {
            _snapshot = new HuggingFaceModelSearchSnapshot(
                request.Query,
                HuggingFaceSearchState.Ready,
                "Fake search service.",
                DateTimeOffset.UtcNow,
                Array.Empty<HuggingFaceModelSearchEntry>());

            StateChanged?.Invoke(_snapshot);
            return Task.FromResult(_snapshot);
        }
    }

    private sealed class FakeHuggingFaceSearchHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    [
                      {
                        "id": "nvidia/Qwen3.6-27B-NVFP4",
                        "modelId": "nvidia/Qwen3.6-27B-NVFP4",
                        "likes": 232,
                        "downloads": 94465,
                        "pipeline_tag": "text-generation",
                        "library_name": "Model Optimizer",
                        "createdAt": "2026-06-22T12:24:08.000Z",
                        "private": false,
                        "trendingScore": 225,
                        "tags": ["qwen3_5", "nvidia", "text-generation", "conversational"]
                      }
                    ]
                    """)
            };

            return Task.FromResult(response);
        }
    }

    private static class ModelEndpointTestFixture
    {
        public const string LmStudioBaseUrl = "http://10.0.0.38:1234/v1";
        public const string LmStudioModelId = "lfm2.5-8b-a1b";

        public static ModelEndpointCreateRequest CreateLmStudioLanRequest()
        {
            return new ModelEndpointCreateRequest(
                "lm-studio-lan-dev-test",
                "LM Studio LAN",
                ProviderKind.LmStudio,
                LmStudioBaseUrl,
                LmStudioModelId);
        }
    }
}
