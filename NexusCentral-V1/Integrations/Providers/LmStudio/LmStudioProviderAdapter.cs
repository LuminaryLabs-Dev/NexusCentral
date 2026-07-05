using System;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V1.Core.Execution;
using NexusCentral.V1.Features.Models;

namespace NexusCentral.V1.Integrations.Providers.LmStudio;

public sealed class LmStudioProviderAdapter : IModelEndpointProbe
{
    private readonly IModelEndpointProbe _openAiCompatibleProbe;

    public LmStudioProviderAdapter(IModelEndpointProbe openAiCompatibleProbe)
    {
        _openAiCompatibleProbe = openAiCompatibleProbe;
    }

    public Task<ModelEndpointProbeResult> ProbeAsync(ModelEndpointDefinition endpoint, CancellationToken cancellationToken = default)
    {
        if (endpoint.ProviderKind is ProviderKind.LmStudio or ProviderKind.OpenAiCompatible)
        {
            return _openAiCompatibleProbe.ProbeAsync(endpoint, cancellationToken);
        }

        return Task.FromResult(new ModelEndpointProbeResult(
            false,
            Array.Empty<string>(),
            $"Provider kind '{endpoint.ProviderKind}' is not supported by the LM Studio adapter."));
    }
}
