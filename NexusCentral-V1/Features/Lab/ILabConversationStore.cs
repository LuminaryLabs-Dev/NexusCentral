using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Features.Lab;

public interface ILabConversationStore
{
    Task<LabConversation?> LoadAsync(string conversationId, CancellationToken cancellationToken = default);

    Task SaveAsync(LabConversation conversation, CancellationToken cancellationToken = default);
}
