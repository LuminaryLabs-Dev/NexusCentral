using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NexusCentral_Avalonia.ViewModels;

public abstract class ChatRenderBlockViewModel
{
}

public class ChatParagraphBlockViewModel : ChatRenderBlockViewModel
{
    public ChatParagraphBlockViewModel(IEnumerable<ChatParagraphLineViewModel> lines)
    {
        Lines = new ObservableCollection<ChatParagraphLineViewModel>(lines);
    }

    public ObservableCollection<ChatParagraphLineViewModel> Lines { get; }
}

public class ChatParagraphLineViewModel
{
    public ChatParagraphLineViewModel(IEnumerable<ChatInlineSegmentViewModel> segments)
    {
        Segments = new ObservableCollection<ChatInlineSegmentViewModel>(segments);
    }

    public ObservableCollection<ChatInlineSegmentViewModel> Segments { get; }
}

public class ChatHeadingBlockViewModel : ChatRenderBlockViewModel
{
    public ChatHeadingBlockViewModel(string text, int level)
    {
        Text = text;
        Level = level;
    }

    public string Text { get; }
    public int Level { get; }
}

public class ChatCodeBlockViewModel : ChatRenderBlockViewModel
{
    public ChatCodeBlockViewModel(string code, string language)
    {
        Code = code;
        Language = language;
    }

    public string Code { get; }
    public string Language { get; }
}

public class ChatListBlockViewModel : ChatRenderBlockViewModel
{
    public ChatListBlockViewModel(bool isOrdered, IEnumerable<ChatListItemViewModel> items)
    {
        IsOrdered = isOrdered;
        Items = new ObservableCollection<ChatListItemViewModel>(items);
    }

    public bool IsOrdered { get; }
    public ObservableCollection<ChatListItemViewModel> Items { get; }
}

public class ChatListItemViewModel
{
    public ChatListItemViewModel(int index, bool isOrdered, IEnumerable<ChatParagraphLineViewModel> lines)
    {
        Index = index;
        IsOrdered = isOrdered;
        Lines = new ObservableCollection<ChatParagraphLineViewModel>(lines);
    }

    public int Index { get; }
    public bool IsOrdered { get; }
    public ObservableCollection<ChatParagraphLineViewModel> Lines { get; }
    public string Marker => $"{Index}.";
}

public class ChatInlineSegmentViewModel
{
    public ChatInlineSegmentViewModel(string text, bool isCode = false)
    {
        Text = text;
        IsCode = isCode;
    }

    public string Text { get; }
    public bool IsCode { get; }
}

public static class ChatMessageFormatter
{
    public static IReadOnlyList<ChatRenderBlockViewModel> ParseBlocks(string? text)
    {
        var normalized = (text ?? string.Empty).Replace("\r", "");
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new[] { new ChatParagraphBlockViewModel(Array.Empty<ChatParagraphLineViewModel>()) };
        }

        var lines = normalized.Split('\n');
        var blocks = new List<ChatRenderBlockViewModel>();
        var paragraphLines = new List<string>();
        var codeLines = new List<string>();
        var listItems = new List<(bool Ordered, string Text)>();
        var inCodeBlock = false;
        var codeLanguage = string.Empty;

        void FlushParagraph()
        {
            if (paragraphLines.Count == 0)
            {
                return;
            }

            blocks.Add(new ChatParagraphBlockViewModel(paragraphLines.Select(line =>
                new ChatParagraphLineViewModel(ParseInlineSegments(line)))));
            paragraphLines.Clear();
        }

        void FlushList()
        {
            if (listItems.Count == 0)
            {
                return;
            }

            var isOrdered = listItems[0].Ordered;
            var items = listItems
                .Select((item, index) => new ChatListItemViewModel(index + 1, isOrdered, new[]
                {
                    new ChatParagraphLineViewModel(ParseInlineSegments(item.Text))
                }))
                .ToList();
            blocks.Add(new ChatListBlockViewModel(isOrdered, items));
            listItems.Clear();
        }

