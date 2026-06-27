using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NexusCentral_Avalonia.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NexusCentral_Avalonia.Services;

public class WorkflowCatalogService
{
    private readonly AppPathsService _paths;
    private readonly LogService _logs;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;
    private readonly object _lock = new();
    private List<WorkflowDefinition> _workflows = new();

    public event Action? WorkflowsChanged;

    public WorkflowCatalogService(AppPathsService paths, LogService logs)
    {
        _paths = paths;
        _logs = logs;
        _deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        _serializer = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        Reload();
    }

    public IReadOnlyList<WorkflowDefinition> GetWorkflows()
    {
        lock (_lock)
        {
            return _workflows.Select(workflow => workflow.Clone()).ToList();
        }
    }

    public WorkflowDefinition? FindById(string? workflowId)
    {
        if (string.IsNullOrWhiteSpace(workflowId))
        {
            return null;
        }

        lock (_lock)
        {
            var workflow = _workflows.FirstOrDefault(candidate => string.Equals(candidate.Id, workflowId, StringComparison.OrdinalIgnoreCase));
            return workflow?.Clone();
        }
    }

    public WorkflowBundle? LoadBundle(string? bundleIdOrPath)
    {
        if (string.IsNullOrWhiteSpace(bundleIdOrPath))
        {
            return null;
        }

        var candidatePath = ResolveBundlePath(bundleIdOrPath);
        if (!File.Exists(candidatePath))
        {
            return null;
        }

        try
        {
            var bundle = _deserializer.Deserialize<WorkflowBundle>(File.ReadAllText(candidatePath)) ?? new WorkflowBundle();
            bundle.Workflow = NormalizeWorkflow(bundle.Workflow, "bundle", candidatePath);
            bundle.EditorState ??= new WorkflowEditorState();
            bundle.LastValues ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bundle.SavedAtUtc = string.IsNullOrWhiteSpace(bundle.SavedAtUtc) ? DateTimeOffset.UtcNow.ToString("O") : bundle.SavedAtUtc;
            return bundle.Clone();
        }
        catch (Exception ex)
        {
            _logs.Append("error", "workflows", $"Failed to load workflow bundle '{candidatePath}': {ex.Message}");
            return null;
        }
    }

    public string SaveWorkflow(WorkflowDefinition workflow)
    {
        var normalized = NormalizeWorkflow(workflow.Clone(), "local", string.Empty);
        var path = Path.Combine(_paths.UserWorkflowsDirectory, $"{normalized.Id}.yaml");
        Directory.CreateDirectory(_paths.UserWorkflowsDirectory);
        File.WriteAllText(path, _serializer.Serialize(normalized));
        Reload();
        return path;
    }

    public string SaveBundle(WorkflowBundle bundle, string? bundleId = null)
    {
        var normalized = bundle.Clone();
        normalized.Workflow = NormalizeWorkflow(normalized.Workflow, "bundle", string.Empty);
        normalized.EditorState ??= new WorkflowEditorState();
        normalized.LastValues ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        normalized.SavedAtUtc = DateTimeOffset.UtcNow.ToString("O");

        var safeBundleId = string.IsNullOrWhiteSpace(bundleId)
            ? $"{normalized.Workflow.Id}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"
            : bundleId.Trim();
        var path = Path.Combine(_paths.WorkflowBundlesDirectory, $"{safeBundleId}.yaml");
        Directory.CreateDirectory(_paths.WorkflowBundlesDirectory);
        File.WriteAllText(path, _serializer.Serialize(normalized));
        return path;
    }

