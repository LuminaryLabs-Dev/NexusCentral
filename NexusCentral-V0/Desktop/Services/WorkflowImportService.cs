using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class WorkflowImportService
{
    private readonly AppPathsService _paths;

    public WorkflowImportService(AppPathsService paths)
    {
        _paths = paths;
    }

    public WorkflowDefinition? ImportBuilderWorkflowSpec(string filePath)
    {
        var text = File.ReadAllText(filePath);
        var map = WorkflowYamlUtility.AsMap(WorkflowYamlUtility.DeserializeDocument(text));
        if (!string.Equals(WorkflowYamlUtility.GetString(map, "kind"), "workflow_spec", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var metadata = WorkflowYamlUtility.AsMap(WorkflowYamlUtility.GetValue(map, "metadata"));
        var summary = WorkflowYamlUtility.AsMap(WorkflowYamlUtility.GetValue(map, "summary"));
        var inputs = WorkflowYamlUtility.AsMap(WorkflowYamlUtility.GetValue(map, "inputs"));
        var steps = WorkflowYamlUtility.AsList(WorkflowYamlUtility.GetValue(map, "steps"));

        var workflow = new WorkflowDefinition
        {
            Id = WorkflowYamlUtility.GetString(metadata, "id", Path.GetFileNameWithoutExtension(filePath)),
            Name = WorkflowYamlUtility.GetString(metadata, "name", Path.GetFileNameWithoutExtension(filePath)),
            Description = WorkflowYamlUtility.GetString(summary, "purpose", "Imported Builder workflow spec."),
            WorkingDirectory = Path.GetDirectoryName(filePath) ?? _paths.ResolvedRootDirectory,
            GlobalSettings = new WorkflowGlobalSettings
            {
                DefaultWrapperId = "nexusagent",
                DefaultMaxTokens = 1200,
                DefaultTimeoutSec = 240,
                StopOnError = true
            },
            Inputs = ReadInputs(inputs),
            Steps = ReadSteps(steps, filePath),
            Tags = WorkflowYamlUtility.GetStringList(metadata, "tags"),
            Source = "imported_spec",
            SourcePath = filePath
        };

        return WorkflowYamlUtility.NormalizeWorkflow(workflow, _paths, "imported_spec", filePath);
    }

    private static List<WorkflowInputLabel> ReadInputs(IDictionary<object, object>? inputs)
    {
        var result = new List<WorkflowInputLabel>();
        foreach (var bucket in new[] { "required", "optional" })
        {
            var required = string.Equals(bucket, "required", StringComparison.OrdinalIgnoreCase);
            var list = WorkflowYamlUtility.AsList(WorkflowYamlUtility.GetValue(inputs, bucket));
            if (list is null)
            {
                continue;
            }

            foreach (var item in list)
            {
                var map = WorkflowYamlUtility.AsMap(item);
                var name = WorkflowYamlUtility.GetString(map, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var type = WorkflowYamlUtility.GetString(map, "type", "string");
                result.Add(new WorkflowInputLabel
                {
                    Key = name,
                    Label = name.Replace('_', ' '),
                    Kind = MapInputKind(type),
                    Required = required,
                    DefaultValue = WorkflowYamlUtility.ConvertValueToString(WorkflowYamlUtility.GetValue(map, "default"))
                });
            }
        }

        if (result.All(input => !string.Equals(input.Key, "workspace_root", StringComparison.OrdinalIgnoreCase)))
        {
            result.Insert(0, new WorkflowInputLabel
            {
                Key = "workspace_root",
                Label = "workspace root",
                Kind = "path",
                Required = true,
                DefaultValue = string.Empty
            });
        }

        return result;
    }

    private static List<WorkflowStepDefinition> ReadSteps(IList? steps, string filePath)
    {
        var result = new List<WorkflowStepDefinition>();
        if (steps is null)
        {
            return result;
        }

        var index = 1;
        foreach (var item in steps)
        {
            var map = WorkflowYamlUtility.AsMap(item);
            var stepId = WorkflowYamlUtility.GetString(map, "id", $"step_{index:00}");
            var objective = WorkflowYamlUtility.GetString(map, "objective", "Complete the imported workflow step.");
            var actions = WorkflowYamlUtility.GetStringList(map, "actions");
            var prompt = BuildPrompt(filePath, stepId, objective, actions);

            result.Add(new WorkflowAgentStep
            {
                Id = stepId,
                Name = objective,
                Type = "agent",
                PromptTemplate = prompt,
                AppendPreviousStepOutput = true,
                OutputBindings = new List<WorkflowOutputBinding>
                {
                    new()
                    {
                        Source = "final_text",
                        TargetLabel = $"{stepId}.final_text"
                    }
                }
            });

            index++;
        }

        if (result.Count == 0)
        {
            result.Add(new WorkflowAgentStep
            {
                Id = "step_01",
                Name = "Imported Draft Step",
                PromptTemplate = BuildPrompt(filePath, "step_01", "Review the imported Builder workflow spec and produce the first draft output.", new List<string>()),
                AppendPreviousStepOutput = true
            });
        }

        return result.Cast<WorkflowStepDefinition>().ToList();
    }

    private static string BuildPrompt(string filePath, string stepId, string objective, IReadOnlyList<string> actions)
    {
        var actionText = actions.Count == 0
            ? "- Review the source Builder spec and convert the intent into a concrete step output."
            : string.Join(Environment.NewLine, actions.Select(action => $"- {action}"));

        return
$@"This workflow was imported from a NexusCentral-Builder workflow spec.

Source spec:
{filePath}

Step id:
{stepId}

Objective:
{objective}

Requested actions:
{actionText}

Use the current workflow inputs and prior step context to produce the concrete output for this step.
";
    }

    private static string MapInputKind(string type)
    {
        var normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("path"))
        {
            return "path";
        }

        if (normalized.Contains("bool"))
        {
            return "bool";
        }

        if (normalized.Contains("number") || normalized.Contains("int") || normalized.Contains("float"))
        {
            return "number";
        }

        if (normalized.Contains("json") || normalized.Contains("list[") || normalized.Contains("dict") || normalized.Contains("map"))
        {
            return "json";
        }

        return "text";
    }
}
