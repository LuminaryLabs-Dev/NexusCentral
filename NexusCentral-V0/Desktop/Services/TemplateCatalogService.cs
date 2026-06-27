using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class TemplateCatalogService
{
    private readonly AppPathsService _paths;
    private readonly LogService _logs;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private readonly object _lock = new();
    private List<TemplateDefinition> _templates = new();

    public event Action? TemplatesChanged;

    public TemplateCatalogService(AppPathsService paths, LogService logs)
    {
        _paths = paths;
        _logs = logs;
        Reload();
    }

    public IReadOnlyList<TemplateDefinition> GetTemplates()
    {
        lock (_lock)
        {
            return _templates.Select(Clone).ToList();
        }
    }

    public TemplateDefinition? FindById(string? templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return null;
        }

        lock (_lock)
        {
            var found = _templates.FirstOrDefault(template => string.Equals(template.Id, templateId, StringComparison.OrdinalIgnoreCase));
            return found is null ? null : Clone(found);
        }
    }

    public TemplateDefinition? ValidateTemplate(string? templateId)
    {
        var template = FindById(templateId);
        if (template is null)
        {
            return null;
        }

        ValidateTemplateDefinition(template);
        return template;
    }

    public string? ExportTemplateJson(string? templateId)
    {
        var template = FindById(templateId);
        if (template is null)
        {
            return null;
        }

        var exportDirectory = Path.Combine(_paths.LogsDirectory, "template-exports");
        Directory.CreateDirectory(exportDirectory);
        var exportPath = Path.Combine(exportDirectory, $"{template.Id}.json");
        File.WriteAllText(exportPath, JsonSerializer.Serialize(template, _jsonOptions));
        return exportPath;
    }

    public void Reload()
    {
        var templates = new Dictionary<string, TemplateDefinition>(StringComparer.OrdinalIgnoreCase);
        LoadDirectory(_paths.TemplatesDirectory, "built_in", templates);
        LoadDirectory(_paths.UserTemplatesDirectory, "local", templates);

        lock (_lock)
        {
            _templates = templates.Values
                .OrderBy(template => string.Equals(template.Source, "local", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        TemplatesChanged?.Invoke();
    }

    private void LoadDirectory(string directory, string source, Dictionary<string, TemplateDefinition> templates)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var template = JsonSerializer.Deserialize<TemplateDefinition>(File.ReadAllText(path), _jsonOptions) ?? new TemplateDefinition();
                NormalizeTemplate(template, source, path);
                ValidateTemplateDefinition(template);
                templates[template.Id] = template;
            }
            catch (Exception ex)
            {
                _logs.Append("error", "templates", $"Failed to load template '{path}': {ex.Message}");
            }
        }
    }

    private static void NormalizeTemplate(TemplateDefinition template, string source, string sourcePath)
    {
        template.Id = string.IsNullOrWhiteSpace(template.Id) ? Path.GetFileNameWithoutExtension(sourcePath) : template.Id.Trim();
        template.Name = string.IsNullOrWhiteSpace(template.Name) ? template.Id : template.Name.Trim();
        template.Description = template.Description?.Trim() ?? string.Empty;
        template.DefaultMode = string.IsNullOrWhiteSpace(template.DefaultMode) ? "direct_llm" : template.DefaultMode.Trim().ToLowerInvariant();
        template.DefaultWrapperId = template.DefaultWrapperId?.Trim() ?? string.Empty;
        template.Source = source;
        template.SourcePath = sourcePath;
        template.Inputs ??= new List<TemplateInputDefinition>();
        template.ArtifactOutputs ??= new ArtifactDiscoveryDefinition();
        template.ArtifactOutputs.Globs ??= new List<string>();
        template.AllowedWrapperIds ??= new List<string>();
        template.DefaultArgs ??= new List<TemplateArgDefinition>();
        template.Tags ??= new List<string>();
        if (template.DefaultMaxTokens <= 0)
        {
            template.DefaultMaxTokens = 1200;
        }
        if (template.Inputs.Count == 0)
        {
            template.Inputs.Add(new TemplateInputDefinition
            {
                Id = "prompt",
                Label = "Prompt",
                Kind = "text",
                Required = true,
                MapsTo = "prompt"
            });
        }
    }

    private static void ValidateTemplateDefinition(TemplateDefinition template)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(template.Id))
        {
            errors.Add("Missing template id.");
        }
        if (string.IsNullOrWhiteSpace(template.Name))
        {
            errors.Add("Missing template name.");
        }
        if (!string.Equals(template.DefaultMode, "direct_llm", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(template.DefaultMode, "wrapper", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("default_mode must be 'direct_llm' or 'wrapper'.");
        }
        if (string.Equals(template.DefaultMode, "wrapper", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(template.DefaultWrapperId))
        {
            errors.Add("Wrapper templates require default_wrapper_id.");
        }
        if (template.Inputs.Any(input => string.IsNullOrWhiteSpace(input.Id) || string.IsNullOrWhiteSpace(input.Kind)))
        {
            errors.Add("Each template input requires id and kind.");
        }

        template.IsValid = errors.Count == 0;
        template.ValidationMessage = errors.Count == 0 ? "OK" : string.Join(" ", errors);
    }

    private static TemplateDefinition Clone(TemplateDefinition template)
    {
        return new TemplateDefinition
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            DefaultMode = template.DefaultMode,
            DefaultWrapperId = template.DefaultWrapperId,
            AllowedWrapperIds = template.AllowedWrapperIds.ToList(),
            WrapperLocked = template.WrapperLocked,
            Inputs = template.Inputs.Select(input => new TemplateInputDefinition
            {
                Id = input.Id,
                Label = input.Label,
                Kind = input.Kind,
                Required = input.Required,
                MapsTo = input.MapsTo
            }).ToList(),
            ArtifactOutputs = new ArtifactDiscoveryDefinition
            {
                Discover = template.ArtifactOutputs.Discover,
                Globs = template.ArtifactOutputs.Globs.ToList()
            },
            DefaultModelId = template.DefaultModelId,
            DefaultMaxTokens = template.DefaultMaxTokens,
            DefaultArgs = template.DefaultArgs.Select(arg => new TemplateArgDefinition
            {
                Type = arg.Type,
                Name = arg.Name,
                Value = arg.Value
            }).ToList(),
            Tags = template.Tags.ToList(),
            Source = template.Source,
            SourcePath = template.SourcePath,
            IsValid = template.IsValid,
            ValidationMessage = template.ValidationMessage
        };
    }
}
