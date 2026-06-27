using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NexusCentral.V3.Core.Config;
using NexusCentral.V3.Core.Logging;
using NexusCentral.V3.Core.State;
using NexusCentral.V3.Features.Workspaces;
using Xunit;

namespace NexusCentral.V3.Tests;

public sealed class WorkspaceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"nexuscentral-v3-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task OpenWorkspaceAsync_OpensValidFolderAndBrowsesTree()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "readme.md"), "hello");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "src"));
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "src", "app.cs"), "class App {}");

        var service = CreateService();
        var workspace = await service.OpenWorkspaceAsync(workspaceRoot);
        var tree = await service.GetTreeAsync(workspace.Id);

        Assert.Equal(workspace.Id, service.ActiveWorkspace?.Id);
        Assert.Contains(tree.Single().Children, node => node.Name == "readme.md" && !node.IsDirectory);
        Assert.Contains(tree.Single().Children, node => node.Name == "src" && node.IsDirectory);
    }

    [Fact]
    public async Task WriteFileAsync_RejectsPathEscape()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var service = CreateService();
        var workspace = await service.OpenWorkspaceAsync(workspaceRoot);

        var result = await service.WriteFileAsync(workspace.Id, "../escape.txt", "bad");

        Assert.False(result.Ok);
        Assert.Contains("escapes", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(workspaceRoot, "..", "escape.txt")));
    }

    [Fact]
    public async Task ReadFileAsync_RejectsSymlinkEscape()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
        {
            return;
        }

        var workspaceRoot = CreateWorkspaceRoot();
        var outsideRoot = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllTextAsync(Path.Combine(outsideRoot, "secret.txt"), "secret");
        Directory.CreateSymbolicLink(Path.Combine(workspaceRoot, "linked-outside"), outsideRoot);

        var service = CreateService();
        var workspace = await service.OpenWorkspaceAsync(workspaceRoot);
        var result = await service.ReadFileAsync(workspace.Id, "linked-outside/secret.txt");

        Assert.False(result.Ok);
        Assert.Contains("link", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAndSelectWorkspace_UpdatesActiveWorkspace()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var service = CreateService();
        var opened = await service.OpenWorkspaceAsync(workspaceRoot);
        var sandbox = await service.CreateSandboxAsync("Second sandbox");

        Assert.Equal(sandbox.Id, service.ActiveWorkspace?.Id);
        Assert.True(await service.SelectWorkspaceAsync(opened.Id));
        Assert.Equal(opened.Id, service.ActiveWorkspace?.Id);
        Assert.Contains(service.GetWorkspaces(), workspace => workspace.Id == opened.Id);
        Assert.Contains(service.GetWorkspaces(), workspace => workspace.Id == sandbox.Id);
    }

    [Fact]
    public async Task GetTreeAsync_IgnoresGeneratedDirectories()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "src"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "node_modules"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "bin"));
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "src", "keep.cs"), "class Keep {}");
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "node_modules", "skip.js"), "skip");
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "bin", "skip.dll"), "skip");

        var service = CreateService();
        var workspace = await service.OpenWorkspaceAsync(workspaceRoot);
        var root = (await service.GetTreeAsync(workspace.Id, maxDepth: 3)).Single();
        var names = Flatten(root).Select(node => node.Name).ToList();

        Assert.Contains("src", names);
        Assert.Contains("keep.cs", names);
        Assert.DoesNotContain("node_modules", names);
        Assert.DoesNotContain("bin", names);
        Assert.DoesNotContain("skip.js", names);
    }

    [Fact]
    public async Task ReadWriteRenameDelete_WorkInsideWorkspace()
    {
        var workspaceRoot = CreateWorkspaceRoot();
        var service = CreateService();
        var workspace = await service.OpenWorkspaceAsync(workspaceRoot);

        var write = await service.WriteFileAsync(workspace.Id, "notes/one.md", "first");
        var read = await service.ReadFileAsync(workspace.Id, "notes/one.md");
        var rename = await service.RenamePathAsync(workspace.Id, "notes/one.md", "notes/two.md");
        var delete = await service.DeletePathAsync(workspace.Id, "notes/two.md");

        Assert.True(write.Ok);
        Assert.True(read.Ok);
        Assert.Equal("first", read.Contents);
        Assert.True(rename.Ok);
        Assert.True(delete.Ok);
        Assert.False(File.Exists(Path.Combine(workspaceRoot, "notes", "two.md")));
    }

    [Fact]
    public async Task WorkspaceSessionStore_PersistsRecentUploads()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "state"));
        var store = new FileWorkspaceSessionStore(paths, new JsonFileStore());
        var session = new WorkspaceSession(
            "ws-test",
            DateTimeOffset.UtcNow,
            new[] { "src" },
            new[] { "/tmp/input.txt" });

        await store.SaveAsync(session);
        var loaded = await store.LoadAsync("ws-test");

        Assert.Contains("src", loaded.ExpandedPaths);
        Assert.Contains("/tmp/input.txt", loaded.RecentUploads);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private WorkspaceService CreateService()
    {
        var paths = AppPathSettings.CreateDefault(Path.Combine(_root, "state"));
        return new WorkspaceService(paths, new JsonFileStore(), new InMemoryAppLogService());
    }

    private string CreateWorkspaceRoot()
    {
        var workspaceRoot = Path.Combine(_root, "workspace");
        Directory.CreateDirectory(workspaceRoot);
        return workspaceRoot;
    }

    private static IQueryable<WorkspaceTreeNode> Flatten(WorkspaceTreeNode root)
    {
        return FlattenCore(root).AsQueryable();
    }

    private static System.Collections.Generic.IEnumerable<WorkspaceTreeNode> FlattenCore(WorkspaceTreeNode node)
    {
        yield return node;
        foreach (var child in node.Children.SelectMany(FlattenCore))
        {
            yield return child;
        }
    }
}
