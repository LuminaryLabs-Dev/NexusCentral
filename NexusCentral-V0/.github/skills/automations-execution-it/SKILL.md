---
name: automations-execution-it
description: "Use when building the Automations tab, chaining multi-file step execution, or reporting step progress."
---

# Automations Execution It

## When to Use
- Build or adjust the Automations tab.
- Run multi-step automation workflows.
- Aggregate step inputs into one prompt and one output file.

## Procedure
1. Build a request with workflows, steps, and output targets.
2. Aggregate inputs deterministically and call the runtime once per step.
3. Report step progress and keep final output paths visible to the UI.

## References
- [AutomationsViewModel.cs](../../../Desktop/ViewModels/AutomationsViewModel.cs)
- [AutomationExecutionService.cs](../../../Desktop/Services/AutomationExecutionService.cs)
- [AutomationExecutionModels.cs](../../../Desktop/Models/AutomationExecutionModels.cs)
- [AutomationModels.cs](../../../Desktop/Models/AutomationModels.cs)
