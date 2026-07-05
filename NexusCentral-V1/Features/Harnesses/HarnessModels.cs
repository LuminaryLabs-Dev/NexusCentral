using System;
using System.Collections.Generic;

namespace NexusCentral.V1.Features.Harnesses;

public enum HarnessKind
{
    CodexCli,
    CodexAppServer,
    Manual
}

public enum HarnessHealth
{
    Inactive,
    Discovered,
    Missing,
    Unavailable
}

public sealed record HarnessCapabilityDefinition(
    string Id,
    string Name,
    string Description,
    bool IsEnabled,
    bool RequiresApproval);

public sealed record HarnessEndpointDefinition(
    string Method,
    string Route,
    string CapabilityId,
    string Safety,
    string Description);

public sealed record HarnessDefinition(
    string Id,
    string Name,
    HarnessKind Kind,
    string CommandPath,
    string Version,
    HarnessHealth Health,
    DateTimeOffset? LastDiscoveredUtc,
    string StatusMessage,
    IReadOnlyList<string> DiscoveredCommands,
    IReadOnlyList<string> ExecOptions,
    IReadOnlyList<HarnessCapabilityDefinition> Capabilities,
    IReadOnlyList<HarnessEndpointDefinition> ProposedEndpoints);

public sealed record HarnessImportRequest(
    string? Id,
    string Name,
    HarnessKind Kind,
    string CommandPath,
    string Version,
    IReadOnlyList<HarnessCapabilityDefinition> Capabilities,
    IReadOnlyList<HarnessEndpointDefinition> ProposedEndpoints);

public sealed record HarnessManifestCandidate(
    string ManifestPath,
    string Id,
    string Name,
    string Kind,
    string Version,
    string Command,
    string StatusMessage);

public sealed record HarnessImporterSnapshot(
    HarnessDefinition? ActiveHarness,
    IReadOnlyList<HarnessDefinition> Harnesses,
    string HarnessStatusText,
    string CapabilitySummaryText,
    string WrapperSummaryText,
    string OrchestrationSummaryText,
    IReadOnlyList<HarnessOrchestrationStep> OrchestrationSteps);

public sealed record HarnessOrchestrationStep(
    int Order,
    string HarnessId,
    string HarnessName,
    string Role,
    string CapabilityId,
    string EndpointRoute,
    string StatusText);

public sealed record HarnessQuestionRequest(
    string HarnessId,
    string Question,
    string WorkingDirectory,
    TimeSpan Timeout);

public sealed record HarnessQuestionResult(
    bool Ok,
    string HarnessId,
    string EndpointRoute,
    string Question,
    string Answer,
    int ExitCode,
    string Message,
    TimeSpan Duration);

public sealed record HostedHarnessTurnRequest(
    string Message,
    string WorkingDirectory,
    TimeSpan Timeout);

public sealed record HostedHarnessTurnResult(
    bool Ok,
    string HarnessId,
    string Assistant,
    string RawOutput,
    string ErrorOutput,
    int ExitCode,
    string Message,
    TimeSpan Duration);

public sealed record HarnessOrchestrationSmokeRequest(
    string Prompt,
    string WorkingDirectory,
    TimeSpan Timeout);

public sealed record HarnessOrchestrationSmokeStepResult(
    int Order,
    string HarnessId,
    string HarnessName,
    string CapabilityId,
    bool Executed,
    bool Ok,
    string Message,
    string Output);

public sealed record HarnessOrchestrationSmokeResult(
    bool Ok,
    string RunId,
    DateTimeOffset CompletedAtUtc,
    int PlannedCount,
    int ExecutedCount,
    int PassedCount,
    IReadOnlyList<HarnessOrchestrationSmokeStepResult> Steps,
    string Message);

public sealed record HarnessOrchestrationSmokeHistory(
    IReadOnlyList<HarnessOrchestrationSmokeResult> Runs);

internal sealed record HarnessRegistry(
    string ActiveHarnessId,
    List<HarnessDefinition> Harnesses);
