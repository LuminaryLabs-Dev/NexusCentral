using System;
using System.Linq;
using System.Net.Http;
using NexusCentral.V1.Core.Config;
using NexusCentral.V1.Core.Execution;
using NexusCentral.V1.Core.Logging;
using NexusCentral.V1.Core.State;
using NexusCentral.V1.Desktop.ViewModels;
using NexusCentral.V1.Features.Harnesses;
using NexusCentral.V1.Features.Lab;
using NexusCentral.V1.Features.Models;
using NexusCentral.V1.Features.Router;
using NexusCentral.V1.Features.Validation;
using NexusCentral.V1.Features.Workspaces;
using NexusCentral.V1.Integrations.Codex;
using NexusCentral.V1.Integrations.Providers;
using NexusCentral.V1.Integrations.Providers.HuggingFace;
using NexusCentral.V1.Integrations.Providers.LmStudio;

namespace NexusCentral.V1.Desktop;

public sealed class AppCompositionRoot
{
    private AppCompositionRoot(MainShellViewModel shell)
    {
        Shell = shell;
    }

    public MainShellViewModel Shell { get; }

    public static AppCompositionRoot CreateDefault(SurfaceKind? startupSurface = null)
    {
        var paths = AppPathSettings.CreateDefault();
        paths.EnsureDirectories();

        var json = new JsonFileStore();
        var logs = new InMemoryAppLogService();
        var executionEvents = new InMemoryExecutionEventStream();
        var modelProbe = new LmStudioProviderAdapter(new OpenAiCompatibleModelProbe());
        var huggingFaceSearch = new HuggingFaceModelSearchService(new HttpClient
        {
            BaseAddress = new Uri("https://huggingface.co"),
            Timeout = TimeSpan.FromSeconds(8)
        });
        var modelEndpoints = new ModelEndpointService(paths, json, modelProbe, logs);
        var harnessImporter = new HarnessImporterService(paths, json, new CodexCliDiscovery(), logs);
        var hostedHarnessRunner = new HostedHarnessTurnRunner();
        var harnessOrchestrationSmoke = new HarnessOrchestrationSmokeService(harnessImporter, hostedHarnessRunner, paths, json);
        var router = new RouterService(paths, json, modelEndpoints, harnessImporter, logs, hostedHarnessRunner);
        EnsureDefaultModelEndpoint(modelEndpoints);
        EnsureDefaultConversationHarness(harnessImporter);
        var workspaceSessions = new FileWorkspaceSessionStore(paths, json);
        var workspaces = new WorkspaceService(paths, json, logs);
        var conversations = new FileLabConversationStore(paths, json);
        var validationRunner = new ValidationScenarioRunService(workspaces, harnessImporter, hostedHarnessRunner);
        var validationJobs = new ValidationScenarioJobService(validationRunner, paths, json);
        var lab = new LabViewModel(
            workspaces,
            workspaceSessions,
            conversations,
            executionEvents,
            harnessImporter,
            hostedHarnessRunner,
            validationRunner,
            validationJobs);
        var models = new ModelsViewModel(modelEndpoints, huggingFaceSearch);
        var routes = new RouterViewModel(router);
        var harnesses = new HarnessImporterViewModel(harnessImporter, hostedHarnessRunner, harnessOrchestrationSmoke);

        return new AppCompositionRoot(new MainShellViewModel(lab, models, routes, harnesses, logs, executionEvents, startupSurface));
    }

    private static void EnsureDefaultModelEndpoint(IModelEndpointService modelEndpoints)
    {
        var snapshot = modelEndpoints.GetSnapshot();
        var defaultEndpoint = snapshot.Endpoints.FirstOrDefault(endpoint => string.Equals(endpoint.Id, "lm-studio-lfm-a1b", StringComparison.OrdinalIgnoreCase));
        if (snapshot.Endpoints.Count > 0
            && defaultEndpoint is null)
        {
            return;
        }

        if (defaultEndpoint is not null
            && string.Equals(defaultEndpoint.BaseUrl, "http://10.0.0.38:1234/v1", StringComparison.OrdinalIgnoreCase)
            && string.Equals(defaultEndpoint.SelectedModelId, "lfm2.5-8b-a1b", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        System.Threading.Tasks.Task.Run(() => modelEndpoints.AddOrUpdateEndpointAsync(new ModelEndpointCreateRequest(
                "lm-studio-lfm-a1b",
                "LM Studio LFM",
                ProviderKind.LmStudio,
                "http://10.0.0.38:1234/v1",
                "lfm2.5-8b-a1b")))
            .GetAwaiter()
            .GetResult();
    }

    private static void EnsureDefaultConversationHarness(IHarnessImporterService harnessImporter)
    {
        if (harnessImporter.GetSnapshot().Harnesses.Count > 0)
        {
            return;
        }

        System.Threading.Tasks.Task.Run(() => harnessImporter.ImportHarnessAsync(new HarnessImportRequest(
                "conversation-harness",
                "Conversation Harness",
                HarnessKind.Manual,
                "cd /Users/crimsonwheeler/Documents/GitHub/NexusAgent/Conversation-Harness && CONVERSATION_HARNESS_BASE_URL=http://10.0.0.38:1234 CONVERSATION_HARNESS_MODEL=lfm2.5-8b-a1b CONVERSATION_HARNESS_MAX_TOKENS=16000 PYTHONPATH=src python3 -m ConversationHarness.cli chat-turn",
                "0.1.0",
                new[]
                {
                    new HarnessCapabilityDefinition(
                        "conversation-harness.chat",
                        "Chat turn",
                        "Run one hosted conversation harness turn.",
                        true,
                        false)
                },
                new[]
                {
                    new HarnessEndpointDefinition(
                        "POST",
                        "/harnesses/conversation-harness/chat-turn",
                        "conversation-harness.chat",
                        "safe",
                        "Send one message to the default hosted conversation harness.")
                })))
            .GetAwaiter()
            .GetResult();
    }
}
