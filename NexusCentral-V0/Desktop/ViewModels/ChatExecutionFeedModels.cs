using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace NexusCentral_Avalonia.ViewModels;

public enum ChatExecutionFeedSection
{
    System,
    Memory,
    Agent,
    Model,
    Artifacts,
    Workflow
}

public static class ChatExecutionFeedClassifier
{
    public static ChatExecutionFeedSection InferSection(string? text, string? type = null, string? sectionHint = null, string? categoryHint = null)
    {
        if (TryParseSection(sectionHint, out var explicitSection))
        {
            return explicitSection;
        }

        if (TryParseSection(categoryHint, out var categorySection))
        {
            return categorySection;
        }

        var normalizedType = Normalize(type);
        if (normalizedType is "artifact")
        {
            return ChatExecutionFeedSection.Artifacts;
        }

        if (normalizedType is "workflow")
        {
            return ChatExecutionFeedSection.Workflow;
        }

        if (normalizedType is "error")
        {
            return InferFromText(text, ChatExecutionFeedSection.System);
        }

        return InferFromText(text, ChatExecutionFeedSection.System);
    }

    public static string GetSectionLabel(ChatExecutionFeedSection section) =>
        section switch
        {
            ChatExecutionFeedSection.Memory => "Memory",
            ChatExecutionFeedSection.Agent => "Agent",
            ChatExecutionFeedSection.Model => "Model",
            ChatExecutionFeedSection.Artifacts => "Artifacts",
            ChatExecutionFeedSection.Workflow => "Workflow",
            _ => "System"
        };

    private static bool TryParseSection(string? value, out ChatExecutionFeedSection section)
    {
        switch (Normalize(value))
        {
            case "memory":
                section = ChatExecutionFeedSection.Memory;
                return true;
            case "agent":
            case "wrapper":
                section = ChatExecutionFeedSection.Agent;
                return true;
            case "model":
            case "runtime":
                section = ChatExecutionFeedSection.Model;
                return true;
            case "artifact":
            case "artifacts":
                section = ChatExecutionFeedSection.Artifacts;
                return true;
            case "workflow":
            case "step":
            case "tool":
                section = ChatExecutionFeedSection.Workflow;
                return true;
            case "system":
            case "status":
            case "log":
                section = ChatExecutionFeedSection.System;
                return true;
            default:
                section = ChatExecutionFeedSection.System;
                return false;
        }
    }

