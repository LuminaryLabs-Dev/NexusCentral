using System;
using System.Collections.Generic;

namespace NexusCentral.V3.Features.Workspaces;

public sealed record WorkspaceDefinition(
    string Id,
    string Name,
    string Kind,
    string RootPath,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActiveAtUtc);

public sealed record WorkspaceTreeNode(
    string Name,
    string RelativePath,
    bool IsDirectory,
    int Depth,
    IReadOnlyList<WorkspaceTreeNode> Children);

public sealed record WorkspaceSession(
    string WorkspaceId,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> ExpandedPaths,
    IReadOnlyList<string> RecentUploads);

public sealed record WorkspaceFileReadResult(
    bool Ok,
    string RelativePath,
    string Contents,
    string Error);

public sealed record WorkspaceMutationResult(
    bool Ok,
    string RelativePath,
    string Error);

internal sealed record WorkspaceRegistry(
    string ActiveWorkspaceId,
    List<WorkspaceDefinition> Workspaces);
