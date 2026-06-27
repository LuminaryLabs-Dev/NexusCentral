using System;
using System.IO;

namespace NexusCentral.V3.Core.Config;

public sealed record AppPathSettings(
    string StateRoot,
    string WorkspaceRegistryPath,
    string WorkspaceSessionsDirectory,
    string ConversationsDirectory,
    string SandboxRootDirectory)
{
    public static AppPathSettings CreateDefault(string? stateRootOverride = null)
    {
        var stateRoot = !string.IsNullOrWhiteSpace(stateRootOverride)
            ? Path.GetFullPath(stateRootOverride)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NexusCentral",
                "V3");

        return new AppPathSettings(
            stateRoot,
            Path.Combine(stateRoot, "workspaces", "registry.json"),
            Path.Combine(stateRoot, "workspaces", "sessions"),
            Path.Combine(stateRoot, "lab", "conversations"),
            Path.Combine(stateRoot, "workspaces", "sandbox"));
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(StateRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(WorkspaceRegistryPath)!);
        Directory.CreateDirectory(WorkspaceSessionsDirectory);
        Directory.CreateDirectory(ConversationsDirectory);
        Directory.CreateDirectory(SandboxRootDirectory);
    }
}
