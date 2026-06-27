using System.Runtime.InteropServices;
using System.Text.Json;
using NexusCentral_Avalonia.Services;
using Xunit;

namespace NexusCentral_Triceratops.Tests;

public sealed class AppPathsServiceTests : IDisposable
{
    private readonly string? _previousBundleRoot = Environment.GetEnvironmentVariable("NEXUSCENTRAL_BUNDLE_ROOT");
    private readonly string? _previousStateRoot = Environment.GetEnvironmentVariable("NEXUSCENTRAL_STATE_ROOT");
    private readonly List<string> _temporaryDirectories = new();

    [Fact]
    public void GetDefaultStateRootDirectory_UsesExpectedOsSpecificBase()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusCentral-Triceratops")
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? Path.Combine(home, "Library", "Application Support", "NexusCentral-Triceratops")
                : Path.Combine(
                    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XDG_DATA_HOME"))
                        ? Path.Combine(home, ".local", "share")
                        : Path.GetFullPath(Environment.GetEnvironmentVariable("XDG_DATA_HOME")!),
                    "NexusCentral-Triceratops");

        Assert.Equal(expected, AppPathsService.GetDefaultStateRootDirectory());
    }

    [Fact]
    public void Constructor_UsesEnvironmentOverrides_AndKeepsMutablePathsUnderStateRoot()
    {
        var bundleRoot = CreateTempDirectory();
        var stateRoot = CreateTempDirectory();
        Environment.SetEnvironmentVariable("NEXUSCENTRAL_BUNDLE_ROOT", bundleRoot);
        Environment.SetEnvironmentVariable("NEXUSCENTRAL_STATE_ROOT", stateRoot);

        var service = new AppPathsService(CreateTempDirectory(), legacyDocumentsModelsDirectory: string.Empty);

        Assert.Equal(bundleRoot, service.BundleRootDirectory);
        Assert.Equal(stateRoot, service.StateRootDirectory);
        Assert.True(service.BundleRootOverrideActive);
        Assert.True(service.StateRootOverrideActive);
        Assert.Equal(Path.Combine(stateRoot, "models"), service.ModelsDirectory);
        Assert.Equal(Path.Combine(stateRoot, "logs"), service.LogsDirectory);
        Assert.StartsWith(stateRoot, service.CatalogPath, StringComparison.Ordinal);
        Assert.StartsWith(stateRoot, service.LibraryStatePath, StringComparison.Ordinal);
        Assert.StartsWith(stateRoot, service.LanRoutingSettingsPath, StringComparison.Ordinal);
        Assert.StartsWith(stateRoot, service.LanPeerRegistryPath, StringComparison.Ordinal);
        Assert.StartsWith(stateRoot, service.LanDiscoveryCachePath, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_MigratesLegacyDocumentsModelsIntoStateRoot()
    {
        var legacyModels = CreateTempDirectory();
        var stateRoot = CreateTempDirectory();
        var modelPath = WriteFile(Path.Combine(legacyModels, "demo.gguf"), "legacy-model");
        WriteFile(Path.Combine(legacyModels, "catalog.json"), "{\"entries\":[]}");
        WriteFile(Path.Combine(legacyModels, "quarantine", "bad.gguf"), "bad-model");

        var service = new AppPathsService(
            baseDirectory: CreateTempDirectory(),
            stateRootOverridePath: stateRoot,
            legacyDocumentsModelsDirectory: legacyModels);

        Assert.False(File.Exists(modelPath));
        Assert.Equal("legacy-model", File.ReadAllText(Path.Combine(service.ModelsDirectory, "demo.gguf")));
        Assert.True(File.Exists(Path.Combine(service.ModelsDirectory, "catalog.json")));
        Assert.True(File.Exists(Path.Combine(service.QuarantineDirectory, "bad.gguf")));
        Assert.True(File.Exists(service.StateMigrationPath));
    }

    [Fact]
    public void Constructor_MigratesLegacyBundleStateIntoStateRoot()
    {
        var bundleRoot = CreateTempDirectory();
        var stateRoot = CreateTempDirectory();
        var bundleModel = WriteFile(Path.Combine(bundleRoot, "models", "bundle.gguf"), "bundle-model");
        var bundleLog = WriteFile(Path.Combine(bundleRoot, "logs", "downloads", "job.ndjson"), "{\"event\":\"done\"}");

        var service = new AppPathsService(
            baseDirectory: CreateTempDirectory(),
            bundleRootOverridePath: bundleRoot,
            stateRootOverridePath: stateRoot,
            legacyDocumentsModelsDirectory: string.Empty);

        Assert.False(File.Exists(bundleModel));
        Assert.False(File.Exists(bundleLog));
        Assert.Equal("bundle-model", File.ReadAllText(Path.Combine(service.ModelsDirectory, "bundle.gguf")));
        Assert.True(File.Exists(Path.Combine(service.DownloadsLogDirectory, "job.ndjson")));
    }

    [Fact]
    public void Constructor_DoesNotOverwriteExistingDestinationFiles()
    {
        var legacyModels = CreateTempDirectory();
        var stateRoot = CreateTempDirectory();
        WriteFile(Path.Combine(legacyModels, "shared.gguf"), "old-copy");
        var destinationFile = WriteFile(Path.Combine(stateRoot, "models", "shared.gguf"), "new-copy");

        var service = new AppPathsService(
            baseDirectory: CreateTempDirectory(),
            stateRootOverridePath: stateRoot,
            legacyDocumentsModelsDirectory: legacyModels);

        Assert.Equal("new-copy", File.ReadAllText(destinationFile));
        Assert.True(File.Exists(Path.Combine(legacyModels, "shared.gguf")));

        using var document = JsonDocument.Parse(File.ReadAllText(service.StateMigrationPath));
        var conflictEntry = document.RootElement
            .GetProperty("Entries")
            .EnumerateArray()
            .First(entry => entry.GetProperty("SourceId").GetString() == "legacy_documents_models");

        Assert.Equal("conflict", conflictEntry.GetProperty("Status").GetString());
        Assert.Equal(1, conflictEntry.GetProperty("SkippedConflicts").GetInt32());
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("NEXUSCENTRAL_BUNDLE_ROOT", _previousBundleRoot);
        Environment.SetEnvironmentVariable("NEXUSCENTRAL_STATE_ROOT", _previousStateRoot);

        foreach (var directory in _temporaryDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "nexuscentral-triceratops-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _temporaryDirectories.Add(path);
        return path;
    }

    private static string WriteFile(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
        return path;
    }
}
