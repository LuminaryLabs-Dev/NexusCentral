using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V3.Features.Workspaces;

public interface IWorkspaceService
{
    WorkspaceDefinition? ActiveWorkspace { get; }

    IReadOnlyList<WorkspaceDefinition> GetWorkspaces();

    Task<WorkspaceDefinition> CreateSandboxAsync(string? name = null, CancellationToken cancellationToken = default);

    Task<WorkspaceDefinition> OpenWorkspaceAsync(string rootPath, CancellationToken cancellationToken = default);

    Task<bool> SelectWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceTreeNode>> GetTreeAsync(string workspaceId, int maxDepth = 4, CancellationToken cancellationToken = default);

    Task<WorkspaceFileReadResult> ReadFileAsync(string workspaceId, string relativePath, CancellationToken cancellationToken = default);

    Task<WorkspaceMutationResult> WriteFileAsync(string workspaceId, string relativePath, string contents, CancellationToken cancellationToken = default);

    Task<WorkspaceMutationResult> RenamePathAsync(string workspaceId, string sourceRelativePath, string destinationRelativePath, CancellationToken cancellationToken = default);

    Task<WorkspaceMutationResult> DeletePathAsync(string workspaceId, string relativePath, CancellationToken cancellationToken = default);
}

public interface IWorkspaceSessionStore
{
    Task<WorkspaceSession> LoadAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task SaveAsync(WorkspaceSession session, CancellationToken cancellationToken = default);
}
