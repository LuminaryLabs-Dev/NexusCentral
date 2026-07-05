using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V1.Features.Models;

namespace NexusCentral.V1.Integrations.Providers;

public sealed class OpenAiCompatibleModelProbe : IModelEndpointProbe
{
    private readonly HttpClient _httpClient;

    public OpenAiCompatibleModelProbe()
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(4) })
    {
    }

    public OpenAiCompatibleModelProbe(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ModelEndpointProbeResult> ProbeAsync(ModelEndpointDefinition endpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(CreateModelsUri(endpoint.BaseUrl), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ModelEndpointProbeResult(false, Array.Empty<string>(), $"Probe failed: HTTP {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return new ModelEndpointProbeResult(false, Array.Empty<string>(), "Probe failed: response did not include a data array.");
            }

            var modelIds = new List<string>();
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
                {
                    var id = idElement.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        modelIds.Add(id);
                    }
                }
            }

            return modelIds.Count == 0
                ? new ModelEndpointProbeResult(false, modelIds, "Probe failed: no model ids were returned.")
                : new ModelEndpointProbeResult(true, modelIds, $"Probe found {modelIds.Count} models.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return new ModelEndpointProbeResult(false, Array.Empty<string>(), $"Probe failed: {ex.Message}");
        }
    }

    private static Uri CreateModelsUri(string baseUrl)
    {
        return new Uri($"{baseUrl.TrimEnd('/')}/models", UriKind.Absolute);
    }
}
