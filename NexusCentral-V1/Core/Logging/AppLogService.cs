using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusCentral.V1.Core.Logging;

public interface IAppLogService
{
    event Action<AppLogEntry>? EntryAdded;

    AppLogEntry Append(string level, string source, string message, string scopeId = "");

    IReadOnlyList<AppLogEntry> GetEntries();
}

public sealed record AppLogEntry(
    DateTimeOffset TimestampUtc,
    string Level,
    string Source,
    string Message,
    string ScopeId);

public sealed class InMemoryAppLogService : IAppLogService
{
    private readonly object _gate = new();
    private readonly List<AppLogEntry> _entries = new();

    public event Action<AppLogEntry>? EntryAdded;

    public AppLogEntry Append(string level, string source, string message, string scopeId = "")
    {
        var entry = new AppLogEntry(
            DateTimeOffset.UtcNow,
            string.IsNullOrWhiteSpace(level) ? "info" : level.Trim(),
            string.IsNullOrWhiteSpace(source) ? "app" : source.Trim(),
            message,
            scopeId ?? string.Empty);

        lock (_gate)
        {
            _entries.Add(entry);
        }

        EntryAdded?.Invoke(entry);
        return entry;
    }

    public IReadOnlyList<AppLogEntry> GetEntries()
    {
        lock (_gate)
        {
            return _entries.ToList();
        }
    }
}
