using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Features.Harnesses;

public interface IHostedHarnessTurnRunner
{
    Task<HostedHarnessTurnResult> RunTurnAsync(HarnessDefinition harness, HostedHarnessTurnRequest request, CancellationToken cancellationToken = default);
}
