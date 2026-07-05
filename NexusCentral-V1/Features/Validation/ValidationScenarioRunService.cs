using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V1.Features.Harnesses;
using NexusCentral.V1.Features.Workspaces;

namespace NexusCentral.V1.Features.Validation;

public sealed class ValidationScenarioRunService : IValidationScenarioRunService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IWorkspaceService _workspaces;
    private readonly IHarnessImporterService? _harnesses;
    private readonly IHostedHarnessTurnRunner? _turnRunner;

    public ValidationScenarioRunService(IWorkspaceService workspaces)
    {
        _workspaces = workspaces;
    }

    public ValidationScenarioRunService(
        IWorkspaceService workspaces,
        IHarnessImporterService harnesses,
        IHostedHarnessTurnRunner turnRunner)
    {
        _workspaces = workspaces;
        _harnesses = harnesses;
        _turnRunner = turnRunner;
    }

    public async Task<ValidationScenarioRunResult> StageSandboxRunAsync(ValidationScenarioSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var workspace = _workspaces.ActiveWorkspace;
        if (workspace is null)
        {
            return new ValidationScenarioRunResult(
                false,
                ValidationScenarioRunStatus.Failed,
                string.Empty,
                snapshot.Scenarios.Count,
                CountFullProjectScenarios(snapshot),
                string.Empty,
                string.Empty,
                string.Empty,
                "No active sandbox workspace is selected.");
        }

        var runId = $"validation-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
        var root = $"validation/scenario-runs/{runId}";
        var manifestPath = $"{root}/scenario-manifest.json";
        var rankingPath = $"{root}/capability-ranking.md";
        var summaryPath = $"{root}/summary.md";

        var manifest = new
        {
            runId,
            stagedAtUtc = DateTimeOffset.UtcNow,
            workspace = new
            {
                workspace.Id,
                workspace.Name,
                workspace.Kind,
                workspace.RootPath
            },
            defaultEndpoint = ValidationScenarioCatalog.DefaultEndpointBaseUrl,
            defaultModel = ValidationScenarioCatalog.DefaultModelId,
            defaultMaxTokens = ValidationScenarioCatalog.DefaultMaxTokens,
            scenarioCount = snapshot.Scenarios.Count,
            fullProjectScenarioCount = CountFullProjectScenarios(snapshot),
            status = "staged",
            note = "This manifest stages validation work only. Scenario pass/fail requires execution evidence.",
            scenarios = snapshot.Scenarios
        };

        var manifestWrite = await _workspaces.WriteFileAsync(
            workspace.Id,
            manifestPath,
            JsonSerializer.Serialize(manifest, JsonOptions),
            cancellationToken);
        if (!manifestWrite.Ok)
        {
            return CreateFailure(runId, snapshot, manifestPath, rankingPath, summaryPath, manifestWrite.Error);
        }

        var rankingWrite = await _workspaces.WriteFileAsync(
            workspace.Id,
            rankingPath,
            BuildRankingMarkdown(snapshot),
            cancellationToken);
        if (!rankingWrite.Ok)
        {
            return CreateFailure(runId, snapshot, manifestPath, rankingPath, summaryPath, rankingWrite.Error);
        }

        var summaryWrite = await _workspaces.WriteFileAsync(
            workspace.Id,
            summaryPath,
            BuildSummaryMarkdown(runId, snapshot),
            cancellationToken);
        if (!summaryWrite.Ok)
        {
            return CreateFailure(runId, snapshot, manifestPath, rankingPath, summaryPath, summaryWrite.Error);
        }

        return new ValidationScenarioRunResult(
            true,
            ValidationScenarioRunStatus.Staged,
            runId,
            snapshot.Scenarios.Count,
            CountFullProjectScenarios(snapshot),
            manifestPath,
            rankingPath,
            summaryPath,
            $"Staged {snapshot.Scenarios.Count} validation scenarios in {workspace.Name}.");
    }

    public async Task<ValidationScenarioExecutionResult> ExecuteSandboxBatchAsync(ValidationScenarioSnapshot snapshot, int maxScenarios, CancellationToken cancellationToken = default)
    {
        var workspace = _workspaces.ActiveWorkspace;
        if (workspace is null)
        {
            return CreateExecutionFailure(snapshot, maxScenarios, "No active sandbox workspace is selected.");
        }

        if (_harnesses is null || _turnRunner is null)
        {
            return CreateExecutionFailure(snapshot, maxScenarios, "Validation execution requires a hosted harness runner.");
        }

        var harness = _harnesses.GetSnapshot().ActiveHarness;
        if (harness is null)
        {
            return CreateExecutionFailure(snapshot, maxScenarios, "No active harness is selected.");
        }

        var runId = $"execution-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
        var root = $"validation/scenario-executions/{runId}";
        var requested = SelectScenarioBatch(snapshot, maxScenarios);
        var results = new List<ValidationScenarioResultEntry>();
        var progressPath = $"{root}/progress.json";
        var progressLogPath = $"{root}/progress.jsonl";

        await WriteProgressAsync(workspace.Id, progressPath, progressLogPath, runId, requested.Count, results, "started", cancellationToken);

        foreach (var scenario in requested)
        {
            var prompt = BuildScenarioPrompt(scenario);
            var turn = await _turnRunner.RunTurnAsync(
                harness,
                new HostedHarnessTurnRequest(prompt, workspace.RootPath, TimeSpan.FromMinutes(4)),
                cancellationToken);
            var score = ScoreScenario(scenario, turn);
            var fullProjectBuilt = false;
            var projectPath = string.Empty;

            if (score >= 60 && IsFullProjectScenario(scenario))
            {
                projectPath = $"{root}/projects/{scenario.Id}";
                fullProjectBuilt = await WriteSandboxProjectAsync(workspace.Id, projectPath, scenario, turn, cancellationToken);
            }

            var resultPath = $"{root}/results/{scenario.Id}.json";
            var passed = score >= 60 && (!IsFullProjectScenario(scenario) || fullProjectBuilt);
            var entry = new ValidationScenarioResultEntry(
                scenario.Id,
                scenario.Capability,
                scenario.Title,
                passed,
                score,
                fullProjectBuilt,
                resultPath,
                projectPath,
                turn.Ok ? "Harness returned evidence." : turn.Message);
            results.Add(entry);

            await _workspaces.WriteFileAsync(
                workspace.Id,
                resultPath,
                JsonSerializer.Serialize(new
                {
                    scenario,
                    turn.Ok,
                    turn.Assistant,
                    turn.RawOutput,
                    turn.ErrorOutput,
                    turn.ExitCode,
                    turn.Message,
                    score,
                    passed,
                    fullProjectBuilt,
                    projectPath
                }, JsonOptions),
                cancellationToken);
            await WriteProgressAsync(workspace.Id, progressPath, progressLogPath, runId, requested.Count, results, $"completed {scenario.Id}", cancellationToken);
        }

        var resultIndexPath = $"{root}/result-index.json";
        var scoreboardPath = $"{root}/scoreboard.md";
        var capabilityRankingPath = $"{root}/capability-execution-ranking.md";
        var fullProjectIndexPath = $"{root}/full-project-index.md";
        var learningNotesPath = $"{root}/learning-notes.md";
        await _workspaces.WriteFileAsync(
            workspace.Id,
            resultIndexPath,
            JsonSerializer.Serialize(new
            {
                runId,
                executedAtUtc = DateTimeOffset.UtcNow,
                harness = new { harness.Id, harness.Name, harness.Kind },
                requestedCount = requested.Count,
                executedCount = results.Count,
                passedCount = results.Count(result => result.Passed),
                fullProjectsBuilt = results.Count(result => result.FullProjectBuilt),
                progressPath,
                progressLogPath,
                scoreboardPath,
                capabilityRankingPath,
                fullProjectIndexPath,
                learningNotesPath,
                endpoint = ValidationScenarioCatalog.DefaultEndpointBaseUrl,
                model = ValidationScenarioCatalog.DefaultModelId,
                maxTokens = ValidationScenarioCatalog.DefaultMaxTokens,
                results
            }, JsonOptions),
            cancellationToken);
        await _workspaces.WriteFileAsync(workspace.Id, scoreboardPath, BuildScoreboard(runId, results), cancellationToken);
        await _workspaces.WriteFileAsync(workspace.Id, capabilityRankingPath, BuildExecutionRanking(runId, results), cancellationToken);
        await _workspaces.WriteFileAsync(workspace.Id, fullProjectIndexPath, BuildFullProjectIndex(runId, results), cancellationToken);
        await _workspaces.WriteFileAsync(workspace.Id, learningNotesPath, BuildLearningNotes(runId, results), cancellationToken);

        return new ValidationScenarioExecutionResult(
            results.All(result => result.Passed),
            runId,
            requested.Count,
            results.Count,
            results.Count(result => result.Passed),
            results.Count(result => result.FullProjectBuilt),
            progressPath,
            progressLogPath,
            resultIndexPath,
            scoreboardPath,
            capabilityRankingPath,
            fullProjectIndexPath,
            $"Executed {results.Count} scenarios; {results.Count(result => result.Passed)} passed; {results.Count(result => result.FullProjectBuilt)} sandbox projects built.");
    }

    private static ValidationScenarioRunResult CreateFailure(
        string runId,
        ValidationScenarioSnapshot snapshot,
        string manifestPath,
        string rankingPath,
        string summaryPath,
        string message)
    {
        return new ValidationScenarioRunResult(
            false,
            ValidationScenarioRunStatus.Failed,
            runId,
            snapshot.Scenarios.Count,
            CountFullProjectScenarios(snapshot),
            manifestPath,
            rankingPath,
            summaryPath,
            message);
    }

    private static string BuildRankingMarkdown(ValidationScenarioSnapshot snapshot)
    {
        var lines = new[]
        {
            "# Capability Ranking",
            string.Empty,
            $"Endpoint: `{ValidationScenarioCatalog.DefaultEndpointBaseUrl}`",
            $"Model: `{ValidationScenarioCatalog.DefaultModelId}`",
            $"Max tokens: `{ValidationScenarioCatalog.DefaultMaxTokens}`",
            string.Empty
        }.ToList();

        lines.AddRange(snapshot.CapabilityRanking.Select(item => $"- {item}"));
        lines.Add(string.Empty);
        lines.Add("## Full Project Coverage");
        lines.Add(string.Empty);
        lines.Add($"- {CountFullProjectScenarios(snapshot)} scenarios require sandbox project files and validation output.");
        lines.Add("- Passing requires execution evidence, not just this staged manifest.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildSummaryMarkdown(string runId, ValidationScenarioSnapshot snapshot)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "# Sandbox Validation Run",
            string.Empty,
            $"- Run: `{runId}`",
            $"- Status: `staged`",
            $"- Scenarios: `{snapshot.Scenarios.Count}`",
            $"- Full-project scenarios: `{CountFullProjectScenarios(snapshot)}`",
            $"- Endpoint: `{ValidationScenarioCatalog.DefaultEndpointBaseUrl}`",
            $"- Model: `{ValidationScenarioCatalog.DefaultModelId}`",
            $"- Max tokens: `{ValidationScenarioCatalog.DefaultMaxTokens}`",
            string.Empty,
            "This run stages the complete validation matrix. Later execution must record per-scenario pass/fail evidence before any capability is treated as proven."
        });
    }

    private static int CountFullProjectScenarios(ValidationScenarioSnapshot snapshot)
    {
        return snapshot.Scenarios.Count(scenario => scenario.ExpectedEvidence.Contains("Sandbox project files", StringComparison.OrdinalIgnoreCase));
    }

    private static ValidationScenarioExecutionResult CreateExecutionFailure(ValidationScenarioSnapshot snapshot, int maxScenarios, string message)
    {
        return new ValidationScenarioExecutionResult(false, string.Empty, Math.Clamp(maxScenarios, 1, snapshot.Scenarios.Count), 0, 0, 0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, message);
    }

    private static List<ValidationScenarioDefinition> SelectScenarioBatch(ValidationScenarioSnapshot snapshot, int maxScenarios)
    {
        var requested = snapshot.Scenarios.Take(Math.Clamp(maxScenarios, 1, snapshot.Scenarios.Count)).ToList();
        if (requested.Any(IsFullProjectScenario))
        {
            return requested;
        }

        var fullProject = snapshot.Scenarios.FirstOrDefault(IsFullProjectScenario);
        if (fullProject is null)
        {
            return requested;
        }

        if (requested.Count == 0)
        {
            requested.Add(fullProject);
            return requested;
        }

        requested[^1] = fullProject;
        return requested;
    }

    private static string BuildScenarioPrompt(ValidationScenarioDefinition scenario)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "You are validating NexusCentral's hosted harness capability.",
            $"Endpoint: {ValidationScenarioCatalog.DefaultEndpointBaseUrl}",
            $"Model: {ValidationScenarioCatalog.DefaultModelId}",
            $"Max tokens: {ValidationScenarioCatalog.DefaultMaxTokens}",
            $"Scenario: {scenario.Title}",
            $"Capability: {scenario.Capability}",
            $"Goal: {scenario.SandboxGoal}",
            $"Expected evidence: {scenario.ExpectedEvidence}",
            "Return exactly four short labeled lines:",
            "Plan: <what the harness would do>",
            "Evidence: <specific artifact or output it would produce>",
            "Risk: <what could fail or remain unproven>",
            "Project: <sandbox project file path or validation surface>",
            "Do not invent metrics, pass rates, benchmark numbers, tickets, people, IDs, or completed results."
        });
    }

    private static int ScoreScenario(ValidationScenarioDefinition scenario, HostedHarnessTurnResult turn)
    {
        if (!turn.Ok || string.IsNullOrWhiteSpace(turn.Assistant))
        {
            return 0;
        }

        var answer = turn.Assistant.ToLowerInvariant();
        var score = 40;
        if (answer.Contains("plan", StringComparison.OrdinalIgnoreCase)) score += 15;
        if (answer.Contains("evidence", StringComparison.OrdinalIgnoreCase) || answer.Contains("validate", StringComparison.OrdinalIgnoreCase)) score += 15;
        if (answer.Contains("risk", StringComparison.OrdinalIgnoreCase) || answer.Contains("fail", StringComparison.OrdinalIgnoreCase)) score += 10;
        if (answer.Contains(scenario.Capability.Replace('_', ' '), StringComparison.OrdinalIgnoreCase)) score += 10;
        if (IsFullProjectScenario(scenario) && answer.Contains("project", StringComparison.OrdinalIgnoreCase)) score += 10;
        if (ContainsInventedEvidence(answer))
        {
            score = Math.Min(score, 40);
        }

        return Math.Min(100, score);
    }

    private static bool ContainsInventedEvidence(string answer)
    {
        return answer.Contains('%')
            || answer.Contains("success rate", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("benchmark data", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("ticket #", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("ticket#", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("ticket id", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("dev ", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("assigned to", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> WriteSandboxProjectAsync(
        string workspaceId,
        string projectPath,
        ValidationScenarioDefinition scenario,
        HostedHarnessTurnResult turn,
        CancellationToken cancellationToken)
    {
        var projectProof = new
        {
            scenarioId = scenario.Id,
            scenario.Capability,
            scenario.Title,
            scenario.SandboxGoal,
            harnessId = turn.HarnessId,
            endpoint = ValidationScenarioCatalog.DefaultEndpointBaseUrl,
            model = ValidationScenarioCatalog.DefaultModelId,
            maxTokens = ValidationScenarioCatalog.DefaultMaxTokens,
            generatedAtUtc = DateTimeOffset.UtcNow,
            files = new[]
            {
                "README.md",
                "index.html",
                "package.json",
                "validation.txt",
                "project-proof.json",
                "validation-report.md"
            },
            evidence = "Sandbox project shell was produced from a passing hosted harness turn."
        };
        var validationReport = string.Join(Environment.NewLine, new[]
        {
            "# Validation Report",
            string.Empty,
            $"- Scenario: `{scenario.Id}`",
            $"- Capability: `{scenario.Capability}`",
            $"- Harness: `{turn.HarnessId}`",
            $"- Model: `{ValidationScenarioCatalog.DefaultModelId}`",
            $"- Max tokens: `{ValidationScenarioCatalog.DefaultMaxTokens}`",
            $"- Result: `project shell written`",
            string.Empty,
            "Acceptance: the project folder includes runnable files, a validation command, and this proof manifest. This proves artifact creation only; product quality still depends on human review."
        });
        var files = new Dictionary<string, string>
        {
            [$"{projectPath}/README.md"] = $"# {scenario.Title}{Environment.NewLine}{Environment.NewLine}{scenario.SandboxGoal}{Environment.NewLine}{Environment.NewLine}Harness response:{Environment.NewLine}{turn.Assistant}{Environment.NewLine}",
            [$"{projectPath}/index.html"] = "<!doctype html><html><head><title>NexusCentral Sandbox Validation</title></head><body><main data-validation-project=\"true\"><h1>NexusCentral Sandbox Validation</h1><p>Scenario project shell.</p></main></body></html>",
            [$"{projectPath}/package.json"] = "{ \"scripts\": { \"validate\": \"node -e \\\"console.log('validation project ready')\\\"\" } }",
            [$"{projectPath}/validation.txt"] = $"scenario={scenario.Id}{Environment.NewLine}capability={scenario.Capability}{Environment.NewLine}model={ValidationScenarioCatalog.DefaultModelId}{Environment.NewLine}maxTokens={ValidationScenarioCatalog.DefaultMaxTokens}{Environment.NewLine}",
            [$"{projectPath}/project-proof.json"] = JsonSerializer.Serialize(projectProof, JsonOptions),
            [$"{projectPath}/validation-report.md"] = validationReport
        };

        foreach (var file in files)
        {
            var result = await _workspaces.WriteFileAsync(workspaceId, file.Key, file.Value, cancellationToken);
            if (!result.Ok)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsFullProjectScenario(ValidationScenarioDefinition scenario)
    {
        return scenario.ExpectedEvidence.Contains("Sandbox project files", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildScoreboard(string runId, IReadOnlyList<ValidationScenarioResultEntry> results)
    {
        var lines = new List<string>
        {
            "# Validation Scoreboard",
            string.Empty,
            $"- Run: `{runId}`",
            $"- Executed: `{results.Count}`",
            $"- Passed: `{results.Count(result => result.Passed)}`",
            $"- Full projects built: `{results.Count(result => result.FullProjectBuilt)}`",
            string.Empty,
            "| Scenario | Capability | Score | Passed | Full project |",
            "| --- | --- | ---: | --- | --- |"
        };

        lines.AddRange(results.Select(result => $"| {result.ScenarioId} | {result.Capability} | {result.Score} | {result.Passed} | {result.FullProjectBuilt} |"));
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildExecutionRanking(string runId, IReadOnlyList<ValidationScenarioResultEntry> results)
    {
        var groups = results
            .GroupBy(result => result.Capability)
            .Select(group => new
            {
                Capability = group.Key,
                Scenarios = group.Count(),
                Passed = group.Count(result => result.Passed),
                AverageScore = group.Average(result => result.Score),
                FullProjects = group.Count(result => result.FullProjectBuilt)
            })
            .OrderByDescending(group => group.Passed)
            .ThenByDescending(group => group.AverageScore)
            .ThenByDescending(group => group.FullProjects)
            .ThenBy(group => group.Capability, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = new List<string>
        {
            "# Capability Execution Ranking",
            string.Empty,
            $"- Run: `{runId}`",
            $"- Evidence: completed hosted harness results",
            $"- Endpoint: `{ValidationScenarioCatalog.DefaultEndpointBaseUrl}`",
            $"- Model: `{ValidationScenarioCatalog.DefaultModelId}`",
            $"- Max tokens: `{ValidationScenarioCatalog.DefaultMaxTokens}`",
            string.Empty,
            "| Rank | Capability | Scenarios | Passed | Avg score | Full projects |",
            "| ---: | --- | ---: | ---: | ---: | ---: |"
        };

        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            lines.Add($"| {index + 1} | {group.Capability} | {group.Scenarios} | {group.Passed} | {group.AverageScore:0.0} | {group.FullProjects} |");
        }

        lines.Add(string.Empty);
        lines.Add("Ranking is based on actual execution results from this run, not the staged catalog order.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildFullProjectIndex(string runId, IReadOnlyList<ValidationScenarioResultEntry> results)
    {
        var projects = results
            .Where(result => result.FullProjectBuilt && !string.IsNullOrWhiteSpace(result.ProjectPath))
            .OrderBy(result => result.Capability, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.ScenarioId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = new List<string>
        {
            "# Full Project Index",
            string.Empty,
            $"- Run: `{runId}`",
            $"- Full projects built: `{projects.Count}`",
            $"- Model: `{ValidationScenarioCatalog.DefaultModelId}`",
            $"- Max tokens: `{ValidationScenarioCatalog.DefaultMaxTokens}`",
            string.Empty,
            "| Scenario | Capability | Project path | Proof |",
            "| --- | --- | --- | --- |"
        };

        if (projects.Count == 0)
        {
            lines.Add("| none | none | none | none |");
        }
        else
        {
            lines.AddRange(projects.Select(result => $"| {result.ScenarioId} | {result.Capability} | `{result.ProjectPath}` | `{result.ProjectPath}/project-proof.json` |"));
        }

        lines.Add(string.Empty);
        lines.Add("Each listed project path is a sandbox workspace artifact produced only after the scenario passed the hosted harness evidence gate.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildLearningNotes(string runId, IReadOnlyList<ValidationScenarioResultEntry> results)
    {
        var executed = results.Count;
        var passed = results.Count(result => result.Passed);
        var fullProjects = results.Count(result => result.FullProjectBuilt);
        var capabilityGroups = results
            .GroupBy(result => result.Capability)
            .Select(group => new
            {
                Capability = group.Key,
                Count = group.Count(),
                Passed = group.Count(result => result.Passed),
                AverageScore = group.Average(result => result.Score),
                FullProjects = group.Count(result => result.FullProjectBuilt)
            })
            .OrderBy(group => group.Passed)
            .ThenBy(group => group.AverageScore)
            .ThenBy(group => group.FullProjects)
            .ThenBy(group => group.Capability, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var weakest = capabilityGroups.Take(5).ToList();
        var failures = results
            .Where(result => !result.Passed)
            .OrderBy(result => result.Score)
            .ThenBy(result => result.ScenarioId, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
        var lines = new List<string>
        {
            "# Validation Learning Notes",
            string.Empty,
            $"- Run: `{runId}`",
            $"- Executed: `{executed}`",
            $"- Passed: `{passed}`",
            $"- Full projects built: `{fullProjects}`",
            $"- Endpoint: `{ValidationScenarioCatalog.DefaultEndpointBaseUrl}`",
            $"- Model: `{ValidationScenarioCatalog.DefaultModelId}`",
            $"- Max tokens: `{ValidationScenarioCatalog.DefaultMaxTokens}`",
            string.Empty,
            "## What To Improve Next",
            string.Empty
        };

        if (failures.Count == 0)
        {
            lines.Add("- No failing scenarios in this run. Review project quality and broaden human-view checks before treating the harness as release-ready.");
        }
        else
        {
            lines.Add("- Repair the lowest-scoring failed scenarios before adding broader orchestration features.");
            lines.Add("- Compare failed result JSON files against the expected four-line harness answer contract.");
            lines.Add("- Prefer improving harness prompts/adapters over lowering score thresholds.");
        }

        lines.Add(string.Empty);
        lines.Add("## Weakest Capabilities");
        lines.Add(string.Empty);
        lines.Add("| Capability | Scenarios | Passed | Avg score | Full projects |");
        lines.Add("| --- | ---: | ---: | ---: | ---: |");
        if (weakest.Count == 0)
        {
            lines.Add("| none | 0 | 0 | 0 | 0 |");
        }
        else
        {
            lines.AddRange(weakest.Select(group => $"| {group.Capability} | {group.Count} | {group.Passed} | {group.AverageScore:0.0} | {group.FullProjects} |"));
        }

        lines.Add(string.Empty);
        lines.Add("## Watch List");
        lines.Add(string.Empty);
        if (failures.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            lines.AddRange(failures.Select(result => $"- `{result.ScenarioId}` `{result.Capability}` score `{result.Score}`: {result.Message}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task WriteProgressAsync(
        string workspaceId,
        string progressPath,
        string progressLogPath,
        string runId,
        int requestedCount,
        IReadOnlyList<ValidationScenarioResultEntry> results,
        string status,
        CancellationToken cancellationToken)
    {
        var progress = new
        {
            runId,
            status,
            updatedAtUtc = DateTimeOffset.UtcNow,
            requestedCount,
            executedCount = results.Count,
            passedCount = results.Count(result => result.Passed),
            fullProjectsBuilt = results.Count(result => result.FullProjectBuilt),
            percentComplete = requestedCount == 0 ? 0 : Math.Round(results.Count * 100.0 / requestedCount, 2),
            lastScenarioId = results.LastOrDefault()?.ScenarioId ?? string.Empty,
            endpoint = ValidationScenarioCatalog.DefaultEndpointBaseUrl,
            model = ValidationScenarioCatalog.DefaultModelId,
            maxTokens = ValidationScenarioCatalog.DefaultMaxTokens
        };

        var json = JsonSerializer.Serialize(progress, JsonOptions);
        await _workspaces.WriteFileAsync(workspaceId, progressPath, json, cancellationToken);

        var existingLog = await _workspaces.ReadFileAsync(workspaceId, progressLogPath, cancellationToken);
        var nextLog = existingLog.Ok && !string.IsNullOrWhiteSpace(existingLog.Contents)
            ? $"{existingLog.Contents.TrimEnd()}{Environment.NewLine}{JsonSerializer.Serialize(progress)}{Environment.NewLine}"
            : $"{JsonSerializer.Serialize(progress)}{Environment.NewLine}";
        await _workspaces.WriteFileAsync(workspaceId, progressLogPath, nextLog, cancellationToken);
    }
}
