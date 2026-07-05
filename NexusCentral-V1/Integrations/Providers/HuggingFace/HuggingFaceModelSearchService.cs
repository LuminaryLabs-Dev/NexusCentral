using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V1.Features.Models;

namespace NexusCentral.V1.Integrations.Providers.HuggingFace;

public sealed class HuggingFaceModelSearchService : IHuggingFaceModelSearchService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly object _gate = new();
    private HuggingFaceModelSearchSnapshot _snapshot = new(
        string.Empty,
        HuggingFaceSearchState.Idle,
        "Enter a Hugging Face model name to search.",
        null,
        Array.Empty<HuggingFaceModelSearchEntry>());

    public HuggingFaceModelSearchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public event Action<HuggingFaceModelSearchSnapshot>? StateChanged;

    public HuggingFaceModelSearchSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    public async Task<HuggingFaceModelSearchSnapshot> SearchAsync(HuggingFaceModelSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = request.Query.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return UpdateSnapshot(new HuggingFaceModelSearchSnapshot(
                string.Empty,
                HuggingFaceSearchState.Idle,
                "Enter a Hugging Face model name to search.",
                null,
                Array.Empty<HuggingFaceModelSearchEntry>()));
        }

        var limit = request.Limit <= 0 ? 20 : Math.Clamp(request.Limit, 1, 100);
        var current = GetSnapshot();
        UpdateSnapshot(new HuggingFaceModelSearchSnapshot(
            query,
            HuggingFaceSearchState.Searching,
            $"Searching Hugging Face for '{query}'...",
            current.LastSearchUtc,
            current.Results));

        try
        {
            using var response = await _httpClient.GetAsync(CreateSearchUri(query, limit), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateSnapshot(new HuggingFaceModelSearchSnapshot(
                    query,
                    HuggingFaceSearchState.Failed,
                    $"Search failed: HTTP {(int)response.StatusCode}.",
                    DateTimeOffset.UtcNow,
                    Array.Empty<HuggingFaceModelSearchEntry>()));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<List<HuggingFaceSearchApiEntry>>(stream, JsonOptions, cancellationToken)
                ?? new List<HuggingFaceSearchApiEntry>();

            var results = payload
                .Select(ToEntry)
                .OrderByDescending(entry => entry.Downloads)
                .ThenByDescending(entry => entry.Likes)
                .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return UpdateSnapshot(new HuggingFaceModelSearchSnapshot(
                query,
                results.Count == 0 ? HuggingFaceSearchState.Empty : HuggingFaceSearchState.Ready,
                results.Count == 0
                    ? $"No Hugging Face models matched '{query}'."
                    : $"Found {results.Count} Hugging Face models for '{query}'.",
                DateTimeOffset.UtcNow,
                results));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return UpdateSnapshot(new HuggingFaceModelSearchSnapshot(
                query,
                HuggingFaceSearchState.Failed,
                $"Search failed: {ex.Message}",
                DateTimeOffset.UtcNow,
                Array.Empty<HuggingFaceModelSearchEntry>()));
        }
    }

    private HuggingFaceModelSearchSnapshot UpdateSnapshot(HuggingFaceModelSearchSnapshot snapshot)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
        }

        StateChanged?.Invoke(snapshot);
        return snapshot;
    }

    private static Uri CreateSearchUri(string query, int limit)
    {
        var escapedQuery = Uri.EscapeDataString(query);
        return new Uri($"https://huggingface.co/api/models?search={escapedQuery}&limit={limit}", UriKind.Absolute);
    }

    private static HuggingFaceModelSearchEntry ToEntry(HuggingFaceSearchApiEntry entry)
    {
        var id = !string.IsNullOrWhiteSpace(entry.ModelId) ? entry.ModelId! : entry.Id;
        var tags = entry.Tags is null
            ? new List<string>()
            : entry.Tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        return new HuggingFaceModelSearchEntry(
            id,
            entry.ModelId,
            entry.Likes,
            entry.Downloads,
            entry.PipelineTag,
            entry.LibraryName,
            entry.CreatedAtUtc,
            entry.Private,
            entry.TrendingScore,
            tags);
    }

    private sealed record HuggingFaceSearchApiEntry(
        [property: JsonPropertyName("_id")] string? InternalId,
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("modelId")] string? ModelId,
        [property: JsonPropertyName("likes")] int Likes,
        [property: JsonPropertyName("downloads")] int Downloads,
        [property: JsonPropertyName("pipeline_tag")] string? PipelineTag,
        [property: JsonPropertyName("library_name")] string? LibraryName,
        [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAtUtc,
        [property: JsonPropertyName("private")] bool Private,
        [property: JsonPropertyName("trendingScore")] int? TrendingScore,
        [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags);
}
