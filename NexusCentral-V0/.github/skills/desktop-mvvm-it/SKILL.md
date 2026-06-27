---
name: desktop-mvvm-it
description: "Use when editing Avalonia MVVM view models, bindings, commands, collections, or desktop UI state."
---

# Desktop MVVM It

## When to Use
- Build or edit Avalonia view models.
- Add commands, observable state, or collection logic.
- Wire UI automation-friendly properties and events.

## Procedure
1. Inherit from `ViewModelBase` and use MVVM Toolkit attributes.
2. Keep UI state reactive with observable properties and collections.
3. Inject `AppServices` and route behavior through services, not UI code.

## References
- [ChatViewModel.cs](../../../Desktop/ViewModels/ChatViewModel.cs)
- [AutomationsViewModel.cs](../../../Desktop/ViewModels/AutomationsViewModel.cs)
- [WorkflowViewModel.cs](../../../Desktop/ViewModels/WorkflowViewModel.cs)
- [ViewModelBase.cs](../../../Desktop/ViewModels/ViewModelBase.cs)
- [ChatRenderingViewModels.cs](../../../Desktop/ViewModels/ChatRenderingViewModels.cs)
