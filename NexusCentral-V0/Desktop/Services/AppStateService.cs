using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusCentral_Avalonia.Services;

public class AppStateService
{
    private readonly List<string> _tabs = new();

    public event Action? Changed;

    public string StatusText { get; private set; } = "● Disconnected";
    public string SummaryText { get; private set; } = "Supervisor v0.0.0 | Uptime: 0s";
    public string? CurrentTab { get; private set; }
    public IReadOnlyList<string> Tabs => _tabs;
    public Func<string, bool>? SelectTabHandler { get; set; }

    public void RegisterTabs(IEnumerable<string> tabs, string? currentTab)
    {
        _tabs.Clear();
        _tabs.AddRange(tabs);
        CurrentTab = currentTab;
        Changed?.Invoke();
    }

    public bool TrySelectTab(string tab)
    {
        if (string.IsNullOrWhiteSpace(tab))
        {
            return false;
        }

        if (SelectTabHandler?.Invoke(tab) == true)
        {
            CurrentTab = _tabs.FirstOrDefault(candidate =>
                string.Equals(candidate, tab, StringComparison.OrdinalIgnoreCase)) ?? tab;
            Changed?.Invoke();
            return true;
        }

        return false;
    }

    public void SetCurrentTab(string? tab)
    {
        CurrentTab = tab;
        Changed?.Invoke();
    }

    public void UpdateStatus(string statusText, string summaryText)
    {
        StatusText = statusText;
        SummaryText = summaryText;
        Changed?.Invoke();
    }
}
