---
name: workflow-modeling-it
description: "Use when designing workflow YAML, validating workflow models, importing definitions, or shaping automation steps."
---

# Workflow Modeling It

## When to Use
- Author or transform workflow YAML.
- Validate workflow structure and step references.
- Import workflows from disk into runtime models.

## Procedure
1. Normalize the workflow before validation or export.
2. Validate steps, bindings, and referenced services.
3. Keep the YAML shape deterministic and minimal.

## References
- [WorkflowModels.cs](../../../Desktop/Models/WorkflowModels.cs)
- [WorkflowValidationModels.cs](../../../Desktop/Models/WorkflowValidationModels.cs)
- [WorkflowYamlUtility.cs](../../../Desktop/Services/WorkflowYamlUtility.cs)
- [WorkflowValidationService.cs](../../../Desktop/Services/WorkflowValidationService.cs)
- [WorkflowImportService.cs](../../../Desktop/Services/WorkflowImportService.cs)
