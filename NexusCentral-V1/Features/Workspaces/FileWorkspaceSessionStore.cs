using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V3.Core.Config;
using NexusCentral.V3.Core.State;

namespace NexusCentral.V3.Features.Workspaces;

public sealed class FileWorkspaceSessionStore : IWorkspaceSessionStore
{
    private readonly AppPathSettings _paths;
    private readonly JsonFileStore _json;

    public FileWorkspaceSessionStore(AppPathSettings paths, JsonFileStore json)
    {
        _paths = paths;
        _json = json;
    }

    public async Task<WorkspaceSession> LoadAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var path = GetSessionPath(workspaceId);
        var session = await _json.ReadAsync<WorkspaceSession>(path, cancellationToken);
        return session ?? new WorkspaceSession(workspaceId, DateTimeOffset.UtcNow, Array.Empty<string>(), Array.Empty<string>());
    }

    public Task SaveAsync(WorkspaceSession session, CancellationToken cancellationToken = default)
    {
        var normalized = session with { UpdatedAtUtc = DateTimeOffset.UtcNow };
        return _json.WriteAsync(GetSessionPath(session.WorkspaceId), normalized, cancellationToken);
    }

    private string GetSessionPath(string workspaceId)
    {
        var safeId = string.Join("_", workspaceId.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return Path.Combine(_paths.WorkspaceSessionsDirectory, $"{safeId}.json");
    }
}
