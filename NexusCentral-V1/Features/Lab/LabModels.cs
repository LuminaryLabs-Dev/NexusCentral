using System;
using System.Collections.Generic;
using NexusCentral.V1.Core.Execution;

namespace NexusCentral.V1.Features.Lab;

public enum ChatMessageRole
{
    User,
    Assistant,
    System
}

public sealed record ChatAttachment(
    string AttachmentId,
    string FileName,
    string SourcePath,
    long SizeBytes,
    DateTimeOffset AddedAtUtc);

public sealed record ChatMessage(
    string MessageId,
    ChatMessageRole Role,
    string Text,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<ToolUseStep> ToolSteps,
    IReadOnlyList<ChatAttachment> Attachments);

public sealed record LabConversation(
    string ConversationId,
    string WorkspaceId,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ChatMessage> Messages);
