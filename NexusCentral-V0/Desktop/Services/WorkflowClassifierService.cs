using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class WorkflowClassifierService
{
    private readonly AppPathsService _paths;
    private readonly WorkflowValidationService _validation;

    public WorkflowClassifierService(AppPathsService paths, WorkflowValidationService validation)
    {
        _paths = paths;
        _validation = validation;
    }

    public AutomationWorkflowFileState ClassifyFile(string sourceDirectory, string filePath)
    {
        var state = new AutomationWorkflowFileState
        {
            FilePath = Path.GetFullPath(filePath),
            FileName = Path.GetFileName(filePath),
            RelativePath = SafeRelativePath(sourceDirectory, filePath)
        };

        try
        {
            var text = File.ReadAllText(filePath);
            var document = WorkflowYamlUtility.DeserializeDocument(text);
            var map = WorkflowYamlUtility.AsMap(document);
            var kind = WorkflowYamlUtility.GetString(map, "kind", string.Empty);

            if (string.Equals(kind, "workflow_spec", StringComparison.OrdinalIgnoreCase))
            {
                PopulateWorkflowSpecState(state, map);
                return state;
            }

            if (string.Equals(kind, "tool_contract", StringComparison.OrdinalIgnoreCase))
            {
                state.Kind = "tool_contract";
                state.Readiness = "Unsupported";
                state.Classification = "unsupported";
                state.WorkflowId = WorkflowYamlUtility.GetString(WorkflowYamlUtility.AsMap(WorkflowYamlUtility.GetValue(map, "metadata")), "id", Path.GetFileNameWithoutExtension(filePath));
                state.WorkflowName = WorkflowYamlUtility.GetString(WorkflowYamlUtility.AsMap(WorkflowYamlUtility.GetValue(map, "metadata")), "name", state.WorkflowId);
                state.Summary = WorkflowYamlUtility.GetString(WorkflowYamlUtility.AsMap(WorkflowYamlUtility.GetValue(map, "summary")), "purpose", "Tool contract files are not executable workflows.");
                return state;
            }

            if (LooksLikeExecutableWorkflow(map))
            {
                PopulateExecutableWorkflowState(state, text, filePath);
                return state;
            }

            state.Kind = string.IsNullOrWhiteSpace(kind) ? "unknown" : kind;
            state.Readiness = "Unsupported";
            state.Classification = "unsupported";
            state.WorkflowId = Path.GetFileNameWithoutExtension(filePath);
            state.WorkflowName = state.WorkflowId;
            state.Summary = "YAML file does not match a supported workflow format.";
            return state;
        }
        catch (Exception ex)
        {
            state.Kind = "invalid_yaml";
            state.Readiness = "Invalid";
            state.Classification = "invalid";
            state.WorkflowId = Path.GetFileNameWithoutExtension(filePath);
            state.WorkflowName = state.WorkflowId;
            state.ValidationStatus = "invalid";
            state.ValidationMessage = ex.Message;
            state.Summary = "YAML file could not be parsed.";
            return state;
        }
    }

    private void PopulateWorkflowSpecState(AutomationWorkflowFileState state, IDictionary<object, object>? map)
    {
        var metadata = WorkflowYamlUtility.AsMap(WorkflowYamlUtility.GetValue(map, "metadata"));
        var summary = WorkflowYamlUtility.AsMap(WorkflowYamlUtility.GetValue(map, "summary"));
        var inputs = WorkflowYamlUtility.AsMap(WorkflowYamlUtility.GetValue(map, "inputs"));
        var writes = WorkflowYamlUtility.AsMap(WorkflowYamlUtility.GetValue(map, "writes"));
        var validation = WorkflowYamlUtility.AsMap(WorkflowYamlUtility.GetValue(map, "validation"));

        state.Kind = "workflow_spec";
        state.Readiness = "Import Required";
        state.Classification = "import_required";
        state.WorkflowId = WorkflowYamlUtility.GetString(metadata, "id", Path.GetFileNameWithoutExtension(state.FilePath));
        state.WorkflowName = WorkflowYamlUtility.GetString(metadata, "name", state.WorkflowId);
        state.Summary = WorkflowYamlUtility.GetString(summary, "purpose", "Builder workflow spec.");
        state.Inputs = ReadInputDescriptions(inputs);
        state.Outputs = ReadWriteDescriptions(writes);
        state.ValidationChecks = ReadChecks(validation);
        state.References = new List<string> { "Imported draft will use wrapper: nexusagent" };
        state.Enabled = false;
        state.ValidationStatus = "import_required";
        state.ValidationMessage = "Builder workflow specs must be converted before they can run.";
    }

    private void PopulateExecutableWorkflowState(AutomationWorkflowFileState state, string text, string filePath)
    {
        var workflow = WorkflowYamlUtility.DeserializeWorkflowDefinition(text);
        if (workflow is null)
        {
            state.Kind = "native_workflow";
            state.Readiness = "Invalid";
            state.Classification = "invalid";
            state.WorkflowId = Path.GetFileNameWithoutExtension(filePath);
            state.WorkflowName = state.WorkflowId;
            state.ValidationStatus = "invalid";
            state.ValidationMessage = "Workflow YAML could not be read into the executable workflow model.";
            state.Summary = "Nexus Central workflow YAML is malformed.";
            return;
        }

        workflow = WorkflowYamlUtility.NormalizeWorkflow(workflow, _paths, "external", filePath);
        var validation = _validation.Validate(workflow);

        state.Kind = "native_workflow";
        state.Classification = "executable";
        state.WorkflowId = workflow.Id;
        state.WorkflowName = workflow.Name;
        state.Summary = string.IsNullOrWhiteSpace(workflow.Description) ? "Executable Nexus Central workflow." : workflow.Description;
        state.Inputs = workflow.Inputs.Select(input => $"{input.Key} ({input.Kind})").ToList();
        state.Outputs = ReadWorkflowOutputs(workflow);
        state.ValidationChecks = new List<string>
        {
            "Workflow id and name must be present.",
            "Enabled steps must use supported step types.",
            "Wrapper, integration, and tool references must resolve."
        };
        state.References = ReadWorkflowReferences(workflow);
        state.ExecutableWorkflow = workflow.Clone();
        state.ValidationResult = validation.Clone();
        state.ValidationStatus = validation.Status;
        state.ValidationMessage = validation.Summary;
        state.Readiness = validation.Readiness;
        state.Enabled = validation.IsValid;
    }

    private static bool LooksLikeExecutableWorkflow(IDictionary<object, object>? map)
    {
        if (map is null)
        {
            return false;
        }

        if (WorkflowYamlUtility.GetValue(map, "kind") is not null)
        {
            return false;
        }

        var steps = WorkflowYamlUtility.AsList(WorkflowYamlUtility.GetValue(map, "steps"));
        return steps is not null && steps.Count > 0;
    }

    private static List<string> ReadInputDescriptions(IDictionary<object, object>? inputs)
    {
        var result = new List<string>();
        foreach (var bucket in new[] { "required", "optional" })
        {
            var list = WorkflowYamlUtility.AsList(WorkflowYamlUtility.GetValue(inputs, bucket));
            if (list is null)
            {
                continue;
            }

            foreach (var item in list)
            {
                var map = WorkflowYamlUtility.AsMap(item);
                var name = WorkflowYamlUtility.GetString(map, "name", "input");
                var type = WorkflowYamlUtility.GetString(map, "type", "text");
                result.Add($"{name} ({type})");
            }
        }

        return result;
    }

    private static List<string> ReadWriteDescriptions(IDictionary<object, object>? writes)
    {
        var result = new List<string>();
        foreach (var bucket in new[] { "primary", "secondary" })
        {
            var list = WorkflowYamlUtility.AsList(WorkflowYamlUtility.GetValue(writes, bucket));
            if (list is null)
            {
                continue;
            }

            foreach (var item in list)
            {
                var map = WorkflowYamlUtility.AsMap(item);
                var path = WorkflowYamlUtility.GetString(map, "path");
                var description = WorkflowYamlUtility.GetString(map, "description");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    result.Add(string.IsNullOrWhiteSpace(description) ? path : $"{path}: {description}");
                }
            }
        }

        return result;
    }

    private static List<string> ReadChecks(IDictionary<object, object>? validation)
    {
        return WorkflowYamlUtility.GetStringList(validation, "checks");
    }

    private static List<string> ReadWorkflowOutputs(WorkflowDefinition workflow)
    {
        var outputs = new List<string>();
        foreach (var step in workflow.Steps)
        {
            if (step.OutputBindings.Count == 0)
            {
                outputs.Add($"{step.Id}: {(string.Equals(step.Type, "tool_call", StringComparison.OrdinalIgnoreCase) ? $"{step.Id}.response" : $"{step.Id}.final_text")}");
                continue;
            }

            outputs.AddRange(step.OutputBindings
                .Where(binding => !string.IsNullOrWhiteSpace(binding.TargetLabel))
                .Select(binding => $"{step.Id}: {binding.TargetLabel}"));
        }

        return outputs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ReadWorkflowReferences(WorkflowDefinition workflow)
    {
        var references = new List<string> { $"Default wrapper: {workflow.GlobalSettings.DefaultWrapperId}" };
        references.AddRange(workflow.Steps
            .Where(step => string.Equals(step.Type, "agent", StringComparison.OrdinalIgnoreCase))
            .Select(step => $"Step {step.Id}: wrapper {(string.IsNullOrWhiteSpace(step.WrapperIdOverride) ? workflow.GlobalSettings.DefaultWrapperId : step.WrapperIdOverride)}"));
        references.AddRange(workflow.Steps
            .Where(step => string.Equals(step.Type, "tool_call", StringComparison.OrdinalIgnoreCase))
            .Select(step => $"Step {step.Id}: tool {step.IntegrationId}/{step.ToolName}"));
        return references.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string SafeRelativePath(string sourceDirectory, string filePath)
    {
        try
        {
            return string.IsNullOrWhiteSpace(sourceDirectory)
                ? filePath
                : Path.GetRelativePath(sourceDirectory, filePath);
        }
        catch
        {
            return filePath;
        }
    }
}
