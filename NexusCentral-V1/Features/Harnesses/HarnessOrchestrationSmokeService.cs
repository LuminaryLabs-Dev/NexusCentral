using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V1.Core.Config;
using NexusCentral.V1.Core.State;

namespace NexusCentral.V1.Features.Harnesses;

public sealed class HarnessOrchestrationSmokeService : IHarnessOrchestrationSmokeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IHarnessImporterService _harnesses;
    private readonly IHostedHarnessTurnRunner _turnRunner;
    private readonly AppPathSettings? _paths;
    private readonly JsonFileStore? _json;
    private HarnessOrchestrationSmokeResult? _lastResult;

    public HarnessOrchestrationSmokeService(IHarnessImporterService harnesses, IHostedHarnessTurnRunner turnRunner)
    {
        _harnesses = harnesses;
        _turnRunner = turnRunner;
    }

    public HarnessOrchestrationSmokeService(
        IHarnessImporterService harnesses,
        IHostedHarnessTurnRunner turnRunner,
        AppPathSettings paths,
        JsonFileStore json)
        : this(harnesses, turnRunner)
    {
        _paths = paths;
        _json = json;
        _paths.EnsureDirectories();
    }

    public HarnessOrchestrationSmokeResult? LastResult => _lastResult ?? LoadHistory().Runs.FirstOrDefault();

    public async Task<HarnessOrchestrationSmokeResult> SmokeAsync(HarnessOrchestrationSmokeRequest request, CancellationToken cancellationToken = default)
    {
        var snapshot = _harnesses.GetSnapshot();
        var results = new List<HarnessOrchestrationSmokeStepResult>();
        var runId = $"orchestration-smoke-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";

        foreach (var step in snapshot.OrchestrationSteps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var harness = snapshot.Harnesses.FirstOrDefault(candidate => string.Equals(candidate.Id, step.HarnessId, StringComparison.OrdinalIgnoreCase));
            if (harness is null)
            {
                results.Add(CreateSkippedResult(step, "Harness missing."));
                continue;
            }

            var capability = harness.Capabilities.FirstOrDefault(candidate => string.Equals(candidate.Id, step.CapabilityId, StringComparison.OrdinalIgnoreCase));
            if (capability is null || !capability.IsEnabled)
            {
                results.Add(CreateSkippedResult(step, "Capability is not enabled."));
                continue;
            }

            if (capability.RequiresApproval)
            {
                results.Add(CreateSkippedResult(step, "Skipped approval-gated capability."));
                continue;
            }

            if (harness.Kind == HarnessKind.Manual)
            {
                results.Add(await SmokeManualHarnessAsync(step, harness, request, cancellationToken));
                continue;
            }

            if (harness.Kind == HarnessKind.CodexCli
                && string.Equals(capability.Id, "codex.cli.exec.readonly", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(await SmokeCodexReadOnlyAsync(step, request, cancellationToken));
                continue;
            }

            results.Add(CreateSkippedResult(step, "No smoke-safe runner for this capability."));
        }

        var executed = results.Count(result => result.Executed);
        var passed = results.Count(result => result.Executed && result.Ok);
        var ok = executed > 0 && passed == executed;
        var message = executed == 0
            ? $"No smoke-safe orchestration steps found across {snapshot.OrchestrationSteps.Count} planned steps."
            : $"Orchestration smoke: {passed}/{executed} executed steps passed; {snapshot.OrchestrationSteps.Count} planned.";
        var result = new HarnessOrchestrationSmokeResult(
            ok,
            runId,
            DateTimeOffset.UtcNow,
            snapshot.OrchestrationSteps.Count,
            executed,
            passed,
            results,
            message);
        _lastResult = result;
        await SaveResultAsync(result, cancellationToken);
        return result;
    }

    private async Task<HarnessOrchestrationSmokeStepResult> SmokeManualHarnessAsync(
        HarnessOrchestrationStep step,
        HarnessDefinition harness,
        HarnessOrchestrationSmokeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _turnRunner.RunTurnAsync(
            harness,
            new HostedHarnessTurnRequest(request.Prompt, request.WorkingDirectory, request.Timeout),
            cancellationToken);
        return new HarnessOrchestrationSmokeStepResult(
            step.Order,
            harness.Id,
            harness.Name,
            step.CapabilityId,
            true,
            result.Ok,
            result.Message,
            result.Assistant);
    }

    private async Task<HarnessOrchestrationSmokeStepResult> SmokeCodexReadOnlyAsync(
        HarnessOrchestrationStep step,
        HarnessOrchestrationSmokeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _harnesses.AskReadOnlyAsync(
            new HarnessQuestionRequest(
                step.HarnessId,
                request.Prompt,
                request.WorkingDirectory,
                request.Timeout),
            cancellationToken);
        return new HarnessOrchestrationSmokeStepResult(
            step.Order,
            result.HarnessId,
            step.HarnessName,
            step.CapabilityId,
            true,
            result.Ok,
            result.Message,
            result.Answer);
    }

    private static HarnessOrchestrationSmokeStepResult CreateSkippedResult(HarnessOrchestrationStep step, string message)
    {
        return new HarnessOrchestrationSmokeStepResult(
            step.Order,
            step.HarnessId,
            step.HarnessName,
            step.CapabilityId,
            false,
            false,
            message,
            string.Empty);
    }

    private HarnessOrchestrationSmokeHistory LoadHistory()
    {
        if (_paths is null || _json is null)
        {
            return new HarnessOrchestrationSmokeHistory(Array.Empty<HarnessOrchestrationSmokeResult>());
        }

        try
        {
            if (!System.IO.File.Exists(_paths.HarnessOrchestrationSmokeHistoryPath))
            {
                return new HarnessOrchestrationSmokeHistory(Array.Empty<HarnessOrchestrationSmokeResult>());
            }

            using var stream = System.IO.File.OpenRead(_paths.HarnessOrchestrationSmokeHistoryPath);
            return JsonSerializer.Deserialize<HarnessOrchestrationSmokeHistory>(stream, JsonOptions)
                ?? new HarnessOrchestrationSmokeHistory(Array.Empty<HarnessOrchestrationSmokeResult>());
        }
        catch (JsonException)
        {
            return new HarnessOrchestrationSmokeHistory(Array.Empty<HarnessOrchestrationSmokeResult>());
        }
    }

    private async Task SaveResultAsync(HarnessOrchestrationSmokeResult result, CancellationToken cancellationToken)
    {
        if (_paths is null || _json is null)
        {
            return;
        }

        var existing = LoadHistory().Runs
            .Where(run => !string.Equals(run.RunId, result.RunId, StringComparison.OrdinalIgnoreCase))
            .Take(24)
            .ToList();
        existing.Insert(0, result);
        await _json.WriteAsync(
            _paths.HarnessOrchestrationSmokeHistoryPath,
            new HarnessOrchestrationSmokeHistory(existing.Take(25).ToList()),
            cancellationToken);
    }
}
