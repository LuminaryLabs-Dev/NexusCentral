using System;
using System.Linq;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class DirectChatQualityGuard
{
    public DirectChatQualityDecision Evaluate(RuntimeInvocationResult result, string streamedFallbackText, DirectChatExecutionPlan plan, int attemptNumber)
    {
        var candidateOutput = !string.IsNullOrWhiteSpace(result.Output)
            ? result.Output
            : streamedFallbackText;
        var separated = ThinkingOutputParser.Separate(result.RawOutput, candidateOutput);
        var finalText = ThinkingOutputParser.NormalizeFinalOutput(separated.FinalText);
        var rawOutput = ThinkingOutputParser.GetRawOutput(result.RawOutput, candidateOutput);
        var violatesExactReply = TryExtractExactReplyRequirement(result.Prompt, out var expectedReply) &&
            !string.Equals(finalText.Trim(), expectedReply, StringComparison.Ordinal);
        var highNoiseGuardHit = string.Equals(plan.PolicyName, "thinking-guarded", StringComparison.OrdinalIgnoreCase) &&
            LooksLikeHighNoiseOutput(finalText);

        if (!result.Ok)
        {
            return new DirectChatQualityDecision(
                Outcome: "fail_visible",
                QualityState: "runtime_error",
                FinalText: string.Empty,
                ThinkingText: separated.ThinkingText,
                VisibleError: string.IsNullOrWhiteSpace(result.Error) ? "Runtime failed before producing a usable answer." : result.Error,
                Diagnostic: $"runtime_error:{result.ErrorCode}");
        }

        var needsRetry = string.IsNullOrWhiteSpace(finalText) ||
            ThinkingOutputParser.NeedsFinalAnswerRetry(finalText) ||
            LooksLikeDegenerateOutput(finalText) ||
            highNoiseGuardHit ||
            violatesExactReply ||
            (string.Equals(result.FinishReason, "max_tokens", StringComparison.OrdinalIgnoreCase) && LooksLikeLowConfidenceTerminal(finalText, rawOutput));

        if (needsRetry)
        {
            if (plan.AllowsRetry && attemptNumber == 0)
            {
                return new DirectChatQualityDecision(
                    Outcome: "retry_with_tighter_prompt",
                    QualityState: "retry_requested",
                    FinalText: string.Empty,
                    ThinkingText: separated.ThinkingText,
                    VisibleError: string.Empty,
                    Diagnostic: BuildDiagnostic(result, finalText, rawOutput));
            }

            return new DirectChatQualityDecision(
                Outcome: "fail_visible",
                QualityState: "unusable_output",
                FinalText: string.Empty,
                ThinkingText: separated.ThinkingText,
                VisibleError: "Model output was unusable; try a different quantization or mode.",
                Diagnostic: BuildDiagnostic(result, finalText, rawOutput));
        }

        return new DirectChatQualityDecision(
            Outcome: "accept",
            QualityState: attemptNumber == 0 ? "accepted_clean" : "accepted_after_retry",
            FinalText: finalText,
            ThinkingText: separated.ThinkingText,
            VisibleError: string.Empty,
            Diagnostic: $"accepted:{result.FinishReason}");
    }

    private static string BuildDiagnostic(RuntimeInvocationResult result, string finalText, string rawOutput)
    {
        var reason = string.IsNullOrWhiteSpace(result.FinishReason) ? "unknown" : result.FinishReason;
        var preview = string.IsNullOrWhiteSpace(finalText)
            ? BuildCompactPreview(rawOutput)
            : BuildCompactPreview(finalText);
        return $"finish_reason={reason}; preview={preview}";
    }

    private static bool LooksLikeLowConfidenceTerminal(string finalText, string rawOutput) =>
        string.IsNullOrWhiteSpace(finalText) ||
        LooksLikeDegenerateOutput(finalText) ||
        (BuildAlnumRatio(rawOutput) < 0.12 && rawOutput.Trim().Length >= 12);

    private static bool LooksLikeHighNoiseOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();
        var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 10)
        {
            return false;
        }

        var alphaRatio = BuildAlphaRatio(trimmed);
        var tokensWithoutLetters = tokens.Count(token => !token.Any(char.IsLetter));
        var symbolHeavyTokens = tokens.Count(token =>
        {
            var nonWhitespace = token.Count(character => !char.IsWhiteSpace(character));
            if (nonWhitespace == 0)
            {
                return false;
            }

            var symbolCount = token.Count(character => !char.IsLetterOrDigit(character));
            return symbolCount >= Math.Max(1, (int)Math.Ceiling(nonWhitespace * 0.45));
        });
        var shortWeirdLines = trimmed
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Count(line => line.Length <= 24 && BuildAlphaRatio(line) < 0.45);

        return alphaRatio < 0.55 &&
            (tokensWithoutLetters >= tokens.Length / 3 ||
             symbolHeavyTokens >= tokens.Length / 2 ||
             shortWeirdLines >= 4);
    }

    private static bool LooksLikeDegenerateOutput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();
        if (trimmed.Length >= 12 && BuildAlnumRatio(trimmed) < 0.15)
        {
            return true;
        }

        var lines = trimmed
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length >= 4 && lines.All(IsMostlySymbolLine))
        {
            return true;
        }

        var distinctNonWhitespace = trimmed
            .Where(character => !char.IsWhiteSpace(character))
            .Select(char.ToLowerInvariant)
            .Distinct()
            .Count();
        if (trimmed.Length >= 16 && distinctNonWhitespace <= 3 && BuildAlnumRatio(trimmed) < 0.25)
        {
            return true;
        }

        return false;
    }

    private static bool IsMostlySymbolLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var nonWhitespace = line.Count(character => !char.IsWhiteSpace(character));
        if (nonWhitespace == 0)
        {
            return false;
        }

        var symbolCount = line.Count(character => !char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character));
        return symbolCount >= Math.Max(1, (int)Math.Ceiling(nonWhitespace * 0.7));
    }

    private static double BuildAlnumRatio(string text)
    {
        var nonWhitespace = text.Count(character => !char.IsWhiteSpace(character));
        if (nonWhitespace == 0)
        {
            return 0;
        }

        var alphaNumeric = text.Count(char.IsLetterOrDigit);
        return alphaNumeric / (double)nonWhitespace;
    }

    private static double BuildAlphaRatio(string text)
    {
        var nonWhitespace = text.Count(character => !char.IsWhiteSpace(character));
        if (nonWhitespace == 0)
        {
            return 0;
        }

        var alpha = text.Count(char.IsLetter);
        return alpha / (double)nonWhitespace;
    }

    private static bool TryExtractExactReplyRequirement(string prompt, out string expectedReply)
    {
        expectedReply = string.Empty;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return false;
        }

        var trimmed = prompt.Trim();
        const string replyPrefix = "reply with exactly ";
        const string respondPrefix = "respond with exactly ";
        var comparison = StringComparison.OrdinalIgnoreCase;
        var prefixLength = trimmed.StartsWith(replyPrefix, comparison)
            ? replyPrefix.Length
            : trimmed.StartsWith(respondPrefix, comparison)
                ? respondPrefix.Length
                : -1;
        if (prefixLength < 0)
        {
            return false;
        }

        var remainder = trimmed[prefixLength..].Trim().TrimEnd('.').Trim();
        if (string.IsNullOrWhiteSpace(remainder) || remainder.Contains('\n') || remainder.Length > 80)
        {
            return false;
        }

        if ((remainder.StartsWith("\"", StringComparison.Ordinal) && remainder.EndsWith("\"", StringComparison.Ordinal)) ||
            (remainder.StartsWith("'", StringComparison.Ordinal) && remainder.EndsWith("'", StringComparison.Ordinal)))
        {
            remainder = remainder[1..^1].Trim();
        }

        if (string.IsNullOrWhiteSpace(remainder))
        {
            return false;
        }

        expectedReply = remainder;
        return true;
    }

    private static string BuildCompactPreview(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "<empty>";
        }

        var normalized = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 120 ? normalized : normalized[..120];
    }
}

public sealed record DirectChatQualityDecision(
    string Outcome,
    string QualityState,
    string FinalText,
    string ThinkingText,
    string VisibleError,
    string Diagnostic);
