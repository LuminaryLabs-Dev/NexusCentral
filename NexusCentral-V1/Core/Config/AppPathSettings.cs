using System;
using System.IO;

namespace NexusCentral.V1.Core.Config;

public sealed record AppPathSettings(
    string StateRoot,
    string WorkspaceRegistryPath,
    string WorkspaceSessionsDirectory,
    string ConversationsDirectory,
    string SandboxRootDirectory,
    string ModelEndpointRegistryPath,
    string RouterRegistryPath,
    string HarnessRegistryPath,
    string HarnessOrchestrationSmokeHistoryPath,
    string ValidationJobRegistryPath)
{
    public static AppPathSettings CreateDefault(string? stateRootOverride = null)
    {
        var stateRoot = !string.IsNullOrWhiteSpace(stateRootOverride)
            ? Path.GetFullPath(stateRootOverride)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NexusCentral",
                "V1");

        return new AppPathSettings(
            stateRoot,
            Path.Combine(stateRoot, "workspaces", "registry.json"),
            Path.Combine(stateRoot, "workspaces", "sessions"),
            Path.Combine(stateRoot, "lab", "conversations"),
            Path.Combine(stateRoot, "workspaces", "sandbox"),
            Path.Combine(stateRoot, "models", "endpoints.json"),
            Path.Combine(stateRoot, "router", "registry.json"),
            Path.Combine(stateRoot, "harnesses", "registry.json"),
            Path.Combine(stateRoot, "harnesses", "orchestration-smoke-history.json"),
            Path.Combine(stateRoot, "validation", "jobs.json"));
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(StateRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(WorkspaceRegistryPath)!);
        Directory.CreateDirectory(WorkspaceSessionsDirectory);
        Directory.CreateDirectory(ConversationsDirectory);
        Directory.CreateDirectory(SandboxRootDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(ModelEndpointRegistryPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(RouterRegistryPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(HarnessRegistryPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(HarnessOrchestrationSmokeHistoryPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(ValidationJobRegistryPath)!);
    }
}
