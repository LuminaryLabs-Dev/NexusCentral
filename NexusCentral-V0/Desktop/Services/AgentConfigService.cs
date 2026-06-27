using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class AgentConfigService
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
    private readonly Dictionary<string, AgentConfigOverride> _configs = new(StringComparer.OrdinalIgnoreCase);

    public event Action? ConfigurationsChanged;

    public AgentConfigService(AppPathsService paths, LogService logs)
    {
        _paths = paths;
        _logs = logs;
        Reload();
    }

    public IReadOnlyList<AgentConfigOverride> GetAll()
    {
        lock (_lock)
        {
            return _configs.Values
                .OrderBy(config => config.AgentId, StringComparer.OrdinalIgnoreCase)
                .Select(config => config.Clone())
                .ToList();
        }
    }

    public AgentConfigOverride GetOverride(string? agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return new AgentConfigOverride();
        }

        lock (_lock)
        {
            return _configs.TryGetValue(agentId, out var config)
                ? config.Clone()
                : new AgentConfigOverride { AgentId = agentId.Trim() };
        }
    }

    public EffectiveAgentConfig ResolveEffective(WrapperDefinition? wrapper)
    {
        if (wrapper is null)
        {
            return new EffectiveAgentConfig();
        }

        var overrideConfig = GetOverride(wrapper.Id);
        return new EffectiveAgentConfig
        {
            AgentId = wrapper.Id,
            TemplateId = string.Empty,
            DefaultModelId = overrideConfig.DefaultModelId,
            DefaultWorkflowId = overrideConfig.DefaultWorkflowId,
            AllowedTools = wrapper.AllowedTools.ToList()
        };
    }

    public void Save(AgentConfigOverride config)
    {
        if (string.IsNullOrWhiteSpace(config.AgentId))
        {
            throw new InvalidOperationException("Agent config requires an agent id.");
        }

        config.AgentId = config.AgentId.Trim();
        config.TemplateId = string.Empty;
        config.DefaultModelId = config.DefaultModelId?.Trim() ?? string.Empty;
        config.DefaultWorkflowId = config.DefaultWorkflowId?.Trim() ?? string.Empty;
        config.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var path = GetPath(config.AgentId);
        lock (_lock)
        {
            _configs[config.AgentId] = config.Clone();
        }

        File.WriteAllText(path, JsonSerializer.Serialize(config, _jsonOptions));
        ConfigurationsChanged?.Invoke();
    }

    public void Reset(string? agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return;
        }

        lock (_lock)
        {
            _configs.Remove(agentId);
        }

        var path = GetPath(agentId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        ConfigurationsChanged?.Invoke();
    }

    public void Reload()
    {
        lock (_lock)
        {
            _configs.Clear();
            if (!Directory.Exists(_paths.AgentConfigsDirectory))
            {
                return;
            }

            foreach (var path in Directory.EnumerateFiles(_paths.AgentConfigsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var config = JsonSerializer.Deserialize<AgentConfigOverride>(File.ReadAllText(path), _jsonOptions);
                    if (config is null || string.IsNullOrWhiteSpace(config.AgentId))
                    {
                        continue;
                    }

                    _configs[config.AgentId] = config;
                }
                catch (Exception ex)
                {
                    _logs.Append("warning", "agent-config", $"Failed to load agent config '{path}': {ex.Message}");
                }
            }
        }

        ConfigurationsChanged?.Invoke();
    }

    private string GetPath(string agentId) => Path.Combine(_paths.AgentConfigsDirectory, $"{agentId}.json");
}
