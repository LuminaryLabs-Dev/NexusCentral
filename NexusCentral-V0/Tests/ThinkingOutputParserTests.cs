using NexusCentral_Avalonia.Services;
using Xunit;

namespace NexusCentral_Triceratops.Tests;

public sealed class ThinkingOutputParserTests
{
    [Fact]
    public void Separate_PreservesAllThinkingBlocks_AndFinalAnswer()
    {
        var separated = ThinkingOutputParser.Separate(
            "<think>Inspect the request.</think>\nVisible text.\n<think>Check the available runtime path.</think>\nFinal answer.",
            "Final answer.");

        Assert.Equal("Inspect the request.\n\nCheck the available runtime path.", separated.ThinkingText);
        Assert.Equal("Final answer.", separated.FinalText);
    }

    [Fact]
    public void Separate_FallsBackToAnswerOutsideThinkBlocks_WhenOutputLeaksReasoning()
    {
        var separated = ThinkingOutputParser.Separate(
            "<think>Review the prompt.</think>\nAnswer: Ship the direct response.",
            "<think>Review the prompt.</think>");

        Assert.Equal("Review the prompt.", separated.ThinkingText);
        Assert.Equal("Ship the direct response.", separated.FinalText);
    }

    [Fact]
    public void BuildReasoningSummaryText_FormatsConciseBulletList()
    {
        var summary = ThinkingOutputParser.BuildReasoningSummaryText(
            "Okay, let me inspect the request.\n\n1. Check the runtime bundle version.\n2. Confirm the chat selector fallback.");

        Assert.Equal(
            "- Inspect the request.\n- Check the runtime bundle version.\n- Confirm the chat selector fallback.",
            summary);
    }
}
