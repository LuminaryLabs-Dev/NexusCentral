using System;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Features.Router;

public interface IRouterService
{
    event Action<RouterSnapshot>? StateChanged;

    RouterSnapshot GetSnapshot();

    Task<bool> SelectRouteAsync(string routeId, CancellationToken cancellationToken = default);

    Task<RouterSnapshot> SelectBestAvailableRouteAsync(CancellationToken cancellationToken = default);

    Task<RouterSnapshot> RefreshAsync(CancellationToken cancellationToken = default);

    Task<RouterSmokeResult> SmokeSelectedRouteAsync(string prompt, CancellationToken cancellationToken = default);
}
