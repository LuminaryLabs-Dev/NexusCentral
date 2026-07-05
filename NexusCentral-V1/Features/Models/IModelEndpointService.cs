using System;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Features.Models;

public interface IModelEndpointProbe
{
    Task<ModelEndpointProbeResult> ProbeAsync(ModelEndpointDefinition endpoint, CancellationToken cancellationToken = default);
}

public interface IModelEndpointService
{
    event Action<ModelEndpointSnapshot>? StateChanged;

    ModelEndpointSnapshot GetSnapshot();

    Task<ModelEndpointDefinition> AddOrUpdateEndpointAsync(ModelEndpointCreateRequest request, CancellationToken cancellationToken = default);

    Task<bool> SelectEndpointAsync(string endpointId, CancellationToken cancellationToken = default);

    Task<ModelEndpointDefinition?> ProbeEndpointAsync(string endpointId, CancellationToken cancellationToken = default);

    Task<bool> RemoveEndpointAsync(string endpointId, CancellationToken cancellationToken = default);
}
