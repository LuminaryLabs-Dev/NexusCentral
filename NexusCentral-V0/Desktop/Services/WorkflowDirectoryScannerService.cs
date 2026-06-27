using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NexusCentral_Avalonia.Services;

public class WorkflowDirectoryScannerService
{
    public IReadOnlyList<string> EnumerateYamlFiles(string sourceDirectory, bool recursive)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return Array.Empty<string>();
        }

        var fullPath = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(fullPath))
        {
            return Array.Empty<string>();
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(fullPath, "*.*", searchOption)
            .Where(path => path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
