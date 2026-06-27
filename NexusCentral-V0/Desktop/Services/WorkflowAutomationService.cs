using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class WorkflowAutomationService : IDisposable
{
    private readonly WorkflowDirectoryScannerService _scanner;
    private readonly WorkflowClassifierService _classifier;
    private readonly WorkflowImportService _imports;
    private readonly WorkflowAutomationRunStateService _history;
    private readonly WorkflowRunService _workflowRuns;
    private readonly SemaphoreSlim _passGate = new(1, 1);
    private readonly object _lock = new();

    private AutomationSessionState _session = new();
    private List<AutomationWorkflowFileState> _rows = new();
    private System.Timers.Timer? _timer;
    private bool _automationStarted;

    public WorkflowAutomationService(
        WorkflowDirectoryScannerService scanner,
        WorkflowClassifierService classifier,
        WorkflowImportService imports,
        WorkflowAutomationRunStateService history,
        WorkflowRunService workflowRuns)
    {
        _scanner = scanner;
        _classifier = classifier;
        _imports = imports;
        _history = history;
        _workflowRuns = workflowRuns;
    }

    public event Action? Changed;

    public AutomationSessionState GetSession()
    {
        lock (_lock)
        {
            return _session.Clone();
        }
    }

    public IReadOnlyList<AutomationWorkflowFileState> GetRows()
    {
        lock (_lock)
        {
            return _rows.Select(row => row.Clone()).ToList();
        }
    }

    public void SetSourceDirectory(string path)
    {
        lock (_lock)
        {
            _session.SourceDirectory = path?.Trim() ?? string.Empty;
        }

        NotifyChanged();
    }

    public void SetRecursive(bool recursive)
    {
        lock (_lock)
        {
            _session.Recursive = recursive;
        }

        NotifyChanged();
    }

    public void SetAutoRunEnabled(bool autoRunEnabled)
    {
        lock (_lock)
        {
            _session.AutoRunEnabled = autoRunEnabled;
        }

        NotifyChanged();
    }

    public void SetIntervalSeconds(int intervalSeconds)
    {
        lock (_lock)
        {
            _session.IntervalSeconds = Math.Max(5, intervalSeconds);
            if (_timer is not null)
            {
                _timer.Interval = _session.IntervalSeconds * 1000;
            }
        }

        NotifyChanged();
    }

    public async Task SearchDirectoryAsync()
    {
        await RunExclusivePassAsync(runEligibleRows: false, "scanning");
    }

    public async Task StartAutomationAsync()
    {
        lock (_lock)
        {
            _automationStarted = true;
            _session.State = "idle";
            _session.StatusMessage = "Automation started.";
            EnsureTimer();
            _timer!.Interval = _session.IntervalSeconds * 1000;
            _timer.Start();
        }

        NotifyChanged();
        await RunExclusivePassAsync(runEligibleRows: true, "running");
    }

    public void StopAutomation()
    {
        lock (_lock)
        {
            _automationStarted = false;
            _timer?.Stop();
            _session.State = "stopped";
            _session.StatusMessage = "Automation stopped.";
        }

        NotifyChanged();
    }

    public void SetFileEnabled(string filePath, bool enabled)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        _history.SetEnabled(normalizedPath, enabled);

        lock (_lock)
        {
            var row = _rows.FirstOrDefault(candidate => string.Equals(candidate.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase));
            if (row is not null)
            {
                row.Enabled = enabled;
            }
        }

        NotifyChanged();
    }

    public async Task ValidateFileAsync(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        var sourceDirectory = GetSession().SourceDirectory;
        var classified = _classifier.ClassifyFile(sourceDirectory, normalizedPath);
        MergeRowState(classified);
        NotifyChanged();
        await Task.CompletedTask;
    }

    public async Task RunFileOnceAsync(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        AutomationWorkflowFileState? row;
        lock (_lock)
        {
            row = _rows.FirstOrDefault(candidate => string.Equals(candidate.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase))?.Clone();
        }

        if (row is null || !string.Equals(row.Classification, "executable", StringComparison.OrdinalIgnoreCase) || row.ExecutableWorkflow is null)
        {
            return;
        }

        await RunWorkflowFileAsync(row);
        await SearchDirectoryAsync();
    }

    public WorkflowDefinition? CreateWorkflowForEditor(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        AutomationWorkflowFileState? row;
        lock (_lock)
        {
            row = _rows.FirstOrDefault(candidate => string.Equals(candidate.FilePath, normalizedPath, StringComparison.OrdinalIgnoreCase))?.Clone();
        }

        if (row is null)
        {
            return null;
        }

        if (string.Equals(row.Classification, "executable", StringComparison.OrdinalIgnoreCase))
        {
            return row.ExecutableWorkflow?.Clone();
        }

        if (string.Equals(row.Classification, "import_required", StringComparison.OrdinalIgnoreCase))
        {
            return _imports.ImportBuilderWorkflowSpec(normalizedPath);
        }

        return null;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _passGate.Dispose();
    }

    private async Task RunExclusivePassAsync(bool runEligibleRows, string activeState)
    {
        await _passGate.WaitAsync();
        try
        {
            await ExecutePassAsync(runEligibleRows, activeState);
        }
        finally
        {
            _passGate.Release();
        }
    }

    private async Task ExecutePassAsync(bool runEligibleRows, string activeState)
    {
        var session = GetSession();
        if (string.IsNullOrWhiteSpace(session.SourceDirectory))
        {
            UpdateSession("error", "Set a source directory first.");
            return;
        }

        if (!Directory.Exists(session.SourceDirectory))
        {
            UpdateSession("error", $"Directory '{session.SourceDirectory}' does not exist.");
            return;
        }

        UpdateSession("scanning", "Scanning workflow directory...");
        var files = _scanner.EnumerateYamlFiles(session.SourceDirectory, session.Recursive);
        var discovered = files.Select(path => _classifier.ClassifyFile(session.SourceDirectory, path)).ToList();
        ApplyDiscoveryResults(discovered, "Directory scan complete.");

        if (!_automationStarted || !runEligibleRows || !session.AutoRunEnabled)
        {
            UpdateSession(_automationStarted ? "idle" : "idle", session.AutoRunEnabled ? "Scan complete." : "Scan complete. Auto Run is disabled.");
            return;
        }

        UpdateSession("running", "Running enabled executable workflows...");
        foreach (var row in discovered.Where(candidate =>
                     string.Equals(candidate.Classification, "executable", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(candidate.Readiness, "Ready", StringComparison.OrdinalIgnoreCase) &&
                     candidate.Enabled))
        {
            await RunWorkflowFileAsync(row);
        }

        UpdateSession("idle", "Automation pass complete.");
        ApplyDiscoveryResults(_rows, "Automation pass complete.");
    }

    private async Task RunWorkflowFileAsync(AutomationWorkflowFileState row)
    {
        if (row.ExecutableWorkflow is null)
        {
            return;
        }

        _history.UpdateValidation(row.FilePath, row.ValidationResult, row.ValidationStatus, row.ValidationMessage);
        var handle = _workflowRuns.StartRun(row.ExecutableWorkflow.Clone());
        var run = await handle.Completion;
        _history.UpdateRun(row.FilePath, run.RunId, run.Status, run.CompletedAtUtc ?? run.StartedAtUtc ?? DateTimeOffset.UtcNow);
    }

    private void ApplyDiscoveryResults(IEnumerable<AutomationWorkflowFileState> discoveredRows, string statusMessage)
    {
        var merged = discoveredRows.Select(ApplyHistoryToRow).ToList();

        lock (_lock)
        {
            _rows = merged;
            _session.LastScanAtUtc = DateTimeOffset.UtcNow;
            _session.TotalFiles = merged.Count;
            _session.ExecutableCount = merged.Count(row => string.Equals(row.Classification, "executable", StringComparison.OrdinalIgnoreCase));
            _session.ImportRequiredCount = merged.Count(row => string.Equals(row.Classification, "import_required", StringComparison.OrdinalIgnoreCase));
            _session.InvalidCount = merged.Count(row =>
                string.Equals(row.Readiness, "Invalid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.Classification, "invalid", StringComparison.OrdinalIgnoreCase));
            _session.StatusMessage = statusMessage;
        }

        NotifyChanged();
    }

    private AutomationWorkflowFileState ApplyHistoryToRow(AutomationWorkflowFileState row)
    {
        var defaultEnabled = string.Equals(row.Classification, "executable", StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(row.Readiness, "Ready", StringComparison.OrdinalIgnoreCase);
        var history = _history.GetOrCreate(row.FilePath, defaultEnabled);
        row.Enabled = history.Enabled;

        if (row.ValidationResult is not null)
        {
            _history.UpdateValidation(row.FilePath, row.ValidationResult, row.ValidationStatus, row.ValidationMessage);
        }
        else if (!string.IsNullOrWhiteSpace(history.ValidationStatus))
        {
            row.ValidationStatus = history.ValidationStatus;
            row.ValidationMessage = history.ValidationMessage;
            row.ValidationResult = history.ValidationResult?.Clone();
        }

        row.LastRunId = history.LastRunId;
        row.LastRunStatus = history.LastRunStatus;
        row.LastRunAtUtc = history.LastRunAtUtc;
        return row;
    }

    private void MergeRowState(AutomationWorkflowFileState updated)
    {
        var row = ApplyHistoryToRow(updated);
        lock (_lock)
        {
            var index = _rows.FindIndex(candidate => string.Equals(candidate.FilePath, row.FilePath, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _rows[index] = row;
            }
            else
            {
                _rows.Add(row);
                _rows = _rows.OrderBy(candidate => candidate.FilePath, StringComparer.OrdinalIgnoreCase).ToList();
            }

            _session.LastScanAtUtc = DateTimeOffset.UtcNow;
            _session.TotalFiles = _rows.Count;
            _session.ExecutableCount = _rows.Count(candidate => string.Equals(candidate.Classification, "executable", StringComparison.OrdinalIgnoreCase));
            _session.ImportRequiredCount = _rows.Count(candidate => string.Equals(candidate.Classification, "import_required", StringComparison.OrdinalIgnoreCase));
            _session.InvalidCount = _rows.Count(candidate =>
                string.Equals(candidate.Readiness, "Invalid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Classification, "invalid", StringComparison.OrdinalIgnoreCase));
        }
    }

    private void EnsureTimer()
    {
        if (_timer is not null)
        {
            return;
        }

        _timer = new System.Timers.Timer(Math.Max(5, _session.IntervalSeconds) * 1000)
        {
            AutoReset = true
        };
        _timer.Elapsed += async (_, _) =>
        {
            if (!_automationStarted)
            {
                return;
            }

            if (!await _passGate.WaitAsync(0))
            {
                UpdateSession(GetSession().State, "Skipped interval tick because an automation pass is already active.");
                return;
            }

            try
            {
                await ExecutePassAsync(runEligibleRows: true, activeState: "running");
            }
            finally
            {
                _passGate.Release();
            }
        };
    }

    private void UpdateSession(string state, string statusMessage)
    {
        lock (_lock)
        {
            _session.State = state;
            _session.StatusMessage = statusMessage;
        }

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
