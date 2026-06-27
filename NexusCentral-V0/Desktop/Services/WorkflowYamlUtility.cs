using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NexusCentral_Avalonia.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NexusCentral_Avalonia.Services;

internal static class WorkflowYamlUtility
{
    private static readonly IDeserializer GenericDeserializer = new DeserializerBuilder().Build();
    private static readonly IDeserializer WorkflowDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public static object? DeserializeDocument(string text)
    {
        try
        {
            return GenericDeserializer.Deserialize<object>(text);
        }
        catch
        {
            return null;
        }
    }

    public static WorkflowDefinition? DeserializeWorkflowDefinition(string text)
    {
        try
        {
            return WorkflowDeserializer.Deserialize<WorkflowDefinition>(text);
        }
        catch
        {
            return null;
        }
    }

    public static WorkflowDefinition NormalizeWorkflow(WorkflowDefinition workflow, AppPathsService paths, string source, string sourcePath)
    {
        workflow.Id = string.IsNullOrWhiteSpace(workflow.Id)
            ? Path.GetFileNameWithoutExtension(string.IsNullOrWhiteSpace(sourcePath) ? $"workflow_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}" : sourcePath)
            : workflow.Id.Trim();
        workflow.Name = string.IsNullOrWhiteSpace(workflow.Name) ? workflow.Id : workflow.Name.Trim();
        workflow.Description = workflow.Description?.Trim() ?? string.Empty;
        workflow.WorkingDirectory = string.IsNullOrWhiteSpace(workflow.WorkingDirectory)
            ? Path.GetDirectoryName(sourcePath) ?? paths.ResolvedRootDirectory
            : ResolvePathValue(workflow.WorkingDirectory.Trim(), Path.GetDirectoryName(sourcePath) ?? paths.ResolvedRootDirectory);
        workflow.GlobalSettings ??= new WorkflowGlobalSettings();
        if (workflow.GlobalSettings.DefaultMaxTokens <= 0)
        {
            workflow.GlobalSettings.DefaultMaxTokens = 1200;
        }

        if (workflow.GlobalSettings.DefaultTimeoutSec <= 0)
        {
            workflow.GlobalSettings.DefaultTimeoutSec = 240;
        }

        workflow.GlobalSettings.DefaultWrapperId = string.IsNullOrWhiteSpace(workflow.GlobalSettings.DefaultWrapperId)
            ? "nexusagent"
            : workflow.GlobalSettings.DefaultWrapperId.Trim();

        workflow.Inputs ??= new List<WorkflowInputLabel>();
        workflow.Steps ??= new List<WorkflowStepDefinition>();
        workflow.Tags ??= new List<string>();
        workflow.Source = source;
        workflow.SourcePath = sourcePath;

        foreach (var input in workflow.Inputs)
        {
            input.Key = (input.Key ?? string.Empty).Trim();
            input.Label = string.IsNullOrWhiteSpace(input.Label) ? input.Key : input.Label.Trim();
            input.Kind = string.IsNullOrWhiteSpace(input.Kind) ? "text" : input.Kind.Trim();
            input.DefaultValue ??= string.Empty;
        }

        var stepIndex = 1;
        foreach (var step in workflow.Steps)
        {
            step.Id = string.IsNullOrWhiteSpace(step.Id) ? $"step_{stepIndex:00}" : step.Id.Trim();
            step.Name = string.IsNullOrWhiteSpace(step.Name) ? step.Id : step.Name.Trim();
            step.Type = string.Equals(step.Type, "tool_call", StringComparison.OrdinalIgnoreCase) ? "tool_call" : "agent";
            step.TemplateId ??= string.Empty;
            step.WrapperIdOverride ??= string.Empty;
            step.ModelIdOverride ??= string.Empty;
            step.RawFlags ??= string.Empty;
            step.PromptTemplate ??= string.Empty;
            step.ArtifactInputs ??= new List<string>();
            step.IntegrationId = string.IsNullOrWhiteSpace(step.IntegrationId) ? "nexuscentral" : step.IntegrationId.Trim();
            step.ToolName ??= string.Empty;
            step.Arguments ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            step.InputBindings ??= new List<WorkflowBinding>();
            step.OutputBindings ??= new List<WorkflowOutputBinding>();
            stepIndex++;
        }

        return workflow;
    }

    public static IDictionary<object, object>? AsMap(object? value)
    {
        return value as IDictionary<object, object>;
    }

    public static IList? AsList(object? value)
    {
        return value as IList;
    }

    public static object? GetValue(IDictionary<object, object>? map, string key)
    {
        if (map is null)
        {
            return null;
        }

        foreach (var pair in map)
        {
            if (string.Equals(pair.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    public static string GetString(IDictionary<object, object>? map, string key, string fallback = "")
    {
        var value = GetValue(map, key);
        return value?.ToString()?.Trim() ?? fallback;
    }

    public static List<string> GetStringList(IDictionary<object, object>? map, string key)
    {
        return ToStringList(GetValue(map, key));
    }

    public static List<string> ToStringList(object? value)
    {
        if (value is null)
        {
            return new List<string>();
        }

        if (value is string raw)
        {
            return string.IsNullOrWhiteSpace(raw) ? new List<string>() : new List<string> { raw.Trim() };
        }

        if (value is IList list)
        {
            return list.Cast<object?>()
                .Select(item => item?.ToString()?.Trim() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }

        return new List<string>();
    }

    public static string ConvertValueToString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            IList list => JsonSerializer.Serialize(list.Cast<object?>().ToList()),
            IDictionary dictionary => JsonSerializer.Serialize(dictionary.Cast<DictionaryEntry>().ToDictionary(
                entry => entry.Key?.ToString() ?? string.Empty,
                entry => entry.Value?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string ResolvePathValue(string value, string baseDirectory)
    {
        return Path.IsPathRooted(value)
            ? Path.GetFullPath(value)
            : Path.GetFullPath(Path.Combine(baseDirectory, value));
    }
}
