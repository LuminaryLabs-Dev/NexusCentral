using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NexusCentral_Avalonia.Services;

public class AppPathsService
{
    private const string DefaultStateDirectoryName = "NexusCentral-Triceratops";
    private const string MigrationVersion = "2026-03-state-root-v1";
    private static readonly JsonSerializerOptions MigrationJsonOptions = new() { WriteIndented = true };

    public AppPathsService()
        : this(AppContext.BaseDirectory)
    {
    }

    public AppPathsService(
        string baseDirectory,
        string? bundleRootOverridePath = null,
        string? stateRootOverridePath = null,
        string? legacyDocumentsModelsDirectory = null)
    {
        BaseDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(baseDirectory) ? AppContext.BaseDirectory : baseDirectory);
        BundleRootOverridePath = ResolveBundleRootOverride(bundleRootOverridePath);
        BundleRootOverrideActive = !string.IsNullOrWhiteSpace(BundleRootOverridePath);
        ResolvedRootDirectory = BundleRootOverrideActive ? BundleRootOverridePath! : BaseDirectory;
        PathMode = BundleRootOverrideActive ? "bundle_root_override" : "default_base_directory";
        BundleRootDirectory = ResolvedRootDirectory;

        StateRootOverridePath = ResolveStateRootOverride(stateRootOverridePath);
        StateRootOverrideActive = !string.IsNullOrWhiteSpace(StateRootOverridePath);
        StateRootDirectory = StateRootOverrideActive ? StateRootOverridePath! : GetDefaultStateRootDirectory();
        StatePathMode = StateRootOverrideActive ? "state_root_override" : "default_os_app_data";

        RuntimeDirectory = ResolveBundleSubdirectory(BundleRootDirectory, "runtime");
        ConfigDirectory = ResolveBundleSubdirectory(BundleRootDirectory, "config");
        TemplatesDirectory = Path.Combine(ConfigDirectory, "templates");
        WorkflowDefinitionsDirectory = Path.Combine(ConfigDirectory, "workflows");
        WrapperDefinitionsDirectory = Path.Combine(ConfigDirectory, "wrappers");
        WrapperScriptsDirectory = ResolveBundleSubdirectory(BundleRootDirectory, "wrappers");
        ModelsDirectory = Path.Combine(StateRootDirectory, "models");
        LogsDirectory = Path.Combine(StateRootDirectory, "logs");
        DownloadsLogDirectory = Path.Combine(LogsDirectory, "downloads");
        ChatDataDirectory = Path.Combine(LogsDirectory, "chat");
        UserTemplatesDirectory = Path.Combine(LogsDirectory, "templates");
        UserWorkflowsDirectory = Path.Combine(LogsDirectory, "workflows");
        UserWrappersDirectory = Path.Combine(LogsDirectory, "wrappers");
        AgentConfigsDirectory = Path.Combine(LogsDirectory, "agent-configs");
        WorkspacesDirectory = Path.Combine(LogsDirectory, "workspaces");
        WrapperRunsDirectory = Path.Combine(LogsDirectory, "wrapper-runs");
        WorkflowBundlesDirectory = Path.Combine(LogsDirectory, "workflow-bundles");
        WorkflowRunsDirectory = Path.Combine(LogsDirectory, "workflow-runs");
        QuarantineDirectory = Path.Combine(ModelsDirectory, "quarantine");
        StateMigrationPath = Path.Combine(StateRootDirectory, "migration-v1.json");

