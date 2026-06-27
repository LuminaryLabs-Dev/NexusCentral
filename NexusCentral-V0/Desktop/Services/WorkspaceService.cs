using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NexusCentral_Avalonia.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NexusCentral_Avalonia.Services;

public class WorkspaceService
{
    public const string SandboxWorkspaceId = "sandbox";
    private static readonly string[] IgnoredDirectoryNames =
    {
        ".git",
        "bin",
        "obj",
        "node_modules",
        ".venv",
        "dist"
    };

    private readonly AppPathsService _paths;
    private readonly LogService _logs;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;
    private readonly object _lock = new();
    private WorkspaceRegistry _registry = new();
    private readonly string _sandboxRootDirectory = Path.Combine(Path.GetTempPath(), "NexusCentral", "sandbox", "root");
    private readonly string _sandboxCollectionDirectory = Path.Combine(Path.GetTempPath(), "NexusCentral", "sandboxes");

    public event Action? WorkspacesChanged;

    public WorkspaceService(AppPathsService paths, LogService logs)
    {
        _paths = paths;
        _logs = logs;
        _deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        _serializer = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        Reload();
    }

    public WorkspaceRegistry GetRegistry()
    {
        lock (_lock)
        {
            return _registry.Clone();
        }
    }

    public IReadOnlyList<WorkspaceDefinition> GetWorkspaces()
    {
        lock (_lock)
        {
            return _registry.Workspaces.Select(workspace => workspace.Clone()).ToList();
        }
    }

    public WorkspaceDefinition? GetActiveWorkspace()
    {
        lock (_lock)
        {
            return _registry.Workspaces
                .FirstOrDefault(workspace => string.Equals(workspace.Id, _registry.ActiveWorkspaceId, StringComparison.OrdinalIgnoreCase))
                ?.Clone();
        }
    }

    public WorkspaceDefinition? FindWorkspace(string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return null;
        }