    private static ChatExecutionFeedSection InferFromText(string? text, ChatExecutionFeedSection fallback)
    {
        var normalized = Normalize(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        if (normalized.Contains("memory", StringComparison.Ordinal) ||
            normalized.Contains("toon", StringComparison.Ordinal) ||
            normalized.Contains("session", StringComparison.Ordinal) ||
            normalized.Contains("retrieved", StringComparison.Ordinal))
        {
            return ChatExecutionFeedSection.Memory;
        }

        if (normalized.Contains("workflow", StringComparison.Ordinal) ||
            normalized.Contains("step ", StringComparison.Ordinal) ||
            normalized.Contains("tool call", StringComparison.Ordinal) ||
            normalized.Contains("tool:", StringComparison.Ordinal))
        {
            return ChatExecutionFeedSection.Workflow;
        }

        if (normalized.Contains("model", StringComparison.Ordinal) ||
            normalized.Contains("runtime", StringComparison.Ordinal) ||
            normalized.Contains("thinking route", StringComparison.Ordinal) ||
            normalized.Contains("routing", StringComparison.Ordinal) ||
            normalized.Contains("generating", StringComparison.Ordinal) ||
            normalized.Contains("response stream", StringComparison.Ordinal) ||
            normalized.Contains("selected model", StringComparison.Ordinal) ||
            normalized.Contains("llm", StringComparison.Ordinal))
        {
            return ChatExecutionFeedSection.Model;
        }

        if (normalized.Contains("artifact", StringComparison.Ordinal) ||
            normalized.Contains("file:", StringComparison.Ordinal))
        {
            return ChatExecutionFeedSection.Artifacts;
        }

        if (normalized.Contains("wrapper", StringComparison.Ordinal) ||
            normalized.Contains("agent", StringComparison.Ordinal) ||
            normalized.Contains("input", StringComparison.Ordinal) ||
            normalized.Contains("prompt", StringComparison.Ordinal))
        {
            return ChatExecutionFeedSection.Agent;
        }

        return fallback;
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

public partial class ChatExecutionFeedEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _kind = "status";

    [ObservableProperty]
    private ChatExecutionFeedSection _section = ChatExecutionFeedSection.System;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _timestamp = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private string _level = "info";

    [ObservableProperty]
    private bool _showSectionHeader;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private string _artifactLabel = string.Empty;

    [ObservableProperty]
    private string _artifactPath = string.Empty;

    [ObservableProperty]
    private string _validationStatus = string.Empty;

    [ObservableProperty]
    private string _approvalId = string.Empty;

    [ObservableProperty]
    private string _toolName = string.Empty;

    [ObservableProperty]
    private string _memoryId = string.Empty;

    public string SectionLabel => ChatExecutionFeedClassifier.GetSectionLabel(Section);

    public string DisplayTimestamp => Timestamp.ToLocalTime().ToString("HH:mm:ss");

    public string AccentBrushResourceKey =>
        Level switch
        {
            "error" => "TokyoErrorBrush",
            "warning" => "TokyoWarningBrush",
            "success" => "TokyoSuccessBrush",
            _ => "TokyoAccentBrush"
        };

    public bool HasArtifact => !string.IsNullOrWhiteSpace(ArtifactPath);
    public bool HasValidationStatus => !string.IsNullOrWhiteSpace(ValidationStatus);
    public bool HasApprovalId => !string.IsNullOrWhiteSpace(ApprovalId);
    public bool HasToolName => !string.IsNullOrWhiteSpace(ToolName);
    public bool HasMemoryId => !string.IsNullOrWhiteSpace(MemoryId);
    public bool IsUserFacing =>
        !string.Equals(Kind, "log", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Level, "error", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Level, "warning", StringComparison.OrdinalIgnoreCase) ||
        HasArtifact ||
        HasValidationStatus;

    public string BulletLabel =>
        string.Equals(Kind, "error", StringComparison.OrdinalIgnoreCase)
            ? "Error"
            : SectionLabel;

    public string BulletBodyText => HasArtifact
        ? $"{ArtifactLabel} -> {ArtifactPath}"
        : Text;

    public string BulletText => $"- {BulletLabel}: {BulletBodyText}";

    public string TranscriptText => HasArtifact
        ? BulletText
        : BulletText;

    partial void OnSectionChanged(ChatExecutionFeedSection value)
    {
        OnPropertyChanged(nameof(SectionLabel));
        OnPropertyChanged(nameof(BulletLabel));
        OnPropertyChanged(nameof(BulletText));
        OnPropertyChanged(nameof(TranscriptText));
    }

    partial void OnTimestampChanged(DateTimeOffset value)
    {
        OnPropertyChanged(nameof(DisplayTimestamp));
    }

    partial void OnLevelChanged(string value)
    {
        OnPropertyChanged(nameof(AccentBrushResourceKey));
        OnPropertyChanged(nameof(BulletLabel));
        OnPropertyChanged(nameof(BulletText));
        OnPropertyChanged(nameof(TranscriptText));
    }

    partial void OnArtifactPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasArtifact));
        OnPropertyChanged(nameof(IsUserFacing));
        OnPropertyChanged(nameof(BulletBodyText));
        OnPropertyChanged(nameof(BulletText));
        OnPropertyChanged(nameof(TranscriptText));
    }

    partial void OnArtifactLabelChanged(string value)
    {
        OnPropertyChanged(nameof(BulletBodyText));
        OnPropertyChanged(nameof(BulletText));
        OnPropertyChanged(nameof(TranscriptText));
    }

    partial void OnTextChanged(string value)
    {
        OnPropertyChanged(nameof(BulletBodyText));
        OnPropertyChanged(nameof(BulletText));
        OnPropertyChanged(nameof(TranscriptText));
    }

    partial void OnKindChanged(string value) => OnPropertyChanged(nameof(IsUserFacing));
    partial void OnValidationStatusChanged(string value)
    {
        OnPropertyChanged(nameof(HasValidationStatus));
        OnPropertyChanged(nameof(IsUserFacing));
    }
    partial void OnApprovalIdChanged(string value) => OnPropertyChanged(nameof(HasApprovalId));
    partial void OnToolNameChanged(string value) => OnPropertyChanged(nameof(HasToolName));
    partial void OnMemoryIdChanged(string value) => OnPropertyChanged(nameof(HasMemoryId));
}

