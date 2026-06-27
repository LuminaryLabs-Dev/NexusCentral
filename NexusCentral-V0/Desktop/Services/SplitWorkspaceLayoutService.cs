using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NexusCentral_Avalonia.Services;

public sealed class SplitWorkspaceLayoutService
{
    private readonly AppPathsService _paths;
    private readonly LogService _logs;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private SplitWorkspaceLayoutDocument _document = new();

    public SplitWorkspaceLayoutService(AppPathsService paths, LogService logs)
    {
        _paths = paths;
        _logs = logs;
        Load();
    }

    public double GetRatio(string tabKey, double fallback = 0.5)
    {
        lock (_lock)
        {
            return GetOrCreateEntryUnsafe(tabKey, fallback).Ratio;
        }
    }

    public void SetRatio(string tabKey, double ratio)
    {
        lock (_lock)
        {
            var entry = GetOrCreateEntryUnsafe(tabKey, ratio);
            entry.Ratio = Clamp(ratio);
            PersistUnsafe();
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_paths.SplitWorkspaceLayoutPath))
            {
                var parsed = JsonSerializer.Deserialize<SplitWorkspaceLayoutDocument>(File.ReadAllText(_paths.SplitWorkspaceLayoutPath));
                if (parsed is not null)
                {
                    _document = parsed;
                }
            }
        }
        catch (Exception ex)
        {
            _logs.Append("warning", "split-layout", $"Could not load split workspace layout. {ex.Message}");
        }

        PersistUnsafe();
    }

    private void PersistUnsafe()
    {
        File.WriteAllText(_paths.SplitWorkspaceLayoutPath, JsonSerializer.Serialize(_document, _jsonOptions));
    }

    private SplitWorkspaceLayoutEntry GetOrCreateEntryUnsafe(string tabKey, double fallback)
    {
        var key = NormalizeKey(tabKey);
        if (!_document.Tabs.TryGetValue(key, out var entry))
        {
            entry = new SplitWorkspaceLayoutEntry { Ratio = Clamp(fallback) };
            _document.Tabs[key] = entry;
        }

        return entry;
    }

    private static string NormalizeKey(string tabKey)
    {
        return string.IsNullOrWhiteSpace(tabKey) ? "default" : tabKey.Trim().ToLowerInvariant();
    }

    private static double Clamp(double ratio)
    {
        if (double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            return 0.5;
        }

        return Math.Clamp(ratio, 0.15, 0.85);
    }
}

public sealed class SplitWorkspaceLayoutDocument
{
    public Dictionary<string, SplitWorkspaceLayoutEntry> Tabs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SplitWorkspaceLayoutEntry
{
    public double Ratio { get; set; } = 0.5;
}