        lock (_lock)
        {
            return _registry.Workspaces
                .FirstOrDefault(workspace => string.Equals(workspace.Id, workspaceId, StringComparison.OrdinalIgnoreCase))
                ?.Clone();
        }
    }

    public WorkspaceDefinition OpenWorkspace(string? rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new InvalidOperationException("Workspace root directory is required.");
        }

        var fullRoot = Path.GetFullPath(rootDirectory.Trim());
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"Workspace directory '{fullRoot}' was not found.");
        }

        WorkspaceDefinition? workspace;
        lock (_lock)
        {
            workspace = _registry.Workspaces.FirstOrDefault(candidate =>
                string.Equals(candidate.RootDirectory, fullRoot, StringComparison.OrdinalIgnoreCase));
            if (workspace is null)
            {
                workspace = CreateWorkspaceDefinition(fullRoot);
                _registry.Workspaces.Add(workspace);
            }

            workspace.LastActiveAtUtc = DateTimeOffset.UtcNow.ToString("O");
            _registry.ActiveWorkspaceId = workspace.Id;
            SaveRegistryUnsafe();
        }

        EnsureWorkspaceDirectories(workspace);
        if (!File.Exists(workspace.SessionPath))
        {
            SaveSession(CreateEmptySession(workspace));
        }

        WorkspacesChanged?.Invoke();
        return workspace.Clone();
    }

    public WorkspaceDefinition CreateSandbox(string? name = null)
    {
        var displayName = string.IsNullOrWhiteSpace(name) ? $"Sandbox {_registry.Workspaces.Count(workspace => string.Equals(workspace.Kind, "sandbox", StringComparison.OrdinalIgnoreCase)) + 1}" : name.Trim();
        WorkspaceDefinition workspace;
        lock (_lock)
        {
            var id = CreateSandboxIdUnsafe(displayName);
            var rootDirectory = Path.Combine(_sandboxCollectionDirectory, id, "root");
            Directory.CreateDirectory(rootDirectory);
            workspace = AttachPaths(new WorkspaceDefinition
            {
                Id = id,
                Name = displayName,
                Kind = "sandbox",
                RootDirectory = rootDirectory,
                Pinned = false,
                OpenedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                LastActiveAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });
            _registry.Workspaces.Add(workspace);
            _registry.ActiveWorkspaceId = workspace.Id;
            SaveRegistryUnsafe();
        }

        EnsureWorkspaceDirectories(workspace);
        SaveSession(CreateEmptySession(workspace));
        WorkspacesChanged?.Invoke();
        return workspace.Clone();
    }

    public WorkspaceDefinition DeleteSandbox(string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new InvalidOperationException("Sandbox workspace id is required.");
        }

        if (string.Equals(workspaceId, SandboxWorkspaceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The default sandbox cannot be deleted; clear it instead.");
        }

        WorkspaceDefinition workspace;
        lock (_lock)
        {
            workspace = _registry.Workspaces.FirstOrDefault(candidate => string.Equals(candidate.Id, workspaceId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Sandbox workspace '{workspaceId}' was not found.");
            if (!string.Equals(workspace.Kind, "sandbox", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Only sandbox workspaces can be deleted through this command.");
            }

            ValidateSandboxRoot(workspace.RootDirectory);
            _registry.Workspaces.Remove(workspace);
            if (string.Equals(_registry.ActiveWorkspaceId, workspace.Id, StringComparison.OrdinalIgnoreCase))
            {
                _registry.ActiveWorkspaceId = _registry.Workspaces.FirstOrDefault(candidate => string.Equals(candidate.Id, SandboxWorkspaceId, StringComparison.OrdinalIgnoreCase))?.Id
                    ?? _registry.Workspaces.FirstOrDefault()?.Id
                    ?? string.Empty;
            }

            SaveRegistryUnsafe();
        }

        if (Directory.Exists(workspace.RootDirectory))
        {
            Directory.Delete(workspace.RootDirectory, recursive: true);
        }

        if (Directory.Exists(workspace.StorageDirectory))
        {
            Directory.Delete(workspace.StorageDirectory, recursive: true);
        }

        WorkspacesChanged?.Invoke();
        return workspace.Clone();
    }

    public WorkspaceDefinition SaveSandboxToDirectory(string? targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new InvalidOperationException("Target directory is required.");
        }

        var sandbox = FindWorkspace(SandboxWorkspaceId) ?? throw new InvalidOperationException("Sandbox workspace is not available.");
        var fullTarget = Path.GetFullPath(targetDirectory.Trim());
        if (string.Equals(fullTarget, sandbox.RootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Sandbox cannot be exported into its own temp directory.");
        }

        if (Directory.Exists(fullTarget))
        {
            if (Directory.EnumerateFileSystemEntries(fullTarget).Any())
            {
                throw new InvalidOperationException("Sandbox export requires an empty target directory.");
            }
        }
        else
        {
            Directory.CreateDirectory(fullTarget);
        }

        EnsureWorkspaceRootExists(sandbox);
        CopyDirectoryContents(sandbox.RootDirectory, fullTarget);

        var exported = OpenWorkspace(fullTarget);
        var sandboxSession = LoadSession(SandboxWorkspaceId);
        var exportedSession = sandboxSession.Clone();
        exportedSession.WorkspaceId = exported.Id;
        exportedSession.WorkspaceName = exported.Name;
        exportedSession.RootDirectory = exported.RootDirectory;
        exportedSession.SelectedFilePath = RemapPathToRoot(sandbox.RootDirectory, exported.RootDirectory, sandboxSession.SelectedFilePath);
        exportedSession.SelectedFolderPath = RemapPathToRoot(sandbox.RootDirectory, exported.RootDirectory, sandboxSession.SelectedFolderPath);
        SaveSession(exportedSession);
        return exported;
    }

    public bool SelectWorkspace(string? workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return false;
        }

        lock (_lock)
        {
            var workspace = _registry.Workspaces.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, workspaceId, StringComparison.OrdinalIgnoreCase));
            if (workspace is null)
            {
                return false;
            }

            workspace.LastActiveAtUtc = DateTimeOffset.UtcNow.ToString("O");
            _registry.ActiveWorkspaceId = workspace.Id;
            SaveRegistryUnsafe();
        }

        WorkspacesChanged?.Invoke();
        return true;
    }

    public WorkspaceSessionBundle LoadSession(string? workspaceId)
    {
        var workspace = FindWorkspace(workspaceId) ?? throw new InvalidOperationException("Workspace was not found.");
        EnsureWorkspaceRootExists(workspace);
        EnsureWorkspaceDirectories(workspace);
        if (!File.Exists(workspace.SessionPath))
        {
            var empty = CreateEmptySession(workspace);
            SaveSession(empty);
            return empty;
        }

        try
        {
            var bundle = _deserializer.Deserialize<WorkspaceSessionBundle>(File.ReadAllText(workspace.SessionPath)) ?? new WorkspaceSessionBundle();
            return NormalizeSession(bundle, workspace);
        }
        catch (Exception ex)
        {
            _logs.Append("warning", "workspaces", $"Failed to load workspace session '{workspace.SessionPath}': {ex.Message}");
            var empty = CreateEmptySession(workspace);
            SaveSession(empty);
            return empty;
        }
    }

    public void SaveSession(WorkspaceSessionBundle bundle)
    {
        if (string.IsNullOrWhiteSpace(bundle.WorkspaceId))
        {
            throw new InvalidOperationException("Workspace session requires a workspace id.");
        }

        var workspace = FindWorkspace(bundle.WorkspaceId) ?? throw new InvalidOperationException($"Workspace '{bundle.WorkspaceId}' was not found.");
        EnsureWorkspaceDirectories(workspace);
        var normalized = NormalizeSession(bundle.Clone(), workspace);
        normalized.SavedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        File.WriteAllText(workspace.SessionPath, _serializer.Serialize(normalized));
    }

    public string ArchiveSession(string? workspaceId)
    {
        var workspace = FindWorkspace(workspaceId) ?? throw new InvalidOperationException("Workspace was not found.");
        EnsureWorkspaceDirectories(workspace);
        var current = LoadSession(workspace.Id);
        var archiveId = $"{workspace.Id}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var archivePath = Path.Combine(workspace.ArchivesDirectory, $"{archiveId}.yaml");
        File.WriteAllText(archivePath, _serializer.Serialize(current));
        SaveSession(CreateEmptySession(workspace));
        return archivePath;
    }

    public IReadOnlyList<WorkspaceFileNode> GetRootNodes(string? workspaceId)
    {
        var workspace = FindWorkspace(workspaceId);
        return workspace is null
            ? Array.Empty<WorkspaceFileNode>()
            : EnumerateChildren(workspace.RootDirectory, workspace.RootDirectory);
    }

    public IReadOnlyList<WorkspaceFileNode> GetChildren(string? workspaceId, string? directoryPath)
    {
        var workspace = FindWorkspace(workspaceId);
        if (workspace is null)
        {
            return Array.Empty<WorkspaceFileNode>();
        }

        EnsureWorkspaceRootExists(workspace);

        var fullDirectory = string.IsNullOrWhiteSpace(directoryPath)
            ? workspace.RootDirectory
            : Path.GetFullPath(directoryPath);
        if (!Directory.Exists(fullDirectory))
        {
            return Array.Empty<WorkspaceFileNode>();
        }

        return EnumerateChildren(fullDirectory, workspace.RootDirectory);
    }

    public bool IsPathInsideWorkspace(string? workspaceId, string? path)
    {
        var workspace = FindWorkspace(workspaceId);
        if (workspace is null || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(workspace.RootDirectory, StringComparison.OrdinalIgnoreCase);
    }

    public string? ResolveWorkspaceRelativePath(string? workspaceId, string? path)
    {
        var workspace = FindWorkspace(workspaceId);
        if (workspace is null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(workspace.RootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relative = Path.GetRelativePath(workspace.RootDirectory, fullPath);
        return string.Equals(relative, ".", StringComparison.Ordinal) ? string.Empty : relative.Replace('\\', '/');
    }

    public string ReadFileText(string? workspaceId, string? path)
    {
        var fullPath = ResolveWorkspaceFilePath(workspaceId, path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Workspace file '{fullPath}' was not found.", fullPath);
        }

        return File.ReadAllText(fullPath);
    }

    public void SaveFileText(string? workspaceId, string? path, string? contents)
    {
        var fullPath = ResolveWorkspaceFilePath(workspaceId, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents ?? string.Empty);
    }

    public void CreateDirectoryPath(string? workspaceId, string? path)
    {
        var fullPath = ResolveWorkspaceFilePath(workspaceId, path);
        Directory.CreateDirectory(fullPath);
    }

    public void DeletePath(string? workspaceId, string? path)
    {
        var fullPath = ResolveWorkspaceFilePath(workspaceId, path);
        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
            return;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public IReadOnlyList<string> ClearSandbox(string? workspaceId = null)
    {
        var sandbox = FindWorkspace(string.IsNullOrWhiteSpace(workspaceId) ? SandboxWorkspaceId : workspaceId) ?? throw new InvalidOperationException("Sandbox workspace is not available.");
        if (!string.Equals(sandbox.Kind, "sandbox", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only the sandbox workspace can be cleared.");
        }

        EnsureWorkspaceRootExists(sandbox);
        var root = ValidateSandboxRoot(sandbox.RootDirectory);

        var before = Directory.EnumerateFileSystemEntries(root)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else if (File.Exists(entry))
            {
                File.Delete(entry);
            }
        }

        WorkspacesChanged?.Invoke();
        return before;
    }

    public void RenamePath(string? workspaceId, string? sourcePath, string? destinationPath)
    {
        var fullSourcePath = ResolveWorkspaceFilePath(workspaceId, sourcePath);
        var fullDestinationPath = ResolveWorkspaceFilePath(workspaceId, destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDestinationPath)!);

        if (Directory.Exists(fullSourcePath))
        {
            Directory.Move(fullSourcePath, fullDestinationPath);
            return;
        }

        if (File.Exists(fullSourcePath))
        {
            File.Move(fullSourcePath, fullDestinationPath, overwrite: true);
            return;
        }

        throw new FileNotFoundException($"Workspace path '{fullSourcePath}' was not found.", fullSourcePath);
    }

    public bool IsEditableWorkspace(string? workspaceId)
    {
        var workspace = FindWorkspace(workspaceId);
        return workspace is not null && string.Equals(workspace.Kind, "sandbox", StringComparison.OrdinalIgnoreCase);
    }

    public void Reload()
    {
        lock (_lock)
        {
            if (File.Exists(_paths.WorkspaceRegistryPath))
            {
                try
                {
                    _registry = _deserializer.Deserialize<WorkspaceRegistry>(File.ReadAllText(_paths.WorkspaceRegistryPath)) ?? new WorkspaceRegistry();
                }
                catch (Exception ex)
                {
                    _logs.Append("warning", "workspaces", $"Failed to load workspace registry '{_paths.WorkspaceRegistryPath}': {ex.Message}");
                    _registry = new WorkspaceRegistry();
                }
            }
            else
            {
                _registry = new WorkspaceRegistry();
            }

            _registry.Workspaces ??= new List<WorkspaceDefinition>();
            _registry.Workspaces = _registry.Workspaces
                .Where(workspace =>
                    string.Equals(workspace.Kind, "sandbox", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(workspace.RootDirectory) && Directory.Exists(workspace.RootDirectory)))
                .Select(AttachPaths)
                .ToList();

            EnsureSandboxWorkspaceUnsafe();

            _registry.Workspaces = _registry.Workspaces
                .OrderBy(workspace => string.Equals(workspace.Id, SandboxWorkspaceId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenByDescending(workspace => workspace.Pinned)
                .ThenByDescending(workspace => workspace.LastActiveAtUtc, StringComparer.OrdinalIgnoreCase)
                .ThenBy(workspace => workspace.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!_registry.Workspaces.Any(workspace => string.Equals(workspace.Id, _registry.ActiveWorkspaceId, StringComparison.OrdinalIgnoreCase)))
            {
                _registry.ActiveWorkspaceId = _registry.Workspaces.FirstOrDefault(workspace => string.Equals(workspace.Id, SandboxWorkspaceId, StringComparison.OrdinalIgnoreCase))?.Id
                    ?? _registry.Workspaces.FirstOrDefault()?.Id
                    ?? string.Empty;
            }

            SaveRegistryUnsafe();
        }

        WorkspacesChanged?.Invoke();
    }

    private WorkspaceDefinition CreateWorkspaceDefinition(string rootDirectory)
    {
        var directoryName = Path.GetFileName(rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var idSeed = $"{directoryName}:{rootDirectory}";
        using var sha1 = SHA1.Create();
        var hash = Convert.ToHexString(sha1.ComputeHash(Encoding.UTF8.GetBytes(idSeed))).ToLowerInvariant()[..8];
        var workspace = new WorkspaceDefinition
        {
            Id = $"{SanitizeId(directoryName)}-{hash}",
            Name = string.IsNullOrWhiteSpace(directoryName) ? rootDirectory : directoryName,
            Kind = "directory",
            RootDirectory = rootDirectory,
            OpenedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            LastActiveAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };

        return AttachPaths(workspace);
    }

    private WorkspaceDefinition AttachPaths(WorkspaceDefinition workspace)
    {
        workspace.Id = string.IsNullOrWhiteSpace(workspace.Id) ? SanitizeId(workspace.Name) : workspace.Id.Trim();
        workspace.Name = string.IsNullOrWhiteSpace(workspace.Name)
            ? Path.GetFileName(workspace.RootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : workspace.Name.Trim();
        workspace.Kind = string.IsNullOrWhiteSpace(workspace.Kind) ? "directory" : workspace.Kind.Trim().ToLowerInvariant();
        workspace.RootDirectory = Path.GetFullPath(workspace.RootDirectory);
        workspace.StorageDirectory = Path.Combine(_paths.WorkspacesDirectory, workspace.Id);
        workspace.SessionPath = Path.Combine(workspace.StorageDirectory, "session.yaml");
        workspace.ArchivesDirectory = Path.Combine(workspace.StorageDirectory, "archives");
        workspace.DraftsDirectory = Path.Combine(workspace.StorageDirectory, "drafts");
        workspace.UiStatePath = Path.Combine(workspace.StorageDirectory, "ui-state.json");
        return workspace;
    }

    private void EnsureWorkspaceDirectories(WorkspaceDefinition workspace)
    {
        Directory.CreateDirectory(workspace.StorageDirectory);
        Directory.CreateDirectory(workspace.ArchivesDirectory);
        Directory.CreateDirectory(workspace.DraftsDirectory);
    }

    private void EnsureWorkspaceRootExists(WorkspaceDefinition workspace)
    {
        if (string.Equals(workspace.Kind, "sandbox", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(workspace.RootDirectory);
        }
    }

    private WorkspaceSessionBundle CreateEmptySession(WorkspaceDefinition workspace)
    {
        return new WorkspaceSessionBundle
        {
            WorkspaceId = workspace.Id,
            WorkspaceName = workspace.Name,
            RootDirectory = workspace.RootDirectory,
            SessionId = Guid.NewGuid().ToString("N")
        };
    }

    private WorkspaceSessionBundle NormalizeSession(WorkspaceSessionBundle session, WorkspaceDefinition workspace)
    {
        session.WorkspaceId = workspace.Id;
        session.WorkspaceName = workspace.Name;
        session.RootDirectory = workspace.RootDirectory;
        session.SessionId = string.IsNullOrWhiteSpace(session.SessionId) ? Guid.NewGuid().ToString("N") : session.SessionId.Trim();
        session.Messages ??= new List<WorkspaceChatMessageRecord>();
        session.DraftChanges ??= new List<WorkspaceDraftChange>();
        session.ArtifactInputsText ??= string.Empty;
        session.InputText ??= string.Empty;
        session.SelectedFilePath ??= string.Empty;
        session.SelectedFolderPath ??= string.Empty;
        session.SavedAtUtc = string.IsNullOrWhiteSpace(session.SavedAtUtc) ? DateTimeOffset.UtcNow.ToString("O") : session.SavedAtUtc;
        return session;
    }

    private void EnsureSandboxWorkspaceUnsafe()
    {
        Directory.CreateDirectory(_sandboxRootDirectory);
        var sandbox = _registry.Workspaces.FirstOrDefault(workspace => string.Equals(workspace.Id, SandboxWorkspaceId, StringComparison.OrdinalIgnoreCase));
        if (sandbox is null)
        {
            sandbox = AttachPaths(new WorkspaceDefinition
            {
                Id = SandboxWorkspaceId,
                Name = "Sandbox",
                Kind = "sandbox",
                RootDirectory = _sandboxRootDirectory,
                Pinned = true,
                OpenedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                LastActiveAtUtc = DateTimeOffset.UtcNow.ToString("O")
            });
            _registry.Workspaces.Add(sandbox);
        }
        else
        {
            sandbox.Kind = "sandbox";
            sandbox.Name = "Sandbox";
            sandbox.Pinned = true;
            sandbox.RootDirectory = _sandboxRootDirectory;
            AttachPaths(sandbox);
        }
    }

    private string CreateSandboxIdUnsafe(string displayName)
    {
        var safeName = SanitizeId(displayName);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant();
            var candidate = $"{safeName}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{suffix}";
            if (!candidate.StartsWith("sandbox-", StringComparison.OrdinalIgnoreCase))
            {
                candidate = $"sandbox-{candidate}";
            }

            if (!_registry.Workspaces.Any(workspace => string.Equals(workspace.Id, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return $"sandbox-{DateTimeOffset.UtcNow:yyyyMMddHHmmssffff}";
    }

    private IReadOnlyList<WorkspaceFileNode> EnumerateChildren(string directoryPath, string rootDirectory)
    {
        var children = new List<WorkspaceFileNode>();
        try
        {
            foreach (var childDirectory in Directory.EnumerateDirectories(directoryPath)
                         .Where(path => !IgnoredDirectoryNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                children.Add(new WorkspaceFileNode
                {
                    Name = Path.GetFileName(childDirectory),
                    FullPath = Path.GetFullPath(childDirectory),
                    RelativePath = NormalizeRelative(rootDirectory, childDirectory),
                    IsDirectory = true,
                    HasChildren = Directory.EnumerateFileSystemEntries(childDirectory)
                        .Any(path => !Directory.Exists(path) || !IgnoredDirectoryNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                });
            }

            foreach (var file in Directory.EnumerateFiles(directoryPath)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                children.Add(new WorkspaceFileNode
                {
                    Name = Path.GetFileName(file),
                    FullPath = Path.GetFullPath(file),
                    RelativePath = NormalizeRelative(rootDirectory, file),
                    IsDirectory = false,
                    HasChildren = false
                });
            }
        }
        catch (Exception ex)
        {
            _logs.Append("warning", "workspaces", $"Failed to read directory '{directoryPath}': {ex.Message}");
        }

        return children;
    }

    private void SaveRegistryUnsafe()
    {
        Directory.CreateDirectory(_paths.WorkspacesDirectory);
        File.WriteAllText(_paths.WorkspaceRegistryPath, _serializer.Serialize(_registry));
    }

    private static string SanitizeId(string? value)
    {
        var safe = string.IsNullOrWhiteSpace(value)
            ? "workspace"
            : new string(value.Trim().ToLowerInvariant().Select(character =>
                char.IsLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
        return string.IsNullOrWhiteSpace(safe) ? "workspace" : safe;
    }

    private static string NormalizeRelative(string rootDirectory, string path)
    {
        var relative = Path.GetRelativePath(rootDirectory, path).Replace('\\', '/');
        return string.Equals(relative, ".", StringComparison.Ordinal) ? string.Empty : relative;
    }

    private string ValidateSandboxRoot(string rootDirectory)
    {
        var root = Path.GetFullPath(rootDirectory);
        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "NexusCentral"));
        if (string.IsNullOrWhiteSpace(root) || !root.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Sandbox root failed safety validation.");
        }

        return root;
    }

    private static void CopyDirectoryContents(string sourceDirectory, string targetDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var targetPath = Path.Combine(targetDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file, targetPath, overwrite: true);
        }
    }

    private static string RemapPathToRoot(string sourceRoot, string targetRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var relative = Path.GetRelativePath(sourceRoot, fullPath);
        return Path.GetFullPath(Path.Combine(targetRoot, relative));
    }

    private string ResolveWorkspaceFilePath(string? workspaceId, string? path)
    {
        var workspace = FindWorkspace(workspaceId) ?? throw new InvalidOperationException("Workspace was not found.");
        EnsureWorkspaceRootExists(workspace);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Workspace path is required.");
        }

        var candidate = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workspace.RootDirectory, path));
        if (!candidate.StartsWith(workspace.RootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Workspace path must remain inside the workspace root.");
        }

        return candidate;
    }
}