    public void Reload()
    {
        var workflows = new Dictionary<string, WorkflowDefinition>(StringComparer.OrdinalIgnoreCase);
        LoadDirectory(_paths.WorkflowDefinitionsDirectory, "built_in", workflows);
        LoadDirectory(_paths.UserWorkflowsDirectory, "local", workflows);

        lock (_lock)
        {
            _workflows = workflows.Values
                .OrderBy(workflow => string.Equals(workflow.Source, "local", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(workflow => workflow.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        WorkflowsChanged?.Invoke();
    }

    public WorkflowDefinition CreateEmptyWorkflow()
    {
        var stepId = "step_01";
        return NormalizeWorkflow(new WorkflowDefinition
        {
            Id = $"workflow_{DateTimeOffset.UtcNow:MMddHHmmss}",
            Name = "New Workflow",
            Description = "New linear workflow.",
            WorkingDirectory = _paths.ResolvedRootDirectory,
            GlobalSettings = new WorkflowGlobalSettings
            {
                DefaultWrapperId = "nexusagent",
                DefaultMaxTokens = 1200,
                DefaultTimeoutSec = 240,
                StopOnError = true
            },
            Inputs = new List<WorkflowInputLabel>
            {
                new()
                {
                    Key = "goal",
                    Label = "Goal",
                    Kind = "text",
                    Required = false,
                    DefaultValue = string.Empty
                }
            },
            Steps = new List<WorkflowStepDefinition>
            {
                new WorkflowAgentStep
                {
                    Id = stepId,
                    Name = "Draft",
                    Type = "agent",
                    PromptTemplate = "Goal:\n{{goal}}",
                    OutputBindings = new List<WorkflowOutputBinding>
                    {
                        new()
                        {
                            Source = "final_text",
                            TargetLabel = $"{stepId}.final_text"
                        }
                    }
                }
            }
        }, "local", string.Empty);
    }

    private void LoadDirectory(string directory, string source, Dictionary<string, WorkflowDefinition> workflows)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                     .Where(path => path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var workflow = _deserializer.Deserialize<WorkflowDefinition>(File.ReadAllText(path)) ?? new WorkflowDefinition();
                workflow = NormalizeWorkflow(workflow, source, path);
                workflows[workflow.Id] = workflow;
            }
            catch (Exception ex)
            {
                _logs.Append("error", "workflows", $"Failed to load workflow '{path}': {ex.Message}");
            }
        }
    }

    private WorkflowDefinition NormalizeWorkflow(WorkflowDefinition workflow, string source, string sourcePath)
    {
        workflow.Id = string.IsNullOrWhiteSpace(workflow.Id)
            ? Path.GetFileNameWithoutExtension(string.IsNullOrWhiteSpace(sourcePath) ? $"workflow_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}" : sourcePath)
            : workflow.Id.Trim();
        workflow.Name = string.IsNullOrWhiteSpace(workflow.Name) ? workflow.Id : workflow.Name.Trim();
        workflow.Description = workflow.Description?.Trim() ?? string.Empty;
        workflow.WorkingDirectory = string.IsNullOrWhiteSpace(workflow.WorkingDirectory) ? _paths.ResolvedRootDirectory : workflow.WorkingDirectory.Trim();
        workflow.GlobalSettings ??= new WorkflowGlobalSettings();
        if (workflow.GlobalSettings.DefaultMaxTokens <= 0)
        {
            workflow.GlobalSettings.DefaultMaxTokens = 1200;
        }
        if (workflow.GlobalSettings.DefaultTimeoutSec <= 0)
        {
            workflow.GlobalSettings.DefaultTimeoutSec = 240;
        }
        workflow.Inputs ??= new List<WorkflowInputLabel>();
        workflow.Steps ??= new List<WorkflowStepDefinition>();
        workflow.Tags ??= new List<string>();
        workflow.Source = source;
        workflow.SourcePath = sourcePath;

        if (workflow.Inputs.Count == 0)
        {
            workflow.Inputs.Add(new WorkflowInputLabel
            {
                Key = "goal",
                Label = "Goal",
                Kind = "text",
                Required = false,
                DefaultValue = string.Empty
            });
        }

        if (workflow.Steps.Count == 0)
        {
            workflow.Steps.Add(new WorkflowAgentStep
            {
                Id = "step_01",
                Name = "Draft",
                Type = "agent",
                PromptTemplate = "Goal:\n{{goal}}"
            });
        }

        for (var index = 0; index < workflow.Inputs.Count; index++)
        {
            var input = workflow.Inputs[index] ?? new WorkflowInputLabel();
            input.Key = string.IsNullOrWhiteSpace(input.Key) ? $"input_{index + 1}" : input.Key.Trim();
            input.Label = string.IsNullOrWhiteSpace(input.Label) ? input.Key : input.Label.Trim();
            input.Kind = string.IsNullOrWhiteSpace(input.Kind) ? "text" : input.Kind.Trim().ToLowerInvariant();
            input.DefaultValue ??= string.Empty;
            workflow.Inputs[index] = input;
        }

        for (var index = 0; index < workflow.Steps.Count; index++)
        {
            var step = workflow.Steps[index] ?? new WorkflowStepDefinition();
            step.Id = string.IsNullOrWhiteSpace(step.Id) ? $"step_{index + 1:00}" : step.Id.Trim();
            step.Name = string.IsNullOrWhiteSpace(step.Name) ? step.Id : step.Name.Trim();
            step.Type = string.Equals(step.Type, "tool_call", StringComparison.OrdinalIgnoreCase) ? "tool_call" : "agent";
            step.Enabled = step.Enabled || step.Type is "agent" or "tool_call";
            step.WorkingDirectoryOverride ??= string.Empty;
            step.TemplateId ??= string.Empty;
            step.WrapperIdOverride ??= string.Empty;
            step.ModelIdOverride ??= string.Empty;
            step.Flags ??= new List<TemplateArgDefinition>();
            step.RawFlags ??= string.Empty;
            step.PromptTemplate ??= string.Empty;
            step.ArtifactInputs ??= new List<string>();
            step.InputBindings ??= new List<WorkflowBinding>();
            step.OutputBindings ??= new List<WorkflowOutputBinding>();
            step.IntegrationId = string.IsNullOrWhiteSpace(step.IntegrationId) ? "nexuscentral" : step.IntegrationId.Trim();
            step.ToolName ??= string.Empty;
            step.Arguments ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            workflow.Steps[index] = step;
        }

        return workflow;
    }

    private string ResolveBundlePath(string bundleIdOrPath)
    {
        if (Path.IsPathRooted(bundleIdOrPath) || bundleIdOrPath.Contains(Path.DirectorySeparatorChar) || bundleIdOrPath.Contains(Path.AltDirectorySeparatorChar))
        {
            return bundleIdOrPath;
        }

        var direct = Path.Combine(_paths.WorkflowBundlesDirectory, bundleIdOrPath);
        if (File.Exists(direct))
        {
            return direct;
        }

        return Path.Combine(_paths.WorkflowBundlesDirectory, $"{bundleIdOrPath}.yaml");
    }
}
