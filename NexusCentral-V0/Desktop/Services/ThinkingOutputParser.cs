using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusCentral_Avalonia.Services;

public static class ThinkingOutputParser
{
    public static ParsedThinkingOutput Separate(string rawOutput, string output)
    {
        var thinkingText = ExtractThinkingText(rawOutput);
        var normalizedOutput = NormalizeFinalOutput(output);
        var answerOutsideThink = ExtractAnswerOutsideThinkBlocks(rawOutput);

        if (!string.IsNullOrWhiteSpace(thinkingText))
        {
            if (string.IsNullOrWhiteSpace(normalizedOutput)
                || string.Equals(NormalizeCompact(normalizedOutput), NormalizeCompact(thinkingText), StringComparison.OrdinalIgnoreCase)
                || LooksLikeThinking(normalizedOutput))
            {
                normalizedOutput = NormalizeFinalOutput(answerOutsideThink);
            }

            if (string.IsNullOrWhiteSpace(normalizedOutput))
            {
                normalizedOutput = ExtractFinalAnswerFromThinking(thinkingText);
            }
        }
        else if (string.IsNullOrWhiteSpace(normalizedOutput))
        {
            normalizedOutput = NormalizeFinalOutput(rawOutput);
        }

        return new ParsedThinkingOutput(thinkingText, normalizedOutput);
    }

