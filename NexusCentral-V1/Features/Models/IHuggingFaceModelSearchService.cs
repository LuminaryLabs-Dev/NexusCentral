using System;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Features.Models;

public interface IHuggingFaceModelSearchService
{
    event Action<HuggingFaceModelSearchSnapshot>? StateChanged;

    HuggingFaceModelSearchSnapshot GetSnapshot();

    Task<HuggingFaceModelSearchSnapshot> SearchAsync(HuggingFaceModelSearchRequest request, CancellationToken cancellationToken = default);
}