        EnsureStateDirectories();
        MigrateLegacyState(legacyDocumentsModelsDirectory);
    }

    public string BaseDirectory { get; }
    public string ResolvedRootDirectory { get; }
    public string BundleRootDirectory { get; }
    public bool BundleRootOverrideActive { get; }
    public string? BundleRootOverridePath { get; }
    public string PathMode { get; }
    public string StateRootDirectory { get; }
    public bool StateRootOverrideActive { get; }
    public string? StateRootOverridePath { get; }
    public string StatePathMode { get; }
    public string RuntimeDirectory { get; }
    public string ConfigDirectory { get; }
    public string TemplatesDirectory { get; }
    public string WorkflowDefinitionsDirectory { get; }
    public string WrapperDefinitionsDirectory { get; }
    public string WrapperScriptsDirectory { get; }
    public string ModelsDirectory { get; }
    public string LogsDirectory { get; }
    public string DownloadsLogDirectory { get; }
    public string ChatDataDirectory { get; }
    public string UserTemplatesDirectory { get; }
    public string UserWorkflowsDirectory { get; }
    public string UserWrappersDirectory { get; }
    public string AgentConfigsDirectory { get; }
    public string WorkspacesDirectory { get; }
    public string WrapperRunsDirectory { get; }
    public string WorkflowBundlesDirectory { get; }
    public string WorkflowRunsDirectory { get; }
    public string QuarantineDirectory { get; }
    public string StateMigrationPath { get; }

    public string CatalogPath => Path.Combine(ModelsDirectory, "catalog.json");
    public string LibraryStatePath => Path.Combine(ModelsDirectory, "library-state.json");
    public string LanRoutingSettingsPath => Path.Combine(StateRootDirectory, "lan-routing-settings.json");
    public string UiStyleSettingsPath => Path.Combine(StateRootDirectory, "ui-style-settings.json");
    public string SplitWorkspaceLayoutPath => Path.Combine(StateRootDirectory, "split-workspace-layout.json");
    public string LanPeerRegistryPath => Path.Combine(StateRootDirectory, "lan-peer-registry.json");
    public string LanDiscoveryCachePath => Path.Combine(StateRootDirectory, "lan-discovery-cache.json");
    public string WorkspaceRegistryPath => Path.Combine(WorkspacesDirectory, "registry.yaml");

    public string GetDownloadLogPath(string downloadId) => Path.Combine(DownloadsLogDirectory, $"{downloadId}.ndjson");

    public string GetRuntimeExecutablePath()
    {
        return GetRuntimeExecutablePath("bitnet.cpp");
    }

    public string GetRuntimeExecutablePath(string? runtimeBackend)
    {
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? string.Equals(runtimeBackend, "llama.cpp", StringComparison.OrdinalIgnoreCase) ? "NexusCentral-GGUF-Runtime.cmd" : "NexusCentral-BitNet-Runtime.cmd"
            : string.Equals(runtimeBackend, "llama.cpp", StringComparison.OrdinalIgnoreCase) ? "NexusCentral-GGUF-Runtime" : "NexusCentral-BitNet-Runtime";

        return Path.Combine(RuntimeDirectory, fileName);
    }

    public string GetWebLabRootDirectory()
    {
        return Path.Combine(BundleRootDirectory, "NexusCentral-WebHost");
    }

    public string GetWebLabFrontendDirectory()
    {
        return Path.Combine(GetWebLabRootDirectory(), "frontend");
    }

    public string GetWebLabBackendDirectory()
    {
        return Path.Combine(GetWebLabRootDirectory(), "backend");
    }

    public string GetWebLabFrontendPackageJsonPath()
    {
        return Path.Combine(GetWebLabFrontendDirectory(), "package.json");
    }

    public string GetWebLabFrontendNodeModulesDirectory()
    {
        return Path.Combine(GetWebLabFrontendDirectory(), "node_modules");
    }

    public string GetWebLabBackendGemfilePath()
    {
        return Path.Combine(GetWebLabBackendDirectory(), "Gemfile");
    }

    public string GetWebLabBackendVendorBundleDirectory()
    {
        return Path.Combine(GetWebLabBackendDirectory(), "vendor", "bundle");
    }

    public static string GetDefaultStateRootDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                return Path.Combine(localAppData, DefaultStateDirectoryName);
            }
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(home))
        {
            return Path.Combine(Path.GetTempPath(), DefaultStateDirectoryName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(home, "Library", "Application Support", DefaultStateDirectoryName);
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
        {
            return Path.Combine(Path.GetFullPath(xdgDataHome), DefaultStateDirectoryName);
        }

        return Path.Combine(home, ".local", "share", DefaultStateDirectoryName);
    }

    private static string? ResolveBundleRootOverride(string? explicitOverride)
    {
        var configuredRoot = string.IsNullOrWhiteSpace(explicitOverride)
            ? Environment.GetEnvironmentVariable("NEXUSCENTRAL_BUNDLE_ROOT")
            : explicitOverride;

        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(configuredRoot);
            return Directory.Exists(fullPath) ? fullPath : null;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveBundleSubdirectory(string bundleRoot, string directoryName)
    {
        var direct = Path.Combine(bundleRoot, directoryName);
        if (Directory.Exists(direct))
        {
            return direct;
        }

        var desktopSource = Path.Combine(bundleRoot, "Desktop", directoryName);
        if (Directory.Exists(desktopSource))
        {
            return desktopSource;
        }

        return direct;
    }

    private static string? ResolveStateRootOverride(string? explicitOverride)
    {
        var configuredRoot = string.IsNullOrWhiteSpace(explicitOverride)
            ? Environment.GetEnvironmentVariable("NEXUSCENTRAL_STATE_ROOT")
            : explicitOverride;

        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            return null;
        }

        return Path.GetFullPath(configuredRoot);
    }

    private void EnsureStateDirectories()
    {
        Directory.CreateDirectory(StateRootDirectory);
        Directory.CreateDirectory(ModelsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(DownloadsLogDirectory);
        Directory.CreateDirectory(ChatDataDirectory);
        Directory.CreateDirectory(UserTemplatesDirectory);
        Directory.CreateDirectory(UserWorkflowsDirectory);
        Directory.CreateDirectory(UserWrappersDirectory);
        Directory.CreateDirectory(AgentConfigsDirectory);
        Directory.CreateDirectory(WorkspacesDirectory);
        Directory.CreateDirectory(WrapperRunsDirectory);
        Directory.CreateDirectory(WorkflowBundlesDirectory);
        Directory.CreateDirectory(WorkflowRunsDirectory);
        Directory.CreateDirectory(QuarantineDirectory);
    }

    private void MigrateLegacyState(string? legacyDocumentsModelsDirectory)
    {
        if (File.Exists(StateMigrationPath))
        {
            return;
        }

        var report = new StateMigrationReport
        {
            Version = MigrationVersion,
            TimestampUtc = DateTimeOffset.UtcNow,
        };

        var legacySources = new[]
        {
            ("legacy_documents_models", ResolveLegacyDocumentsModelsDirectory(legacyDocumentsModelsDirectory), ModelsDirectory),
            ("legacy_bundle_models", Path.Combine(BundleRootDirectory, "models"), ModelsDirectory),
            ("legacy_bundle_logs", Path.Combine(BundleRootDirectory, "logs"), LogsDirectory),
        };

        foreach (var (sourceId, sourcePath, destinationPath) in legacySources)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                continue;
            }

            report.Entries.Add(MigrateDirectory(sourceId, sourcePath, destinationPath));
        }

        File.WriteAllText(StateMigrationPath, JsonSerializer.Serialize(report, MigrationJsonOptions));
    }

    private string? ResolveLegacyDocumentsModelsDirectory(string? overridePath)
    {
        if (overridePath is not null)
        {
            return string.IsNullOrWhiteSpace(overridePath) ? null : Path.GetFullPath(overridePath);
        }

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documentsPath))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(home))
            {
                documentsPath = Path.Combine(home, "Documents");
            }
        }

        if (string.IsNullOrWhiteSpace(documentsPath))
        {
            return null;
        }

        return Path.Combine(documentsPath, "Models");
    }

    private StateMigrationEntry MigrateDirectory(string sourceId, string sourcePath, string destinationPath)
    {
        var entry = new StateMigrationEntry
        {
            SourceId = sourceId,
            SourcePath = sourcePath,
            DestinationPath = destinationPath,
            Status = "skipped",
        };

        try
        {
            var normalizedSource = Path.GetFullPath(sourcePath);
            var normalizedDestination = Path.GetFullPath(destinationPath);
            if (!Directory.Exists(normalizedSource) || PathsEqual(normalizedSource, normalizedDestination))
            {
                entry.Message = "Source directory did not require migration.";
                return entry;
            }

            foreach (var filePath in Directory.EnumerateFiles(normalizedSource, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(normalizedSource, filePath);
                var destinationFile = Path.Combine(normalizedDestination, relativePath);
                var destinationDirectory = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                if (File.Exists(destinationFile))
                {
                    entry.SkippedConflicts++;
                    entry.Messages.Add($"Skipped existing file: {relativePath}");
                    continue;
                }

                try
                {
                    File.Move(filePath, destinationFile);
                    entry.MovedFiles++;
                }
                catch (IOException)
                {
                    File.Copy(filePath, destinationFile, overwrite: false);
                    File.Delete(filePath);
                    entry.CopiedFiles++;
                }
            }

            RemoveEmptyDirectories(normalizedSource);
            if (!Directory.Exists(normalizedSource))
            {
                entry.SourceRemoved = true;
            }

            entry.Status = entry.MovedFiles > 0 || entry.CopiedFiles > 0
                ? "migrated"
                : entry.SkippedConflicts > 0
                    ? "conflict"
                    : "skipped";
            entry.Message = entry.Status switch
            {
                "migrated" => "Legacy data migrated into the new state root.",
                "conflict" => "Legacy files were left in place because destination files already existed.",
                _ => "No files needed migration.",
            };
            return entry;
        }
        catch (Exception ex)
        {
            entry.Status = "error";
            entry.Message = ex.Message;
            entry.Messages.Add(ex.ToString());
            return entry;
        }
    }

    private static void RemoveEmptyDirectories(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(rootDirectory))
        {
            RemoveEmptyDirectories(directory);
        }

        if (!Directory.EnumerateFileSystemEntries(rootDirectory).Any())
        {
            Directory.Delete(rootDirectory);
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private sealed class StateMigrationReport
    {
        public string Version { get; set; } = MigrationVersion;
        public DateTimeOffset TimestampUtc { get; set; }
        public List<StateMigrationEntry> Entries { get; set; } = new();
    }

    private sealed class StateMigrationEntry
    {
        public string SourceId { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public string Status { get; set; } = "skipped";
        public string Message { get; set; } = string.Empty;
        public int MovedFiles { get; set; }
        public int CopiedFiles { get; set; }
        public int SkippedConflicts { get; set; }
        public bool SourceRemoved { get; set; }
        public List<string> Messages { get; set; } = new();
    }
}