    public static string BuildReasoningSummaryText(string thinkingText)
    {
        var items = BuildReasoningSummaryItems(thinkingText);
        return items.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, items.Select(item => $"- {item}"));
    }

    public static bool NeedsFinalAnswerRetry(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return true;
        }

        var normalized = output.Trim();
        var squashed = NormalizeCompact(normalized);
        return normalized.StartsWith("<think>", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Instruction:", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Answer the current request", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Recent conversation:", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Relevant memory:", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("markdown markdown", StringComparison.OrdinalIgnoreCase)
            || squashed.StartsWith("okay,let'sseewhattheuser")
            || squashed.StartsWith("okay,letmesee")
            || squashed.Contains("theuserisasking")
            || squashed.Contains("followingspecificinstructions")
            || squashed.Contains("internalthought");
    }

    public static string NormalizeFinalOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return string.Empty;
        }

        var normalized = output.Replace("\r", string.Empty).Trim();
        if (normalized.Contains("</think>", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[(normalized.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase) + "</think>".Length)..].Trim();
        }

        normalized = normalized
            .Replace("<think>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("</think>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Answer:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return normalized;
    }

    public static string GetRawOutput(string rawOutput, string output)
    {
        if (!string.IsNullOrWhiteSpace(rawOutput))
        {
            return rawOutput;
        }

        return output ?? string.Empty;
    }

    private static string ExtractThinkingText(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return string.Empty;
        }

        var normalized = rawOutput.Replace("\r", string.Empty).Trim();
        var thinkBlocks = ExtractThinkBlocks(normalized);
        if (thinkBlocks.Count > 0)
        {
            return string.Join($"{Environment.NewLine}{Environment.NewLine}", thinkBlocks);
        }

        return LooksLikeThinking(normalized)
            ? normalized
            : string.Empty;
    }

    private static string ExtractAnswerOutsideThinkBlocks(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
        {
            return string.Empty;
        }

        if (!rawOutput.Contains("<think>", StringComparison.OrdinalIgnoreCase))
        {
            return rawOutput.Trim();
        }

        var source = rawOutput.Replace("\r", string.Empty);
        var answerParts = new List<string>();
        var index = 0;

        while (index < source.Length)
        {
            var thinkStart = source.IndexOf("<think>", index, StringComparison.OrdinalIgnoreCase);
            if (thinkStart < 0)
            {
                answerParts.Add(source[index..]);
                break;
            }

            answerParts.Add(source[index..thinkStart]);

            var afterStart = thinkStart + "<think>".Length;
            var thinkEnd = source.IndexOf("</think>", afterStart, StringComparison.OrdinalIgnoreCase);
            if (thinkEnd < 0)
            {
                break;
            }

            index = thinkEnd + "</think>".Length;
        }

        return string.Concat(answerParts).Trim();
    }

    private static string ExtractFinalAnswerFromThinking(string thinkingText)
    {
        if (string.IsNullOrWhiteSpace(thinkingText))
        {
            return string.Empty;
        }

        var paragraphs = thinkingText
            .Replace("\r", string.Empty)
            .Split($"{Environment.NewLine}{Environment.NewLine}", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .ToList();

        for (var index = paragraphs.Count - 1; index >= 0; index--)
        {
            var paragraph = paragraphs[index].Trim();
            if (!LooksLikeThinking(paragraph))
            {
                return NormalizeFinalOutput(paragraph);
            }
        }

        return string.Empty;
    }

    private static bool LooksLikeThinking(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();
        var compact = NormalizeCompact(normalized);
        return normalized.StartsWith("<think>", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Okay", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Let me", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Wait", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Hmm", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Relevant memory:", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Recent conversation:", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Current request:", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Instruction:", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("theuser")
            || compact.Contains("relevantmemory")
            || compact.Contains("recentconversation")
            || compact.Contains("currentrequest")
            || compact.Contains("followingspecificinstructions")
            || compact.Contains("internalreasoning")
            || compact.Contains("let'stacklethis")
            || compact.Contains("letmesee")
            || compact.Contains("maybeineedto")
            || compact.Contains("perhaps");
    }

    private static string NormalizeCompact(string text) =>
        string.Concat(text.Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant();

    private static List<string> ExtractThinkBlocks(string text)
    {
        var blocks = new List<string>();
        var index = 0;

        while (index < text.Length)
        {
            var thinkStart = text.IndexOf("<think>", index, StringComparison.OrdinalIgnoreCase);
            if (thinkStart < 0)
            {
                break;
            }

            var afterStart = thinkStart + "<think>".Length;
            var thinkEnd = text.IndexOf("</think>", afterStart, StringComparison.OrdinalIgnoreCase);
            var block = (thinkEnd >= 0 ? text[afterStart..thinkEnd] : text[afterStart..]).Trim();
            if (!string.IsNullOrWhiteSpace(block))
            {
                blocks.Add(block);
            }

            index = thinkEnd >= 0
                ? thinkEnd + "</think>".Length
                : text.Length;
        }

        return blocks;
    }

    private static IReadOnlyList<string> BuildReasoningSummaryItems(string thinkingText)
    {
        if (string.IsNullOrWhiteSpace(thinkingText))
        {
            return Array.Empty<string>();
        }

        var normalized = thinkingText
            .Replace("\r", string.Empty)
            .Replace("<think>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("</think>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<string>();
        }

        var summaryItems = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paragraphs = normalized
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(paragraph => !string.IsNullOrWhiteSpace(paragraph))
            .ToList();

        foreach (var paragraph in paragraphs)
        {
            var addedLineItem = false;
            foreach (var line in paragraph.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var countBefore = summaryItems.Count;
                TryAddSummaryItem(line, summaryItems, seen);
                addedLineItem = addedLineItem || summaryItems.Count > countBefore;
                if (summaryItems.Count >= 6)
                {
                    return summaryItems;
                }
            }

            if (summaryItems.Count >= 6)
            {
                return summaryItems;
            }

            if (!addedLineItem)
            {
                TryAddSummaryItem(paragraph, summaryItems, seen);
            }

            if (summaryItems.Count >= 6)
            {
                return summaryItems;
            }
        }

        return summaryItems;
    }

    private static void TryAddSummaryItem(string source, ICollection<string> summaryItems, ISet<string> seen)
    {
        var candidate = NormalizeSummaryItem(source);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        var key = NormalizeCompact(candidate);
        if (!seen.Add(key))
        {
            return;
        }

        summaryItems.Add(candidate);
    }

    private static string NormalizeSummaryItem(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var normalized = CollapseWhitespace(source.Trim());
        normalized = TrimSummaryMarker(normalized);
        normalized = TrimReasoningPrefix(normalized);
        normalized = CollapseWhitespace(normalized);

        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length < 12 ||
            LooksLikeSummaryNoise(normalized) ||
            LooksLikeThinking(normalized) && normalized.Length < 24)
        {
            return string.Empty;
        }

        return ShortenSummaryItem(UppercaseFirst(normalized));
    }

    private static string TrimSummaryMarker(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("- ", StringComparison.Ordinal) ||
            trimmed.StartsWith("* ", StringComparison.Ordinal) ||
            trimmed.StartsWith("• ", StringComparison.Ordinal))
        {
            return trimmed[2..].Trim();
        }

        var markerLength = 0;
        while (markerLength < trimmed.Length && char.IsDigit(trimmed[markerLength]))
        {
            markerLength++;
        }

        if (markerLength > 0 &&
            markerLength + 1 < trimmed.Length &&
            (trimmed[markerLength] == '.' || trimmed[markerLength] == ')') &&
            trimmed[markerLength + 1] == ' ')
        {
            return trimmed[(markerLength + 2)..].Trim();
        }

        return trimmed;
    }

    private static string TrimReasoningPrefix(string text)
    {
        var prefixes = new[]
        {
            "okay, let me ",
            "okay let me ",
            "ok, let me ",
            "ok let me ",
            "let me ",
            "i should ",
            "we should ",
            "i need to ",
            "we need to ",
            "need to ",
            "first, ",
            "first ",
            "next, ",
            "next ",
            "then, ",
            "then ",
            "finally, ",
            "finally ",
        };

        var normalized = text.Trim();
        var updated = true;
        while (updated)
        {
            updated = false;
            foreach (var prefix in prefixes)
            {
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized[prefix.Length..].Trim();
                    updated = true;
                }
            }
        }

        return normalized;
    }

    private static bool LooksLikeSummaryNoise(string text)
    {
        var compact = NormalizeCompact(text);
        return compact.StartsWith("answer:", StringComparison.OrdinalIgnoreCase) ||
               compact.StartsWith("finalanswer:", StringComparison.OrdinalIgnoreCase) ||
               compact.Contains("recentconversation") ||
               compact.Contains("relevantmemory") ||
               compact.Contains("currentrequest");
    }

    private static string CollapseWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var buffer = new char[text.Length];
        var length = 0;
        var previousWhitespace = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                if (previousWhitespace)
                {
                    continue;
                }

                buffer[length++] = ' ';
                previousWhitespace = true;
                continue;
            }

            buffer[length++] = character;
            previousWhitespace = false;
        }

        return new string(buffer, 0, length).Trim();
    }

    private static string ShortenSummaryItem(string text)
    {
        const int maxLength = 140;
        if (text.Length <= maxLength)
        {
            return text;
        }

        var candidate = text[..maxLength];
        var boundary = candidate.LastIndexOfAny(new[] { '.', ';', ':', ',', ' ' });
        if (boundary >= 48)
        {
            candidate = candidate[..boundary];
        }

        return $"{candidate.TrimEnd()}...";
    }

    private static string UppercaseFirst(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(text[0]) + text[1..];
    }
}

public sealed record ParsedThinkingOutput(string ThinkingText, string FinalText);
