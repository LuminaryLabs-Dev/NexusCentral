using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusCentral.V3.Core.Execution;

public enum PrivacyLevel
{
    LocalOnly,
    TrustedPeer,
    ApprovedRemoteProvider
}

public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Canceled
}

public enum TargetKind
{
    LocalRuntime,
    NexusCentralPeer,
    OpenAI,
    Claude,
    OpenRouter,
    Nvidia,
    LmStudio
}

public enum ProviderKind
{
    OpenAI,
    Claude,
    OpenRouter,
    Nvidia,
    LmStudio
}

public sealed record JobRequest(string Goal, string WorkspaceId, string TaskType, PrivacyLevel Privacy);

public sealed record JobState(string JobId, JobStatus Status, string OwnerConversationId);

public sealed record RouteTarget(string Id, TargetKind Kind, bool Trusted, bool Healthy, int QueueDepth);

public sealed record RemoteQueueRoute(string JobId, string PeerId, string ScopedSessionToken);

public sealed record ProviderEndpoint(string Id, ProviderKind Kind, Uri BaseUrl, string AuthProfileId);

public enum ExecutionEventKind
{
    ToolUseStarted,
    ToolUseCompleted,
    ExecutionFailed
}

public sealed record ToolUseStep(
    string ToolName,
    string Status,
    string Summary,
    DateTimeOffset TimestampUtc);

public sealed record ExecutionEvent(
    string EventId,
    string ConversationId,
    ExecutionEventKind Kind,
    string ToolName,
    string Message,
    DateTimeOffset TimestampUtc);

public interface IExecutionEventStream
{
    event Action<ExecutionEvent>? EventPublished;

    IReadOnlyList<ExecutionEvent> GetEvents(string? conversationId = null);

    ExecutionEvent Publish(ExecutionEventKind kind, string conversationId, string toolName, string message);
}

public sealed class InMemoryExecutionEventStream : IExecutionEventStream
{
    private readonly object _gate = new();
    private readonly List<ExecutionEvent> _events = new();

    public event Action<ExecutionEvent>? EventPublished;

    public IReadOnlyList<ExecutionEvent> GetEvents(string? conversationId = null)
    {
        lock (_gate)
        {
            return _events
                .Where(entry => string.IsNullOrWhiteSpace(conversationId) || string.Equals(entry.ConversationId, conversationId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    public ExecutionEvent Publish(ExecutionEventKind kind, string conversationId, string toolName, string message)
    {
        var entry = new ExecutionEvent(
            Guid.NewGuid().ToString("N"),
            conversationId,
            kind,
            toolName,
            message,
            DateTimeOffset.UtcNow);

        lock (_gate)
        {
            _events.Add(entry);
        }

        EventPublished?.Invoke(entry);
        return entry;
    }
}