public partial class ChatExecutionFeedChainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _chainId = string.Empty;

    [ObservableProperty]
    private string _parentChainId = string.Empty;

    [ObservableProperty]
    private string _stepId = string.Empty;

    [ObservableProperty]
    private string _phase = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _status = "running";

    [ObservableProperty]
    private string _level = "info";

    [ObservableProperty]
    private int _depth;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isRunning = true;

    [ObservableProperty]
    private string _streamingText = string.Empty;

    public ObservableCollection<ChatExecutionFeedEntryViewModel> Entries { get; } = new();
    public ObservableCollection<ChatExecutionFeedChainViewModel> Children { get; } = new();

    public bool HasEntries => Entries.Count > 0;
    public bool HasChildren => Children.Count > 0;
    public bool HasStreamingText => !string.IsNullOrWhiteSpace(StreamingText);
    public string HeaderText => string.IsNullOrWhiteSpace(Title)
        ? string.IsNullOrWhiteSpace(StepId) ? ChainId : $"{StepId} {Phase}".Trim()
        : Title;
    public string MetaText => string.IsNullOrWhiteSpace(Status) ? Phase : $"{Phase} • {Status}";
    public string AccentBrushResourceKey =>
        Level switch
        {
            "error" => "TokyoErrorBrush",
            "warning" => "TokyoWarningBrush",
            "success" => "TokyoSuccessBrush",
            _ => "TokyoAccentBrush"
        };
    public string TranscriptText => string.Join(
        Environment.NewLine,
        Entries.Select(entry => entry.TranscriptText)
            .Concat(Children.Select(child => child.TranscriptText))
            .Where(text => !string.IsNullOrWhiteSpace(text)));

    public void AddEntry(ChatExecutionFeedEntryViewModel entry)
    {
        Entries.Add(entry);
        OnPropertyChanged(nameof(HasEntries));
        OnPropertyChanged(nameof(TranscriptText));
    }

    public void AddChild(ChatExecutionFeedChainViewModel child)
    {
        Children.Add(child);
        OnPropertyChanged(nameof(HasChildren));
        OnPropertyChanged(nameof(TranscriptText));
    }

    public void AppendStreamingText(string text)
    {
        StreamingText = string.Concat(StreamingText, text);
    }

    public void Complete(string status, string level)
    {
        Status = status;
        Level = level;
        IsRunning = false;
        if (Depth > 0)
        {
            IsExpanded = false;
        }
    }

    public void ExpandAll()
    {
        IsExpanded = true;
        foreach (var child in Children)
        {
            child.ExpandAll();
        }
    }

    public void CollapseAll()
    {
        IsExpanded = false;
        foreach (var child in Children)
        {
            child.CollapseAll();
        }
    }

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(HeaderText));
    }

    partial void OnStepIdChanged(string value)
    {
        OnPropertyChanged(nameof(HeaderText));
    }

    partial void OnPhaseChanged(string value)
    {
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(MetaText));
    }

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(MetaText));
    }

    partial void OnLevelChanged(string value)
    {
        OnPropertyChanged(nameof(AccentBrushResourceKey));
    }

    partial void OnStreamingTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasStreamingText));
    }
}
