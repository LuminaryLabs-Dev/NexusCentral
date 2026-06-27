using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class LogService
{
    private readonly List<LogEvent> _events = new();
    private readonly int _maxEvents;
    private readonly object _lock = new();
    private readonly string? _logDirectory;

    public event Action<LogEvent>? LogAppended;
    public event Action? LogsCleared;

    public LogService(string? logDirectory = null, int maxEvents = 5000)
    {
        _maxEvents = Math.Max(100, maxEvents);
        _logDirectory = logDirectory;
        if (!string.IsNullOrWhiteSpace(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public LogEvent Append(string level, string source, string message, string scopeId = "")
    {
        var e = new LogEvent(
            TimestampMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Level: string.IsNullOrWhiteSpace(level) ? "info" : level.Trim().ToLower(),
            Source: string.IsNullOrWhiteSpace(source) ? "app" : source.Trim().ToLower(),
            ScopeId: scopeId?.Trim() ?? "",
            Message: message ?? ""
        );

        lock (_lock)
        {
            _events.Add(e);
            if (_events.Count > _maxEvents)
            {
                _events.RemoveAt(0);
            }
        }

        if (!string.IsNullOrWhiteSpace(_logDirectory))
        {
            var path = Path.Combine(_logDirectory, $"{e.Source}.log");
            var line = $"{DateTimeOffset.FromUnixTimeMilliseconds(e.TimestampMs):O} [{e.Level}] [{e.Source}] [{e.ScopeId}] {e.Message}{Environment.NewLine}";
            File.AppendAllText(path, line);
        }

        LogAppended?.Invoke(e);
        return e;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
        LogsCleared?.Invoke();
    }

    public List<LogEvent> GetEvents()
    {
        lock (_lock)
        {
            return _events.ToList();
        }
    }
}
