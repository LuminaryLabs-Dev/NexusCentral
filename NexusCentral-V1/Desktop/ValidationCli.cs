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
using NexusCentral.V1.Features.Models;
using NexusCentral.V1.Features.Validation;
using NexusCentral.V1.Features.Workspaces;
using NexusCentral.V1.Integrations.Codex;
using NexusCentral.V1.Integrations.Providers;
using NexusCentral.V1.Integrations.Providers.LmStudio;

namespace NexusCentral.V1.Desktop;

internal static class ValidationCli
{
    private const string CommandName = "validate-scenarios";
    private const string DefaultHarnessManifestPath = "/Users/crimsonwheeler/Documents/GitHub/NexusAgent/Conversation-Harness/harness.manifest.json";

    public static bool IsCommand(string[] args)
    {
        return args.Length > 0 && string.Equals(args[0], CommandName, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var options = ParseOptions(args);
        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        var paths = AppPathSettings.CreateDefault(options.StateRoot);
        paths.EnsureDirectories();
        var json = new JsonFileStore();
        var logs = new InMemoryAppLogService();
        var modelEndpoints = new ModelEndpointService(
            paths,
            json,
            new LmStudioProviderAdapter(new OpenAiCompatibleModelProbe()),
            logs);
        var harnesses = new HarnessImporterService(paths, json, new CodexCliDiscovery(), logs);
        var runner = new HostedHarnessTurnRunner();
        var workspaces = new WorkspaceService(paths, json, logs);
        var validation = new ValidationScenarioRunService(workspaces, harnesses, runner);

        var endpoint = await EnsureModelEndpointAsync(modelEndpoints);
        var probed = await modelEndpoints.ProbeEndpointAsync(endpoint.Id);
        if (probed?.Health != ModelEndpointHealth.Healthy)
        {
            Console.Error.WriteLine($"Model endpoint unhealthy: {probed?.StatusMessage ?? "probe failed"}");
            return 2;
        }

        await EnsureConversationHarnessAsync(harnesses, options.HarnessManifestPath);
        var snapshot = CreateRequestedSnapshot(ValidationScenarioCatalog.CreateDefault(), options.ScenarioIds);
        var count = Math.Clamp(options.Count, 1, snapshot.Scenarios.Count);
        Console.WriteLine($"Running {count} NexusCentral validation scenarios.");
        Console.WriteLine($"Endpoint: {ValidationScenarioCatalog.DefaultEndpointBaseUrl}");
        Console.WriteLine($"Model: {ValidationScenarioCatalog.DefaultModelId}");
        Console.WriteLine($"Max tokens: {ValidationScenarioCatalog.DefaultMaxTokens}");
        Console.WriteLine("No model downloads are performed.");

        var result = await validation.ExecuteSandboxBatchAsync(snapshot, count);
        Console.WriteLine(result.Message);
        Console.WriteLine($"Run: {result.RunId}");
        Console.WriteLine($"Executed: {result.ExecutedCount}/{result.RequestedCount}");
        Console.WriteLine($"Passed: {result.PassedCount}");
        Console.WriteLine($"Full projects built: {result.FullProjectsBuilt}");
        Console.WriteLine($"Result index: {result.ResultIndexPath}");
        Console.WriteLine($"Scoreboard: {result.ScoreboardPath}");
        Console.WriteLine($"Capability ranking: {result.CapabilityRankingPath}");
        Console.WriteLine($"Full project index: {result.FullProjectIndexPath}");
        return result.Ok ? 0 : 3;
    }

    private static async Task<ModelEndpointDefinition> EnsureModelEndpointAsync(IModelEndpointService modelEndpoints)
    {
        var existing = modelEndpoints.GetSnapshot().Endpoints.FirstOrDefault(endpoint =>
            string.Equals(endpoint.BaseUrl, ValidationScenarioCatalog.DefaultEndpointBaseUrl, StringComparison.OrdinalIgnoreCase)
            && string.Equals(endpoint.SelectedModelId, ValidationScenarioCatalog.DefaultModelId, StringComparison.OrdinalIgnoreCase));

        var endpoint = existing ?? await modelEndpoints.AddOrUpdateEndpointAsync(new ModelEndpointCreateRequest(
            "lm-studio-lfm-a1b",
            "LM Studio LFM",
            ProviderKind.LmStudio,
            ValidationScenarioCatalog.DefaultEndpointBaseUrl,
            ValidationScenarioCatalog.DefaultModelId));
        await modelEndpoints.SelectEndpointAsync(endpoint.Id);
        return endpoint;
    }

    private static async Task EnsureConversationHarnessAsync(IHarnessImporterService harnesses, string manifestPath)
    {
        var existing = harnesses.GetSnapshot().Harnesses.FirstOrDefault(harness => string.Equals(harness.Id, "conversation-harness", StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            await harnesses.SelectHarnessAsync(existing.Id);
            return;
        }

        if (File.Exists(manifestPath))
        {
            var imported = await harnesses.ImportManifestAsync(manifestPath);
            await harnesses.SelectHarnessAsync(imported.Id);
            return;
        }

        var harness = await harnesses.ImportHarnessAsync(new HarnessImportRequest(
            "conversation-harness",
            "Conversation Harness",
            HarnessKind.Manual,
            "cd /Users/crimsonwheeler/Documents/GitHub/NexusAgent/Conversation-Harness && CONVERSATION_HARNESS_BASE_URL=http://10.0.0.38:1234 CONVERSATION_HARNESS_MODEL=lfm2.5-8b-a1b CONVERSATION_HARNESS_MAX_TOKENS=16000 PYTHONPATH=src python3 -m ConversationHarness.cli chat-turn",
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
        await harnesses.SelectHarnessAsync(harness.Id);
    }

    private static ValidationCliOptions ParseOptions(string[] args)
    {
        var count = ValidationScenarioCatalog.CreateDefault().Scenarios.Count;
        var stateRoot = string.Empty;
        var manifestPath = DefaultHarnessManifestPath;
        var scenarioIds = Array.Empty<string>();
        var showHelp = false;

        for (var index = 1; index < args.Length; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
            {
                showHelp = true;
            }
            else if (string.Equals(arg, "--count", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                if (int.TryParse(args[++index], out var parsed))
                {
                    count = parsed;
                }
            }
            else if (string.Equals(arg, "--state-root", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                stateRoot = args[++index];
            }
            else if (string.Equals(arg, "--harness-manifest", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                manifestPath = args[++index];
            }
            else if (string.Equals(arg, "--ids", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                scenarioIds = args[++index]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }

        return new ValidationCliOptions(count, stateRoot, manifestPath, scenarioIds, showHelp);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("NexusCentral V1 validation CLI");
        Console.WriteLine("Usage: validate-scenarios [--count N] [--ids scenario-001,scenario-002] [--state-root PATH] [--harness-manifest PATH]");
        Console.WriteLine("Runs hosted harness validation through LM Studio without downloading models.");
    }

    private static ValidationScenarioSnapshot CreateRequestedSnapshot(
        ValidationScenarioSnapshot snapshot,
        IReadOnlyList<string> scenarioIds)
    {
        if (scenarioIds.Count == 0)
        {
            return snapshot;
        }

        var selected = snapshot.Scenarios
            .Where(scenario => scenarioIds.Any(id => string.Equals(id, scenario.Id, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (selected.Count == 0)
        {
            throw new InvalidOperationException("No matching scenario ids were found.");
        }

        var ranking = selected
            .GroupBy(scenario => scenario.Capability)
            .OrderBy(group => group.Min(scenario => scenario.Rank))
            .Select(group => $"{group.Min(scenario => scenario.Rank)}. {group.Key.Replace('_', ' ')} ({group.Count()} scenarios)")
            .ToList();

        return new ValidationScenarioSnapshot(
            selected,
            ranking,
            $"{selected.Count} selected scenarios staged for sandbox validation",
            ranking.First(),
            $"{selected.Count(scenario => scenario.ExpectedEvidence.Contains("Sandbox project files", StringComparison.OrdinalIgnoreCase))} full-project sandbox scenarios");
    }

    private sealed record ValidationCliOptions(
        int Count,
        string StateRoot,
        string HarnessManifestPath,
        IReadOnlyList<string> ScenarioIds,
        bool ShowHelp);
}
