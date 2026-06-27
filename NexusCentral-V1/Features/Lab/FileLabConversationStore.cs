using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V3.Core.Config;
using NexusCentral.V3.Core.State;

namespace NexusCentral.V3.Features.Lab;

public sealed class FileLabConversationStore : ILabConversationStore
{
    private readonly AppPathSettings _paths;
    private readonly JsonFileStore _json;

    public FileLabConversationStore(AppPathSettings paths, JsonFileStore json)
    {
        _paths = paths;
        _json = json;
    }

    public Task<LabConversation?> LoadAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        return _json.ReadAsync<LabConversation>(GetConversationPath(conversationId), cancellationToken);
    }

    public Task SaveAsync(LabConversation conversation, CancellationToken cancellationToken = default)
    {
        var normalized = conversation with { UpdatedAtUtc = DateTimeOffset.UtcNow };
        return _json.WriteAsync(GetConversationPath(conversation.ConversationId), normalized, cancellationToken);
    }

    private string GetConversationPath(string conversationId)
    {
        var safeId = string.Join("_", conversationId.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return Path.Combine(_paths.ConversationsDirectory, $"{safeId}.json");
    }
}
