using System;
using System.Collections.Generic;

namespace NexusCentral.V1.Features.Models;

public enum HuggingFaceSearchState
{
    Idle,
    Searching,
    Ready,
    Empty,
    Failed
}

public sealed record HuggingFaceModelSearchRequest(
    string Query,
    int Limit);

public sealed record HuggingFaceModelSearchEntry(
    string Id,
    string? ModelId,
    int Likes,
    int Downloads,
    string? PipelineTag,
    string? LibraryName,
    DateTimeOffset? CreatedAtUtc,
    bool Private,
    int? TrendingScore,
    IReadOnlyList<string> Tags);

public sealed record HuggingFaceModelSearchSnapshot(
    string Query,
    HuggingFaceSearchState State,
    string StatusText,
    DateTimeOffset? LastSearchUtc,
    IReadOnlyList<HuggingFaceModelSearchEntry> Results);
