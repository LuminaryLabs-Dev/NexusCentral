using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Features.Harnesses;

public interface IHarnessOrchestrationSmokeService
{
    HarnessOrchestrationSmokeResult? LastResult { get; }

    Task<HarnessOrchestrationSmokeResult> SmokeAsync(HarnessOrchestrationSmokeRequest request, CancellationToken cancellationToken = default);
}
