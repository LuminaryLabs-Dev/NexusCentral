using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V3.Core.Config;
using NexusCentral.V3.Core.Logging;
using NexusCentral.V3.Core.State;

namespace NexusCentral.V3.Features.Workspaces;

public sealed class WorkspaceService : IWorkspaceService
{
    private static readonly JsonSerializerOptions RegistryJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".next",
        ".venv",
        "__pycache__",
        "bin",
        "build",
        "dist",
        "node_modules",
        "obj",
        "target",
        "venv"
    };

    private readonly AppPathSettings _paths;
    private readonly JsonFileStore _json;
    private readonly IAppLogService _logs;
    private readonly object _gate = new();
    private WorkspaceRegistry _registry = new(string.Empty, new List<WorkspaceDefinition>());

    public WorkspaceService(AppPathSettings paths, JsonFileStore json, IAppLogService logs)
    {
        _paths = paths;
        _json = json;
        _logs = logs;
        _paths.EnsureDirectories();
        LoadRegistry();
        EnsureDefaultSandbox();
    }

    public WorkspaceDefinition? ActiveWorkspace
    {
        get
        {
            lock (_gate)
            {
                return _registry.Workspaces.FirstOrDefault(workspace => string.Equals(workspace.Id, _registry.ActiveWorkspaceId, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    public IReadOnlyList<WorkspaceDefinition> GetWorkspaces()
    {
        lock (_gate)
        {
            return _registry.Workspaces.ToList();
        }
    }

    public async Task<WorkspaceDefinition> CreateSandboxAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        var displayName = string.IsNullOrWhiteSpace(name) ? "Sandbox" : name.Trim();
        var id = CreateWorkspaceId(displayName, _paths.SandboxRootDirectory);
        var root = Path.Combine(_paths.SandboxRootDirectory, id);
        Directory.CreateDirectory(root);

        var workspace = new WorkspaceDefinition(id, displayName, "sandbox", root, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        lock (_gate)
        {
            _registry.Workspaces.RemoveAll(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
            _registry.Workspaces.Add(workspace);
            _registry = _registry with { ActiveWorkspaceId = workspace.Id };
        }

        await SaveRegistryAsync(cancellationToken);
        _logs.Append("info", "workspaces", $"Created sandbox workspace '{workspace.Name}'.", workspace.Id);
        return workspace;
    }

    public async Task<WorkspaceDefinition> OpenWorkspaceAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new InvalidOperationException("Workspace root path is required.");
        }

        var root = Path.GetFullPath(rootPath);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Workspace directory '{root}' was not found.");
        }

        WorkspaceDefinition workspace;
        lock (_gate)
        {
            workspace = _registry.Workspaces.FirstOrDefault(candidate => string.Equals(candidate.RootPath, root, StringComparison.OrdinalIgnoreCase))
                ?? new WorkspaceDefinition(CreateWorkspaceId(Path.GetFileName(root), root), Path.GetFileName(root), "folder", root, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

            workspace = workspace with { LastActiveAtUtc = DateTimeOffset.UtcNow };
            _registry.Workspaces.RemoveAll(candidate => string.Equals(candidate.Id, workspace.Id, StringComparison.OrdinalIgnoreCase));
            _registry.Workspaces.Add(workspace);
            _registry = _registry with { ActiveWorkspaceId = workspace.Id };
        }

        await SaveRegistryAsync(cancellationToken);
        _logs.Append("info", "workspaces", $"Opened workspace '{workspace.Name}'.", workspace.Id);
        return workspace;
    }

    public async Task<bool> SelectWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var workspace = _registry.Workspaces.FirstOrDefault(candidate => string.Equals(candidate.Id, workspaceId, StringComparison.OrdinalIgnoreCase));
            if (workspace is null)
            {
                return false;
            }

            _registry.Workspaces.Remove(workspace);
            _registry.Workspaces.Add(workspace with { LastActiveAtUtc = DateTimeOffset.UtcNow });
            _registry = _registry with { ActiveWorkspaceId = workspace.Id };
        }

        await SaveRegistryAsync(cancellationToken);
        return true;
    }

    public Task<IReadOnlyList<WorkspaceTreeNode>> GetTreeAsync(string workspaceId, int maxDepth = 4, CancellationToken cancellationToken = default)
    {
        var workspace = GetRequiredWorkspace(workspaceId);
        var root = new DirectoryInfo(workspace.RootPath);
        var nodes = new List<WorkspaceTreeNode>
        {
            BuildDirectoryNode(root, string.Empty, 0, Math.Max(1, maxDepth), cancellationToken)
        };

        return Task.FromResult<IReadOnlyList<WorkspaceTreeNode>>(nodes);
    }

    public async Task<WorkspaceFileReadResult> ReadFileAsync(string workspaceId, string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var workspace = GetRequiredWorkspace(workspaceId);
            var path = ResolveInsideWorkspace(workspace, relativePath);
            if (!File.Exists(path))
            {
                return new WorkspaceFileReadResult(false, relativePath, string.Empty, "File was not found.");
            }

            var contents = await File.ReadAllTextAsync(path, cancellationToken);
            return new WorkspaceFileReadResult(true, NormalizeRelativePath(relativePath), contents, string.Empty);
        }
        catch (Exception ex)
        {
            return new WorkspaceFileReadResult(false, NormalizeRelativePath(relativePath), string.Empty, ex.Message);
        }
    }

    public async Task<WorkspaceMutationResult> WriteFileAsync(string workspaceId, string relativePath, string contents, CancellationToken cancellationToken = default)
    {
        try
        {
            var workspace = GetRequiredWorkspace(workspaceId);
            var path = ResolveInsideWorkspace(workspace, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, contents ?? string.Empty, cancellationToken);
            return new WorkspaceMutationResult(true, NormalizeRelativePath(relativePath), string.Empty);
        }
        catch (Exception ex)
        {
            return new WorkspaceMutationResult(false, NormalizeRelativePath(relativePath), ex.Message);
        }
    }

    public Task<WorkspaceMutationResult> RenamePathAsync(string workspaceId, string sourceRelativePath, string destinationRelativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var workspace = GetRequiredWorkspace(workspaceId);
            var sourcePath = ResolveInsideWorkspace(workspace, sourceRelativePath);
            var destinationPath = ResolveInsideWorkspace(workspace, destinationRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
            }
            else
            {
                return Task.FromResult(new WorkspaceMutationResult(false, NormalizeRelativePath(sourceRelativePath), "Source path was not found."));
            }

            return Task.FromResult(new WorkspaceMutationResult(true, NormalizeRelativePath(destinationRelativePath), string.Empty));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new WorkspaceMutationResult(false, NormalizeRelativePath(sourceRelativePath), ex.Message));
        }
    }

    public Task<WorkspaceMutationResult> DeletePathAsync(string workspaceId, string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var workspace = GetRequiredWorkspace(workspaceId);
            var path = ResolveInsideWorkspace(workspace, relativePath);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
            else
            {
                return Task.FromResult(new WorkspaceMutationResult(false, NormalizeRelativePath(relativePath), "Path was not found."));
            }

            return Task.FromResult(new WorkspaceMutationResult(true, NormalizeRelativePath(relativePath), string.Empty));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new WorkspaceMutationResult(false, NormalizeRelativePath(relativePath), ex.Message));
        }
    }

    private async Task LoadRegistryAsync(CancellationToken cancellationToken)
    {
        _registry = await _json.ReadAsync<WorkspaceRegistry>(_paths.WorkspaceRegistryPath, cancellationToken)
            ?? new WorkspaceRegistry(string.Empty, new List<WorkspaceDefinition>());
    }

    private void LoadRegistry()
    {
        if (!File.Exists(_paths.WorkspaceRegistryPath))
        {
            _registry = new WorkspaceRegistry(string.Empty, new List<WorkspaceDefinition>());
            return;
        }

        using var stream = File.OpenRead(_paths.WorkspaceRegistryPath);
        _registry = JsonSerializer.Deserialize<WorkspaceRegistry>(stream, RegistryJsonOptions)
            ?? new WorkspaceRegistry(string.Empty, new List<WorkspaceDefinition>());
    }

    private async Task SaveRegistryAsync(CancellationToken cancellationToken)
    {
        WorkspaceRegistry snapshot;
        lock (_gate)
        {
            snapshot = _registry with { Workspaces = _registry.Workspaces.OrderBy(workspace => workspace.Name).ToList() };
        }

        await _json.WriteAsync(_paths.WorkspaceRegistryPath, snapshot, cancellationToken);
    }

    private void SaveRegistry()
    {
        WorkspaceRegistry snapshot;
        lock (_gate)
        {
            snapshot = _registry with { Workspaces = _registry.Workspaces.OrderBy(workspace => workspace.Name).ToList() };
        }

        var directory = Path.GetDirectoryName(_paths.WorkspaceRegistryPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{_paths.WorkspaceRegistryPath}.tmp";
        using (var stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, snapshot, RegistryJsonOptions);
        }

        if (File.Exists(_paths.WorkspaceRegistryPath))
        {
            File.Replace(tempPath, _paths.WorkspaceRegistryPath, null);
        }
        else
        {
            File.Move(tempPath, _paths.WorkspaceRegistryPath);
        }
    }

    private async Task EnsureDefaultSandboxAsync(CancellationToken cancellationToken)
    {
        if (ActiveWorkspace is not null)
        {
            return;
        }

        await CreateSandboxAsync("Sandbox", cancellationToken);
    }

    private void EnsureDefaultSandbox()
    {
        if (ActiveWorkspace is not null)
        {
            return;
        }

        var displayName = "Sandbox";
        var id = CreateWorkspaceId(displayName, _paths.SandboxRootDirectory);
        var root = Path.Combine(_paths.SandboxRootDirectory, id);
        Directory.CreateDirectory(root);

        var workspace = new WorkspaceDefinition(id, displayName, "sandbox", root, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        lock (_gate)
        {
            _registry.Workspaces.RemoveAll(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
            _registry.Workspaces.Add(workspace);
            _registry = _registry with { ActiveWorkspaceId = workspace.Id };
        }

        SaveRegistry();
        _logs.Append("info", "workspaces", $"Created sandbox workspace '{workspace.Name}'.", workspace.Id);
    }

    private WorkspaceDefinition GetRequiredWorkspace(string workspaceId)
    {
        lock (_gate)
        {
            return _registry.Workspaces.FirstOrDefault(workspace => string.Equals(workspace.Id, workspaceId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Workspace '{workspaceId}' was not found.");
        }
    }

    private static WorkspaceTreeNode BuildDirectoryNode(DirectoryInfo directory, string relativePath, int depth, int maxDepth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var children = new List<WorkspaceTreeNode>();

        if (depth < maxDepth)
        {
            foreach (var childDirectory in directory.EnumerateDirectories().Where(dir => !IgnoredDirectoryNames.Contains(dir.Name)).OrderBy(dir => dir.Name))
            {
                var childRelativePath = CombineRelative(relativePath, childDirectory.Name);
                children.Add(BuildDirectoryNode(childDirectory, childRelativePath, depth + 1, maxDepth, cancellationToken));
            }

            foreach (var file in directory.EnumerateFiles().OrderBy(file => file.Name))
            {
                children.Add(new WorkspaceTreeNode(file.Name, CombineRelative(relativePath, file.Name), false, depth + 1, Array.Empty<WorkspaceTreeNode>()));
            }
        }

        return new WorkspaceTreeNode(directory.Name, relativePath, true, depth, children);
    }

    private static string ResolveInsideWorkspace(WorkspaceDefinition workspace, string relativePath)
    {
        var normalizedRelative = NormalizeRelativePath(relativePath);
        var rootPath = Path.GetFullPath(workspace.RootPath);
        var root = EnsureTrailingSeparator(rootPath);
        var candidate = Path.GetFullPath(Path.Combine(root, normalizedRelative));
        if (!IsInsidePath(root, candidate))
        {
            throw new InvalidOperationException("Path escapes the selected workspace.");
        }

        EnsureExistingSegmentsStayInsideWorkspace(rootPath, candidate);
        return candidate;
    }

    private static void EnsureExistingSegmentsStayInsideWorkspace(string workspaceRoot, string candidatePath)
    {
        if (!Directory.Exists(workspaceRoot))
        {
            throw new DirectoryNotFoundException($"Workspace directory '{workspaceRoot}' was not found.");
        }

        var resolvedRoot = ResolveExistingPath(workspaceRoot);
        var resolvedRootWithSeparator = EnsureTrailingSeparator(resolvedRoot);
        var relative = Path.GetRelativePath(workspaceRoot, candidatePath);
        if (string.IsNullOrWhiteSpace(relative) || relative == ".")
        {
            return;
        }

        var current = workspaceRoot;
        foreach (var segment in relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
            {
                return;
            }

            var attributes = File.GetAttributes(current);
            if (!attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            var resolved = ResolveExistingPath(current);
            if (!IsInsidePath(resolvedRootWithSeparator, resolved))
            {
                throw new InvalidOperationException("Path crosses a workspace link that escapes the selected workspace.");
            }
        }
    }

    private static string ResolveExistingPath(string path)
    {
        FileSystemInfo info = Directory.Exists(path)
            ? new DirectoryInfo(path)
            : new FileInfo(path);

        return Path.GetFullPath(info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? info.FullName);
    }

    private static bool IsInsidePath(string rootWithSeparator, string candidate)
    {
        var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(rootWithSeparator));
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.Ordinal)
            || string.Equals(
                normalizedCandidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.Ordinal);
    }

    private static string NormalizeRelativePath(string? relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : relativePath.Replace('\\', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string CombineRelative(string prefix, string name)
    {
        return string.IsNullOrWhiteSpace(prefix) ? name : Path.Combine(prefix, name);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
    }

    private static string CreateWorkspaceId(string name, string rootPath)
    {
        var input = $"{name}:{Path.GetFullPath(rootPath)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"ws-{Convert.ToHexString(bytes)[..12].ToLowerInvariant()}";
    }
}
