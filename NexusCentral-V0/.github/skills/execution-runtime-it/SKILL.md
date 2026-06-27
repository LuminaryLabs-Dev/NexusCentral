---
name: execution-runtime-it
description: "Use when implementing async execution, progress reporting, runtime calls, or multi-step orchestration."
---

# Execution Runtime It

## When to Use
- Invoke the runtime or a workflow runner.
- Add progress streaming, status updates, or cancellation.
- Collect and persist execution results.

## Procedure
1. Call the runtime service through an async entry point.
2. Report progress with `IProgress` and update UI state as events arrive.
3. Return a structured result that captures output, status, and errors.

## References
- [RuntimeService.cs](../../../Desktop/Services/RuntimeService.cs)
- [WorkflowRunService.cs](../../../Desktop/Services/WorkflowRunService.cs)
- [AutomationExecutionService.cs](../../../Desktop/Services/AutomationExecutionService.cs)
- [RuntimeInvocationResult.cs](../../../Desktop/Models/RuntimeInvocationResult.cs)
- [RuntimeStreamEvent.cs](../../../Desktop/Models/RuntimeStreamEvent.cs)
