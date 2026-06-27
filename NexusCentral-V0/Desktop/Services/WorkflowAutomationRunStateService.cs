using System;
using System.Collections.Generic;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class WorkflowAutomationRunStateService
{
    private readonly object _lock = new();
    private readonly Dictionary<string, AutomationWorkflowHistoryEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public AutomationWorkflowHistoryEntry GetOrCreate(string filePath, bool defaultEnabled)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(filePath, out var entry))
            {
                entry = new AutomationWorkflowHistoryEntry
                {
                    Enabled = defaultEnabled
                };
                _entries[filePath] = entry;
            }

            return entry.Clone();
        }
    }

    public void SetEnabled(string filePath, bool enabled)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(filePath, out var entry))
            {
                entry = new AutomationWorkflowHistoryEntry();
                _entries[filePath] = entry;
            }

            entry.Enabled = enabled;
        }
    }

    public void UpdateValidation(string filePath, WorkflowValidationResult? result, string status, string message)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(filePath, out var entry))
            {
                entry = new AutomationWorkflowHistoryEntry();
                _entries[filePath] = entry;
            }

            entry.ValidationResult = result?.Clone();
            entry.ValidationStatus = status;
            entry.ValidationMessage = message;
        }
    }

    public void UpdateRun(string filePath, string runId, string status, DateTimeOffset? ranAtUtc)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(filePath, out var entry))
            {
                entry = new AutomationWorkflowHistoryEntry();
                _entries[filePath] = entry;
            }

            entry.LastRunId = runId;
            entry.LastRunStatus = status;
            entry.LastRunAtUtc = ranAtUtc;
        }
    }
}
