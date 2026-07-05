using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusCentral.V1.Features.Validation;

public static class ValidationScenarioCatalog
{
    public const string DefaultEndpointBaseUrl = "http://10.0.0.38:1234/v1";
    public const string DefaultModelId = "lfm2.5-8b-a1b";
    public const int DefaultMaxTokens = 16000;

    private static readonly string[] Capabilities =
    [
        "conversation",
        "planning",
        "requirements",
        "workspace_read",
        "sandbox_project",
        "frontend_ui",
        "backend_api",
        "data_contracts",
        "testing",
        "repair",
        "documentation",
        "orchestration"
    ];

    private static readonly string[] ScenarioKinds =
    [
        "triage a rough user request",
        "produce a compact implementation plan",
        "extract acceptance criteria",
        "inspect a workspace before acting",
        "build a full sandbox project",
        "create a clean single-screen UI",
        "define a local API boundary",
        "write shared contracts",
        "select validation commands",
        "repair a failing build",
        "write a handoff summary",
        "rank follow-up work"
    ];

    public static ValidationScenarioSnapshot CreateDefault()
    {
        var scenarios = new List<ValidationScenarioDefinition>();
        var index = 1;

        foreach (var capability in Capabilities)
        {
            foreach (var kind in ScenarioKinds)
            {
                var isFullProject = capability is "sandbox_project" or "frontend_ui" or "backend_api" or "orchestration";
                scenarios.Add(new ValidationScenarioDefinition(
                    $"scenario-{index:000}",
                    capability,
                    Array.IndexOf(Capabilities, capability) + 1,
                    $"{capability.Replace('_', ' ')}: {kind}",
                    isFullProject
                        ? $"In the sandbox lab, {kind} for a small self-hosted operations app with UI, local state, validation, and a README."
                        : $"In the sandbox lab, {kind} for NexusCentral setup and harness orchestration.",
                    isFullProject
                        ? "Sandbox project files, validation output, and a short capability score."
                        : "A concise artifact, transcript entry, or validation note tied to the capability.",
                    DefaultEndpointBaseUrl,
                    DefaultModelId,
                    DefaultMaxTokens));
                index++;
            }
        }

        var ranking = scenarios
            .GroupBy(scenario => scenario.Capability)
            .OrderBy(group => group.Min(scenario => scenario.Rank))
            .Select(group => $"{group.Min(scenario => scenario.Rank)}. {group.Key.Replace('_', ' ')} ({group.Count()} scenarios)")
            .ToList();

        return new ValidationScenarioSnapshot(
            scenarios,
            ranking,
            $"{scenarios.Count} scenarios staged for sandbox validation",
            ranking.First(),
            $"{scenarios.Count(scenario => scenario.ExpectedEvidence.Contains("Sandbox project files", StringComparison.OrdinalIgnoreCase))} full-project sandbox scenarios");
    }
}
