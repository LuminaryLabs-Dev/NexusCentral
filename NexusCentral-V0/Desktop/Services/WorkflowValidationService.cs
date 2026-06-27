using System;
using System.Collections.Generic;
using System.Linq;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class WorkflowValidationService
{
    private readonly WrapperCatalogService _wrappers;
    private readonly IntegrationRegistryService _integrations;

    public WorkflowValidationService(WrapperCatalogService wrappers, IntegrationRegistryService integrations)
    {
        _wrappers = wrappers;
        _integrations = integrations;
    }

    public WorkflowValidationResult Validate(WorkflowDefinition workflow)
    {
        var issues = new List<WorkflowValidationIssue>();

        if (string.IsNullOrWhiteSpace(workflow.Id))
        {
            issues.Add(new WorkflowValidationIssue { Level = "error", Message = "Workflow id is required." });
        }

        if (string.IsNullOrWhiteSpace(workflow.Name))
        {
            issues.Add(new WorkflowValidationIssue { Level = "error", Message = "Workflow name is required." });
        }

        if (workflow.Steps.Count == 0)
        {
            issues.Add(new WorkflowValidationIssue { Level = "error", Message = "At least one workflow step is required." });
        }

        var duplicateInputs = workflow.Inputs
            .GroupBy(input => input.Key ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        foreach (var duplicate in duplicateInputs)
        {
            issues.Add(new WorkflowValidationIssue { Level = "error", Message = $"Duplicate input key '{duplicate}'." });
        }

        var duplicateSteps = workflow.Steps
            .GroupBy(step => step.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        foreach (var duplicate in duplicateSteps)
        {
            issues.Add(new WorkflowValidationIssue { Level = "error", Message = $"Duplicate step id '{duplicate}'." });
        }

        var defaultWrapper = string.IsNullOrWhiteSpace(workflow.GlobalSettings.DefaultWrapperId) ? "nexusagent" : workflow.GlobalSettings.DefaultWrapperId.Trim();
        if (_wrappers.FindById(defaultWrapper) is null)
        {
            issues.Add(new WorkflowValidationIssue
            {
                Level = "error",
                Message = $"Default wrapper '{defaultWrapper}' is not available."
            });
        }

        foreach (var step in workflow.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id))
            {
                issues.Add(new WorkflowValidationIssue { Level = "error", Message = "Each workflow step must have an id." });
            }

            if (string.IsNullOrWhiteSpace(step.Name))
            {
                issues.Add(new WorkflowValidationIssue { Level = "error", Message = $"Step '{step.Id}' must have a name." });
            }

            if (!string.Equals(step.Type, "agent", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(step.Type, "tool_call", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new WorkflowValidationIssue
                {
                    Level = "error",
                    Message = $"Step '{step.Id}' uses unsupported type '{step.Type}'."
                });
                continue;
            }

            if (string.Equals(step.Type, "agent", StringComparison.OrdinalIgnoreCase))
            {
                var wrapperId = string.IsNullOrWhiteSpace(step.WrapperIdOverride) ? defaultWrapper : step.WrapperIdOverride.Trim();
                if (_wrappers.FindById(wrapperId) is null)
                {
                    issues.Add(new WorkflowValidationIssue
                    {
                        Level = "error",
                        Message = $"Step '{step.Id}' references unknown wrapper '{wrapperId}'."
                    });
                }

                if (string.IsNullOrWhiteSpace(step.PromptTemplate) && string.IsNullOrWhiteSpace(step.TemplateId))
                {
                    issues.Add(new WorkflowValidationIssue
                    {
                        Level = "warning",
                        Message = $"Agent step '{step.Id}' has no prompt template; it will fall back to a generic prompt."
                    });
                }
            }

            if (string.Equals(step.Type, "tool_call", StringComparison.OrdinalIgnoreCase))
            {
                var integration = _integrations.GetIntegration(step.IntegrationId);
                if (integration is null || !integration.Enabled)
                {
                    issues.Add(new WorkflowValidationIssue
                    {
                        Level = "error",
                        Message = $"Tool step '{step.Id}' references unavailable integration '{step.IntegrationId}'."
                    });
                }
                else if (integration.Tools.All(tool => !string.Equals(tool.Name, step.ToolName, StringComparison.OrdinalIgnoreCase) || !tool.Enabled))
                {
                    issues.Add(new WorkflowValidationIssue
                    {
                        Level = "error",
                        Message = $"Tool step '{step.Id}' references unavailable tool '{step.ToolName}'."
                    });
                }
            }

            foreach (var binding in step.InputBindings.Where(binding => string.IsNullOrWhiteSpace(binding.Target)))
            {
                issues.Add(new WorkflowValidationIssue
                {
                    Level = "warning",
                    Message = $"Step '{step.Id}' has an input binding without a target."
                });
            }

            foreach (var binding in step.OutputBindings.Where(binding => string.IsNullOrWhiteSpace(binding.TargetLabel)))
            {
                issues.Add(new WorkflowValidationIssue
                {
                    Level = "warning",
                    Message = $"Step '{step.Id}' has an output binding without a target label."
                });
            }
        }

        return new WorkflowValidationResult
        {
            Status = issues.Any(issue => string.Equals(issue.Level, "error", StringComparison.OrdinalIgnoreCase)) ? "invalid" : "valid",
            Readiness = issues.Any(issue => string.Equals(issue.Level, "error", StringComparison.OrdinalIgnoreCase)) ? "Invalid" : "Ready",
            ValidatedAtUtc = DateTimeOffset.UtcNow,
            Issues = issues
        };
    }
}