        void FlushCode()
        {
            if (codeLines.Count == 0)
            {
                return;
            }

            blocks.Add(new ChatCodeBlockViewModel(string.Join("\n", codeLines), codeLanguage));
            codeLines.Clear();
            codeLanguage = string.Empty;
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine ?? string.Empty;
            var trimmed = line.TrimEnd();

            if (trimmed.TrimStart().StartsWith("```"))
            {
                FlushParagraph();
                FlushList();

                if (inCodeBlock)
                {
                    FlushCode();
                    inCodeBlock = false;
                }
                else
                {
                    inCodeBlock = true;
                    codeLanguage = trimmed.Trim().TrimStart('`').Trim();
                }

                continue;
            }

            if (inCodeBlock)
            {
                codeLines.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                FlushParagraph();
                FlushList();
                continue;
            }

            if (TryParseHeading(trimmed, out var level, out var headingText))
            {
                FlushParagraph();
                FlushList();
                blocks.Add(new ChatHeadingBlockViewModel(headingText, level));
                continue;
            }

            if (TryParseListItem(trimmed, out var isOrdered, out var itemText))
            {
                FlushParagraph();
                if (listItems.Count > 0 && listItems[0].Ordered != isOrdered)
                {
                    FlushList();
                }

                listItems.Add((isOrdered, itemText));
                continue;
            }

            if (trimmed.Contains('|') && trimmed.Count(character => character == '|') >= 2)
            {
                FlushParagraph();
                FlushList();
                blocks.Add(new ChatCodeBlockViewModel(trimmed, "table"));
                continue;
            }

            paragraphLines.Add(trimmed);
        }

        FlushParagraph();
        FlushList();
        FlushCode();

        return blocks.Count == 0
            ? new[] { new ChatParagraphBlockViewModel(new[]
            {
                new ChatParagraphLineViewModel(ParseInlineSegments(normalized.Trim()))
            }) }
            : blocks;
    }

    private static IEnumerable<ChatInlineSegmentViewModel> ParseInlineSegments(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new[] { new ChatInlineSegmentViewModel(string.Empty) };
        }

        var segments = new List<ChatInlineSegmentViewModel>();
        var parts = text.Split('`');
        for (var index = 0; index < parts.Length; index++)
        {
            if (string.IsNullOrEmpty(parts[index]))
            {
                continue;
            }

            segments.Add(new ChatInlineSegmentViewModel(parts[index], index % 2 == 1));
        }

        return segments.Count == 0
            ? new[] { new ChatInlineSegmentViewModel(text) }
            : segments;
    }

    private static bool TryParseHeading(string text, out int level, out string headingText)
    {
        level = 0;
        headingText = string.Empty;
        var trimmed = text.TrimStart();
        if (!trimmed.StartsWith("#"))
        {
            return false;
        }

        var count = 0;
        while (count < trimmed.Length && trimmed[count] == '#')
        {
            count++;
        }

        if (count == 0 || count > 4 || trimmed.Length <= count || trimmed[count] != ' ')
        {
            return false;
        }

        level = count;
        headingText = trimmed[(count + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(headingText);
    }

    private static bool TryParseListItem(string text, out bool isOrdered, out string itemText)
    {
        itemText = string.Empty;
        isOrdered = false;

        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
        {
            itemText = trimmed[2..].Trim();
            return true;
        }

        if (trimmed.StartsWith("- [ ] "))
        {
            itemText = $"☐ {trimmed[6..].Trim()}";
            return true;
        }

        if (trimmed.StartsWith("- [x] ", System.StringComparison.OrdinalIgnoreCase))
        {
            itemText = $"☑ {trimmed[6..].Trim()}";
            return true;
        }

        var dotIndex = trimmed.IndexOf(". ", System.StringComparison.Ordinal);
        if (dotIndex > 0 && int.TryParse(trimmed[..dotIndex], out _))
        {
            isOrdered = true;
            itemText = trimmed[(dotIndex + 2)..].Trim();
            return true;
        }

        return false;
    }
}
