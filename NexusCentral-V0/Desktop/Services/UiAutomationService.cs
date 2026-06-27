using System;
using System.Collections.Generic;

namespace NexusCentral_Avalonia.Services;

public class UiAutomationService
{
    private readonly HashSet<string> _expandedElements = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    public string? HighlightedElementId { get; private set; }

    public bool IsHighlighted(string elementId) =>
        !string.IsNullOrWhiteSpace(elementId) &&
        string.Equals(HighlightedElementId, elementId, StringComparison.OrdinalIgnoreCase);

    public bool IsExpanded(string elementId) =>
        !string.IsNullOrWhiteSpace(elementId) && _expandedElements.Contains(elementId);

    public void Highlight(string? elementId)
    {
        var normalized = string.IsNullOrWhiteSpace(elementId) ? null : elementId.Trim();
        if (string.Equals(HighlightedElementId, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        HighlightedElementId = normalized;
        Changed?.Invoke();
    }

    public void SetExpanded(string elementId, bool expanded)
    {
        if (string.IsNullOrWhiteSpace(elementId))
        {
            return;
        }

        var changed = expanded
            ? _expandedElements.Add(elementId)
            : _expandedElements.Remove(elementId);

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public void FocusElement(string elementId, bool expanded)
    {
        Highlight(elementId);
        SetExpanded(elementId, expanded);
    }
}
