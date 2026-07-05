using System;
using System.Collections.Generic;

namespace NexusCentral.V1.Features.Validation;

public sealed record ValidationScenarioDefinition(
    string Id,
    string Capability,
    int Rank,
    string Title,
    string SandboxGoal,
    string ExpectedEvidence,
    string EndpointBaseUrl,
    string ModelId,
    int MaxTokens);

public sealed record ValidationScenarioSnapshot(
    IReadOnlyList<ValidationScenarioDefinition> Scenarios,
    IReadOnlyList<string> CapabilityRanking,
    string SummaryText,
    string TopCapabilityText,
    string FullProjectText);

public enum ValidationScenarioRunStatus
{
    NotStarted,
    Staged,
    Failed
}

public enum ValidationScenarioJobStatus
{
    Queued,
    Running,
    Completed,
    Failed
}

public sealed record ValidationScenarioRunResult(
    bool Ok,
    ValidationScenarioRunStatus Status,
    string RunId,
    int ScenarioCount,
    int FullProjectScenarioCount,
    string ManifestPath,
    string RankingPath,
    string SummaryPath,
    string Message);

public sealed record ValidationScenarioExecutionResult(
    bool Ok,
    string RunId,
    int RequestedCount,
    int ExecutedCount,
    int PassedCount,
    int FullProjectsBuilt,
    string ProgressPath,
    string ProgressLogPath,
    string ResultIndexPath,
    string ScoreboardPath,
    string CapabilityRankingPath,
    string FullProjectIndexPath,
    string Message);

public sealed record ValidationScenarioJobState(
    string JobId,
    ValidationScenarioJobStatus Status,
    int RequestedCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    ValidationScenarioExecutionResult? Result,
    string Message);

public sealed record ValidationScenarioJobRegistry(
    IReadOnlyList<ValidationScenarioJobState> Jobs);

public sealed record ValidationScenarioResultEntry(
    string ScenarioId,
    string Capability,
    string Title,
    bool Passed,
    int Score,
    bool FullProjectBuilt,
    string ResultPath,
    string ProjectPath,
    string Message);
