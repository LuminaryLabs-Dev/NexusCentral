using System;
using System.Linq;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class DirectChatExecutionPolicy
{
    private readonly ModelLibraryService _library;

    public DirectChatExecutionPolicy(ModelLibraryService library)
    {
        _library = library;
    }

    public DirectChatExecutionPlan Resolve(string? selectedInstanceId, string? selectedModelId, int maxTokens)
    {
        var resolved = _library.ResolveInstanceForEndpoint("direct_chat", selectedInstanceId, selectedModelId);
        if (resolved is null)
        {
            return new DirectChatExecutionPlan(
                IsRunnable: false,
                FailureReason: "Load and activate a model in Models before using direct chat.",
                ResolvedInstanceId: string.Empty,
                ResolvedModelId: string.Empty,
                ResolvedModelLabel: string.Empty,
                ResolvedBackend: string.Empty,
                ResolvedExecutionMode: string.Empty,
                PolicyName: "unresolved",
                InitialMaxTokens: Math.Clamp(maxTokens, 1, 5000),
                RetryMaxTokens: Math.Clamp(Math.Min(maxTokens, 256), 1, 5000),
                AllowsRetry: false);
        }

        if (!resolved.IsActive ||
            !string.Equals(resolved.LoadState, "resident", StringComparison.OrdinalIgnoreCase) ||
            !resolved.Capabilities.Any(capability => string.Equals(capability, "text", StringComparison.OrdinalIgnoreCase)))
        {
            return new DirectChatExecutionPlan(
                IsRunnable: false,
                FailureReason: $"Loaded instance '{resolved.DisplayName}' is not ready for direct chat.",
                ResolvedInstanceId: resolved.InstanceId,
                ResolvedModelId: resolved.ModelId,
                ResolvedModelLabel: resolved.DisplayName,
                ResolvedBackend: resolved.RuntimeBackend,
                ResolvedExecutionMode: string.Empty,
                PolicyName: "not-ready",
                InitialMaxTokens: Math.Clamp(maxTokens, 1, 5000),
                RetryMaxTokens: Math.Clamp(Math.Min(maxTokens, 256), 1, 5000),
                AllowsRetry: false);
        }

        var executionMode = ResolveExecutionMode(resolved);
        var initialMaxTokens = Math.Clamp(maxTokens, 1, 5000);
        var retryMaxTokens = Math.Clamp(Math.Min(initialMaxTokens, 256), 64, 5000);
        var isDefensiveModel = string.Equals(executionMode, "thinking", StringComparison.OrdinalIgnoreCase);

        return new DirectChatExecutionPlan(
            IsRunnable: true,
            FailureReason: string.Empty,
            ResolvedInstanceId: resolved.InstanceId,
            ResolvedModelId: resolved.ModelId,
            ResolvedModelLabel: resolved.DisplayName,
            ResolvedBackend: resolved.RuntimeBackend,
            ResolvedExecutionMode: executionMode,
            PolicyName: isDefensiveModel ? "thinking-guarded" : "direct-instruct",
            InitialMaxTokens: initialMaxTokens,
            RetryMaxTokens: retryMaxTokens,
            AllowsRetry: true);
    }

    public string BuildRetryPrompt(string userText) =>
        "Answer the user's request directly with one clean final answer. " +
        "Do not show reasoning, internal notes, scratch work, symbols-only output, or prompt restatement." +
        Environment.NewLine +
        Environment.NewLine +
        "User request:" +
        Environment.NewLine +
        userText.Trim();

    private static string ResolveExecutionMode(LoadedInstanceSnapshot instance)
    {
        if (string.Equals(instance.PrimaryMode, "thinking", StringComparison.OrdinalIgnoreCase))
        {
            return "thinking";
        }

        var identity = string.Join(" ", instance.DisplayName, instance.ModelId, instance.Family).ToLowerInvariant();
        if (identity.Contains("qwen", StringComparison.OrdinalIgnoreCase) ||
            identity.Contains("thinking", StringComparison.OrdinalIgnoreCase))
        {
            return "thinking";
        }

        return "instruct";
    }
}

public sealed record DirectChatExecutionPlan(
    bool IsRunnable,
    string FailureReason,
    string ResolvedInstanceId,
    string ResolvedModelId,
    string ResolvedModelLabel,
    string ResolvedBackend,
    string ResolvedExecutionMode,
    string PolicyName,
    int InitialMaxTokens,
    int RetryMaxTokens,
    bool AllowsRetry);
