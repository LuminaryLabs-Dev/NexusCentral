using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V1.Core.Config;
using NexusCentral.V1.Core.State;

namespace NexusCentral.V1.Features.Validation;

public sealed class ValidationScenarioJobService : IValidationScenarioJobService
{
    private readonly IValidationScenarioRunService _runner;
    private readonly AppPathSettings? _paths;
    private readonly JsonFileStore? _json;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _worker = new(1, 1);
    private readonly List<ValidationScenarioJobState> _jobs = new();
    private ValidationScenarioJobState? _activeJob;

    public ValidationScenarioJobService(IValidationScenarioRunService runner)
    {
        _runner = runner;
    }

    public ValidationScenarioJobService(
        IValidationScenarioRunService runner,
        AppPathSettings paths,
        JsonFileStore json)
        : this(runner)
    {
        _paths = paths;
        _json = json;
        _paths.EnsureDirectories();
        LoadRegistry();
    }

    public event EventHandler<ValidationScenarioJobState>? StateChanged;

    public ValidationScenarioJobState? ActiveJob
    {
        get
        {
            lock (_gate)
            {
                return _activeJob;
            }
        }
    }

    public IReadOnlyList<ValidationScenarioJobState> GetJobs()
    {
        lock (_gate)
        {
            return _jobs.ToArray();
        }
    }

    public Task<ValidationScenarioJobState> EnqueueAsync(
        ValidationScenarioSnapshot snapshot,
        int maxScenarios,
        CancellationToken cancellationToken = default)
    {
        var requestedCount = Math.Clamp(maxScenarios, 1, snapshot.Scenarios.Count);
        var job = new ValidationScenarioJobState(
            $"validation-job-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}",
            ValidationScenarioJobStatus.Queued,
            requestedCount,
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            $"Queued {requestedCount} validation scenarios at {ValidationScenarioCatalog.DefaultMaxTokens} max tokens.");

        Store(job, persist: true);
        _ = Task.Run(() => RunJobAsync(snapshot, job, cancellationToken), CancellationToken.None);
        return Task.FromResult(job);
    }

    private async Task RunJobAsync(
        ValidationScenarioSnapshot snapshot,
        ValidationScenarioJobState job,
        CancellationToken cancellationToken)
    {
        ValidationScenarioJobState? running = null;
        try
        {
            await _worker.WaitAsync(cancellationToken);
            running = job with
            {
                Status = ValidationScenarioJobStatus.Running,
                StartedAtUtc = DateTimeOffset.UtcNow,
                Message = $"Running {job.RequestedCount} validation scenarios at {ValidationScenarioCatalog.DefaultMaxTokens} max tokens."
            };
            Store(running, persist: true);

            var result = await _runner.ExecuteSandboxBatchAsync(snapshot, job.RequestedCount, cancellationToken);
            Store(running with
            {
                Status = result.Ok ? ValidationScenarioJobStatus.Completed : ValidationScenarioJobStatus.Failed,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Result = result,
                Message = result.Message
            }, persist: true);
        }
        catch (OperationCanceledException)
        {
            Store((running ?? job) with
            {
                Status = ValidationScenarioJobStatus.Failed,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Message = "Validation job cancelled."
            }, persist: true);
        }
        catch (Exception ex)
        {
            Store((running ?? job) with
            {
                Status = ValidationScenarioJobStatus.Failed,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Message = $"Validation job failed: {ex.Message}"
            }, persist: true);
        }
        finally
        {
            if (running is not null)
            {
                _worker.Release();
            }
        }
    }

    private void LoadRegistry()
    {
        if (_paths is null || !File.Exists(_paths.ValidationJobRegistryPath))
        {
            return;
        }

        var registry = _json!.ReadAsync<ValidationScenarioJobRegistry>(_paths.ValidationJobRegistryPath)
            .GetAwaiter()
            .GetResult();
        if (registry is null)
        {
            return;
        }

        lock (_gate)
        {
            _jobs.Clear();
            foreach (var job in registry.Jobs)
            {
                _jobs.Add(NormalizeRestoredJob(job));
            }

            _activeJob = _jobs.Count == 0 ? null : _jobs[^1];
        }

        Task.Run(() => PersistAsync(CancellationToken.None)).GetAwaiter().GetResult();
    }

    private static ValidationScenarioJobState NormalizeRestoredJob(ValidationScenarioJobState job)
    {
        if (job.Status is not (ValidationScenarioJobStatus.Queued or ValidationScenarioJobStatus.Running))
        {
            return job;
        }

        return job with
        {
            Status = ValidationScenarioJobStatus.Failed,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Message = "Validation job did not complete before the app stopped. Re-run it to refresh evidence."
        };
    }

    private void Store(ValidationScenarioJobState state, bool persist)
    {
        lock (_gate)
        {
            var index = _jobs.FindIndex(job => job.JobId == state.JobId);
            if (index >= 0)
            {
                _jobs[index] = state;
            }
            else
            {
                _jobs.Add(state);
            }

            _activeJob = state;
        }

        StateChanged?.Invoke(this, state);
        if (persist)
        {
            Task.Run(() => PersistAsync(CancellationToken.None)).GetAwaiter().GetResult();
        }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        if (_paths is null || _json is null)
        {
            return;
        }

        ValidationScenarioJobRegistry registry;
        lock (_gate)
        {
            registry = new ValidationScenarioJobRegistry(_jobs.ToArray());
        }

        await _json.WriteAsync(_paths.ValidationJobRegistryPath, registry, cancellationToken);
    }
}
