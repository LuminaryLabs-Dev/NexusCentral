using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class WrapperCatalogService
{
    private readonly AppPathsService _paths;
    private readonly LogService _logs;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private readonly object _lock = new();
    private List<WrapperDefinition> _wrappers = new();

    public event Action? WrappersChanged;

    public WrapperCatalogService(AppPathsService paths, LogService logs)
    {
        _paths = paths;
        _logs = logs;
        Reload();
    }

    public IReadOnlyList<WrapperDefinition> GetWrappers()
    {
        lock (_lock)
        {
            return _wrappers.Select(Clone).ToList();
        }
    }

    public WrapperDefinition? FindById(string? wrapperId)
    {
        if (string.IsNullOrWhiteSpace(wrapperId))
        {
            return null;
        }

        lock (_lock)
        {
            var wrapper = _wrappers.FirstOrDefault(candidate => string.Equals(candidate.Id, wrapperId, StringComparison.OrdinalIgnoreCase));
            return wrapper is null ? null : Clone(wrapper);
        }
    }

    public void Reload()
    {
        var wrappers = new Dictionary<string, WrapperDefinition>(StringComparer.OrdinalIgnoreCase);
        LoadDirectory(_paths.WrapperDefinitionsDirectory, "built_in", wrappers);
        LoadDirectory(_paths.UserWrappersDirectory, "local", wrappers);

        lock (_lock)
        {
            _wrappers = wrappers.Values
                .OrderBy(wrapper => string.Equals(wrapper.Source, "local", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(wrapper => wrapper.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        WrappersChanged?.Invoke();
    }

    public async Task<WrapperValidationResult> ValidateWrapperAsync(string? wrapperId, string nexuscentralBaseUrl = "http://localhost:3005")
    {
        var wrapper = FindById(wrapperId);
        if (wrapper is null)
        {
            return new WrapperValidationResult
            {
                Ok = false,
                WrapperId = wrapperId ?? string.Empty,
                Errors = new List<string> { "Wrapper not found." }
            };
        }

        var result = new WrapperValidationResult
        {
            WrapperId = wrapper.Id
        };

        foreach (var error in ValidateDefinition(wrapper))
        {
            result.Errors.Add(error);
        }

        if (string.Equals(wrapper.Transport, "builtin", StringComparison.OrdinalIgnoreCase))
        {
            result.Ok = true;
            result.ResolvedCommand = "builtin";
        }
        else
        {
            result.ResolvedCommand = ResolveCommand(wrapper);
            result.ResolvedArgs = string.Equals(wrapper.IoContractId, "external_batched_agent_v1", StringComparison.OrdinalIgnoreCase)
                ? wrapper.ArgTemplates.Select(argument => ReplaceTokens(argument, wrapper)).ToList()
                : ResolveArgs(wrapper).ToList();
            if (string.IsNullOrWhiteSpace(result.ResolvedCommand))
            {
                result.Errors.Add("Could not resolve wrapper command.");
            }
            else if (!CommandExists(result.ResolvedCommand))
            {
                result.Errors.Add($"Command '{result.ResolvedCommand}' is not available.");
            }
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await client.GetAsync($"{nexuscentralBaseUrl.TrimEnd('/')}/nexuscentral/list_tools");
            result.ToolingReachable = response.IsSuccessStatusCode;
            if (!result.ToolingReachable)
            {
                result.Warnings.Add("NexusCentral tooling surface is not reachable.");
            }
        }
        catch (Exception ex)
        {
            result.ToolingReachable = false;
            result.Warnings.Add($"NexusCentral tooling check failed: {ex.Message}");
        }

        result.Ok = result.Errors.Count == 0;
        return result;
    }

    public async Task<WrapperImportResult> ImportWrapperAsync(string? manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return new WrapperImportResult
            {
                Ok = false,
                Errors = new List<string> { "Manifest path is required." }
            };
        }

        string fullManifestPath;
        try
        {
            fullManifestPath = Path.GetFullPath(manifestPath);
        }
        catch (Exception ex)
        {
            return new WrapperImportResult
            {
                Ok = false,
                Errors = new List<string> { $"Could not resolve manifest path: {ex.Message}" }
            };
        }

        if (!File.Exists(fullManifestPath))
        {
            return new WrapperImportResult
            {
                Ok = false,
                Errors = new List<string> { $"Manifest '{fullManifestPath}' was not found." }
            };
        }

        WrapperImportManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<WrapperImportManifest>(File.ReadAllText(fullManifestPath), _jsonOptions) ?? new WrapperImportManifest();
        }
        catch (Exception ex)
        {
            return new WrapperImportResult
            {
                Ok = false,
                Errors = new List<string> { $"Could not parse manifest '{fullManifestPath}': {ex.Message}" }
            };
        }

        var buildFolderPath = Path.GetDirectoryName(fullManifestPath) ?? string.Empty;
        var wrapper = new WrapperDefinition
        {
            Id = manifest.Id,
            Name = manifest.Name,
            Description = manifest.Description,
            Transport = string.IsNullOrWhiteSpace(manifest.Transport) ? "cli" : manifest.Transport,
            Command = manifest.Executable,
            Args = new List<string>(),
            Env = manifest.Env.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            SupportsChat = manifest.SupportsChat,
            SupportsArtifacts = manifest.SupportsArtifacts,
            SupportsTemplates = manifest.SupportsTemplates,
            SupportsHostedWeb = manifest.SupportsHostedWeb,
            AllowedTools = manifest.AllowedTools.ToList(),
            WebChatMode = manifest.WebChatMode,
            SupportsAttachments = manifest.SupportsAttachments,
            DefaultTimeoutSec = manifest.DefaultTimeoutSec,
            InputContract = manifest.InputContract.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            OutputContract = manifest.OutputContract.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Validation = manifest.Validation,
            IoContractId = manifest.IoContractId,
            TextInputRoles = manifest.TextInputRoles.ToList(),
            TextOutputRoles = manifest.TextOutputRoles.ToList(),
            ArgTemplates = manifest.ArgTemplates.ToList(),
            ImportMetadata = new WrapperImportMetadata
            {
                ManifestPath = fullManifestPath,
                BuildFolderPath = buildFolderPath,
                ExecutableRelativePath = manifest.Executable ?? string.Empty,
                ImportedFrom = "external_manifest",
                ImportedAtUtc = DateTimeOffset.UtcNow
            },
            SourceBundlePath = buildFolderPath,
            Source = "local",
            SourcePath = fullManifestPath,
        };

        NormalizeWrapper(wrapper, "local", fullManifestPath);
        var errors = ValidateDefinition(wrapper);
        if (!TryResolveImportedExecutable(wrapper, out _, out var executableError))
        {
            errors.Add(executableError);
        }

        if (errors.Count > 0)
        {
            return new WrapperImportResult
            {
                Ok = false,
                WrapperId = wrapper.Id,
                Wrapper = wrapper,
                Errors = errors
            };
        }

        var persistedPath = Path.Combine(_paths.UserWrappersDirectory, $"{wrapper.Id}.json");
        wrapper.SourcePath = persistedPath;
        NormalizeWrapper(wrapper, "local", persistedPath);

        try
        {
            File.WriteAllText(persistedPath, JsonSerializer.Serialize(wrapper, _jsonOptions));
        }
        catch (Exception ex)
        {
            return new WrapperImportResult
            {
                Ok = false,
                WrapperId = wrapper.Id,
                Wrapper = wrapper,
                Errors = new List<string> { $"Could not persist imported wrapper: {ex.Message}" }
            };
        }

        Reload();
        return new WrapperImportResult
        {
            Ok = true,
            WrapperId = wrapper.Id,
            PersistedPath = persistedPath,
            Wrapper = FindById(wrapper.Id) ?? wrapper
        };
    }

    public string ResolveCommand(WrapperDefinition wrapper)
    {
        if (IsImportedExternalWrapper(wrapper) &&
            TryResolveImportedExecutable(wrapper, out var importedExecutablePath, out _))
        {
            return importedExecutablePath;
        }

        var command = ReplaceTokens(wrapper.Command ?? string.Empty, wrapper);
        if (string.Equals(command, "{python}", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(wrapper.Command, "{python}", StringComparison.OrdinalIgnoreCase))
        {
            return ResolvePythonCommand();
        }

        if (!string.IsNullOrWhiteSpace(wrapper.SourceBundlePath) &&
            !Path.IsPathRooted(command) &&
            !string.IsNullOrWhiteSpace(wrapper.ImportMetadata.ManifestPath))
        {
            return Path.GetFullPath(Path.Combine(wrapper.SourceBundlePath, command));
        }

        return command;
    }

    public IReadOnlyList<string> ResolveArgs(WrapperDefinition wrapper) =>
        wrapper.Args.Select(argument => ReplaceTokens(argument, wrapper)).ToList();

    public IReadOnlyDictionary<string, string> ResolveEnvironment(WrapperDefinition wrapper) =>
        wrapper.Env.ToDictionary(pair => pair.Key, pair => ReplaceTokens(pair.Value, wrapper), StringComparer.OrdinalIgnoreCase);

    private void LoadDirectory(string directory, string source, Dictionary<string, WrapperDefinition> wrappers)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var wrapper = JsonSerializer.Deserialize<WrapperDefinition>(File.ReadAllText(path), _jsonOptions) ?? new WrapperDefinition();
                NormalizeWrapper(wrapper, source, path);
                wrappers[wrapper.Id] = wrapper;
            }
            catch (Exception ex)
            {
                _logs.Append("error", "wrappers", $"Failed to load wrapper '{path}': {ex.Message}");
            }
        }
    }

    private void NormalizeWrapper(WrapperDefinition wrapper, string source, string path)
    {
        wrapper.Id = string.IsNullOrWhiteSpace(wrapper.Id) ? Path.GetFileNameWithoutExtension(path) : wrapper.Id.Trim();
        wrapper.Name = string.IsNullOrWhiteSpace(wrapper.Name) ? wrapper.Id : wrapper.Name.Trim();
        wrapper.Description = wrapper.Description?.Trim() ?? string.Empty;
        wrapper.Transport = string.IsNullOrWhiteSpace(wrapper.Transport) ? "cli" : wrapper.Transport.Trim().ToLowerInvariant();
        wrapper.IoContractId = NormalizeIoContractId(wrapper.IoContractId);
        wrapper.Source = source;
        wrapper.SourcePath = path;
        wrapper.SourceBundlePath = wrapper.SourceBundlePath?.Trim() ?? string.Empty;
        wrapper.Args ??= new List<string>();
        wrapper.Env ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        wrapper.InputContract ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        wrapper.OutputContract ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        wrapper.AllowedTools ??= new List<string>();
        wrapper.TextInputRoles ??= new List<string>();
        wrapper.TextOutputRoles ??= new List<string>();
        wrapper.ArgTemplates ??= new List<string>();
        wrapper.Settings ??= new List<WrapperSettingDefinition>();
        foreach (var setting in wrapper.Settings)
        {
            setting.Id = setting.Id?.Trim() ?? string.Empty;
            setting.Label = string.IsNullOrWhiteSpace(setting.Label) ? setting.Id : setting.Label.Trim();
            setting.Kind = string.IsNullOrWhiteSpace(setting.Kind) ? "text" : setting.Kind.Trim().ToLowerInvariant();
            setting.DefaultValue = setting.DefaultValue?.Trim() ?? string.Empty;
            setting.Value = string.IsNullOrWhiteSpace(setting.Value) ? setting.DefaultValue : setting.Value.Trim();
            setting.Description = setting.Description?.Trim() ?? string.Empty;
            setting.Options ??= new List<string>();
        }
        wrapper.ImportMetadata ??= new WrapperImportMetadata();
        wrapper.ImportMetadata.ManifestPath = wrapper.ImportMetadata.ManifestPath?.Trim() ?? string.Empty;
        wrapper.ImportMetadata.BuildFolderPath = wrapper.ImportMetadata.BuildFolderPath?.Trim() ?? string.Empty;
        wrapper.ImportMetadata.ExecutableRelativePath = wrapper.ImportMetadata.ExecutableRelativePath?.Trim() ?? string.Empty;
        wrapper.ImportMetadata.ImportedFrom = wrapper.ImportMetadata.ImportedFrom?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(wrapper.SourceBundlePath) && !string.IsNullOrWhiteSpace(wrapper.ImportMetadata.BuildFolderPath))
        {
            wrapper.SourceBundlePath = wrapper.ImportMetadata.BuildFolderPath;
        }

        if (string.IsNullOrWhiteSpace(wrapper.ImportMetadata.BuildFolderPath) && !string.IsNullOrWhiteSpace(wrapper.SourceBundlePath))
        {
            wrapper.ImportMetadata.BuildFolderPath = wrapper.SourceBundlePath;
        }

        if (string.IsNullOrWhiteSpace(wrapper.ImportMetadata.ExecutableRelativePath))
        {
            wrapper.ImportMetadata.ExecutableRelativePath = wrapper.Command?.Trim() ?? string.Empty;
        }

        if (string.Equals(wrapper.IoContractId, "external_batched_agent_v1", StringComparison.OrdinalIgnoreCase) &&
            !wrapper.InputContract.ContainsKey("root_directory"))
        {
            wrapper.InputContract["root_directory"] = "Required workspace root directory for the active Lab workspace.";
        }

        wrapper.WebChatMode = string.IsNullOrWhiteSpace(wrapper.WebChatMode) ? "wrapper_run" : wrapper.WebChatMode.Trim();
        if (!wrapper.SupportsHostedWeb && wrapper.SupportsChat)
        {
            wrapper.SupportsHostedWeb = true;
        }

        var definitionErrors = ValidateDefinition(wrapper);
        if (definitionErrors.Count > 0)
        {
            wrapper.IsAvailable = false;
            wrapper.AvailabilityMessage = string.Join(" ", definitionErrors);
            return;
        }

        if (string.Equals(wrapper.Transport, "builtin", StringComparison.OrdinalIgnoreCase))
        {
            wrapper.IsAvailable = true;
            wrapper.AvailabilityMessage = "Built-in";
            return;
        }

        var resolvedCommand = ResolveCommand(wrapper);
        wrapper.IsAvailable = !string.IsNullOrWhiteSpace(resolvedCommand) && CommandExists(resolvedCommand);
        wrapper.AvailabilityMessage = wrapper.IsAvailable ? "Ready" : "Command unavailable";
    }

    private List<string> ValidateDefinition(WrapperDefinition wrapper)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(wrapper.Id))
        {
            errors.Add("Missing wrapper id.");
        }

        if (string.IsNullOrWhiteSpace(wrapper.Name))
        {
            errors.Add("Missing wrapper name.");
        }

        if (string.Equals(wrapper.IoContractId, "external_batched_agent_v1", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(wrapper.Transport, "cli", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Imported wrapper contract external_batched_agent_v1 requires transport 'cli'.");
            }

            if (string.IsNullOrWhiteSpace(wrapper.Command))
            {
                errors.Add("Imported wrapper is missing command/executable.");
            }

            if (wrapper.TextInputRoles.Count == 0)
            {
                errors.Add("Imported wrapper requires text_input_roles.");
            }

            if (wrapper.TextOutputRoles.Count == 0)
            {
                errors.Add("Imported wrapper requires text_output_roles.");
            }

            if (wrapper.ArgTemplates.Count == 0)
            {
                errors.Add("Imported wrapper requires arg_templates.");
            }

            if (wrapper.ArgTemplates.Count > 0 &&
                !wrapper.ArgTemplates.Any(template => template.Contains("{input_json}", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("Imported wrapper arg_templates must include {input_json}.");
            }

            if (wrapper.ArgTemplates.Count > 0 &&
                !wrapper.ArgTemplates.Any(template => template.Contains("{output_json}", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("Imported wrapper arg_templates must include {output_json}.");
            }

            if (!wrapper.InputContract.ContainsKey("root_directory"))
            {
                errors.Add("Imported wrapper input_contract must declare root_directory.");
            }

            if (string.IsNullOrWhiteSpace(wrapper.SourceBundlePath) && string.IsNullOrWhiteSpace(wrapper.ImportMetadata.BuildFolderPath))
            {
                errors.Add("Imported wrapper requires source_bundle_path.");
            }

            if (!IsImportedExternalWrapper(wrapper))
            {
                errors.Add("Imported wrapper metadata is incomplete.");
            }
        }

        return errors;
    }

    private string ReplaceTokens(string value, WrapperDefinition? wrapper = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace("{resolved_root}", _paths.ResolvedRootDirectory, StringComparison.OrdinalIgnoreCase)
            .Replace("{wrapper_scripts_dir}", _paths.WrapperScriptsDirectory, StringComparison.OrdinalIgnoreCase)
            .Replace("{logs_dir}", _paths.LogsDirectory, StringComparison.OrdinalIgnoreCase)
            .Replace("{source_bundle_path}", wrapper?.SourceBundlePath ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{python}", ResolvePythonCommand(), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIoContractId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "nexus_wrapper_v1" : value.Trim();

    private static string NormalizeMemoryStrategy(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "chat_context" : value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "none" => "none",
            "off" => "none",
            "wrapper_owned" => "wrapper_owned",
            "chat_context" => "chat_context",
            _ => "chat_context"
        };
    }

    private static bool CommandExists(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        if (command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(command);
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var searchExtensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : new[] { string.Empty };

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in searchExtensions)
            {
                var candidate = Path.Combine(directory, command.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? command : $"{command}{extension}");
                if (File.Exists(candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string ResolvePythonCommand()
    {
        foreach (var candidate in OperatingSystem.IsWindows()
                     ? new[] { "python", "py" }
                     : new[] { "python3", "python" })
        {
            if (CanExecute(candidate))
            {
                return candidate;
            }
        }

        return OperatingSystem.IsWindows() ? "python" : "python3";
    }

    private static bool CanExecute(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(command)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { "--version" }
            });
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static WrapperDefinition Clone(WrapperDefinition wrapper)
    {
        return new WrapperDefinition
        {
            Id = wrapper.Id,
            Name = wrapper.Name,
            Description = wrapper.Description,
            Transport = wrapper.Transport,
            Command = wrapper.Command,
            Args = wrapper.Args.ToList(),
            Env = wrapper.Env.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            SupportsChat = wrapper.SupportsChat,
            SupportsArtifacts = wrapper.SupportsArtifacts,
            SupportsTemplates = wrapper.SupportsTemplates,
            SupportsHostedWeb = wrapper.SupportsHostedWeb,
            AllowedTools = wrapper.AllowedTools.ToList(),
            WebChatMode = wrapper.WebChatMode,
            SupportsAttachments = wrapper.SupportsAttachments,
            DefaultTimeoutSec = wrapper.DefaultTimeoutSec,
            InputContract = wrapper.InputContract.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            OutputContract = wrapper.OutputContract.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Validation = wrapper.Validation,
            IoContractId = wrapper.IoContractId,
            TextInputRoles = wrapper.TextInputRoles.ToList(),
            TextOutputRoles = wrapper.TextOutputRoles.ToList(),
            ArgTemplates = wrapper.ArgTemplates.ToList(),
            Settings = wrapper.Settings.Select(setting => new WrapperSettingDefinition
            {
                Id = setting.Id,
                Label = setting.Label,
                Kind = setting.Kind,
                DefaultValue = setting.DefaultValue,
                Value = setting.Value,
                Exposed = setting.Exposed,
                Description = setting.Description,
                Options = setting.Options.ToList(),
                Min = setting.Min,
                Max = setting.Max
            }).ToList(),
            ImportMetadata = new WrapperImportMetadata
            {
                ManifestPath = wrapper.ImportMetadata.ManifestPath,
                BuildFolderPath = wrapper.ImportMetadata.BuildFolderPath,
                ExecutableRelativePath = wrapper.ImportMetadata.ExecutableRelativePath,
                ImportedFrom = wrapper.ImportMetadata.ImportedFrom,
                ImportedAtUtc = wrapper.ImportMetadata.ImportedAtUtc
            },
            SourceBundlePath = wrapper.SourceBundlePath,
            Source = wrapper.Source,
            SourcePath = wrapper.SourcePath,
            IsAvailable = wrapper.IsAvailable,
            AvailabilityMessage = wrapper.AvailabilityMessage
        };
    }

    private static bool IsImportedExternalWrapper(WrapperDefinition wrapper) =>
        string.Equals(wrapper.IoContractId, "external_batched_agent_v1", StringComparison.OrdinalIgnoreCase) &&
        (!string.IsNullOrWhiteSpace(wrapper.ImportMetadata.ManifestPath) ||
         !string.IsNullOrWhiteSpace(wrapper.ImportMetadata.BuildFolderPath) ||
         !string.IsNullOrWhiteSpace(wrapper.SourceBundlePath));

    private static string ResolveBuildFolder(WrapperDefinition wrapper)
    {
        if (!string.IsNullOrWhiteSpace(wrapper.SourceBundlePath))
        {
            return wrapper.SourceBundlePath;
        }

        if (!string.IsNullOrWhiteSpace(wrapper.ImportMetadata.BuildFolderPath))
        {
            return wrapper.ImportMetadata.BuildFolderPath;
        }

        if (!string.IsNullOrWhiteSpace(wrapper.ImportMetadata.ManifestPath))
        {
            return Path.GetDirectoryName(wrapper.ImportMetadata.ManifestPath) ?? string.Empty;
        }

        return string.Empty;
    }

    private static bool TryResolveImportedExecutable(WrapperDefinition wrapper, out string resolvedCommand, out string error)
    {
        resolvedCommand = string.Empty;
        error = string.Empty;

        var buildFolder = ResolveBuildFolder(wrapper);
        if (string.IsNullOrWhiteSpace(buildFolder))
        {
            error = "Imported wrapper requires a build folder.";
            return false;
        }

        var executableRelativePath = string.IsNullOrWhiteSpace(wrapper.ImportMetadata.ExecutableRelativePath)
            ? wrapper.Command?.Trim() ?? string.Empty
            : wrapper.ImportMetadata.ExecutableRelativePath.Trim();
        if (string.IsNullOrWhiteSpace(executableRelativePath))
        {
            error = "Imported wrapper is missing an executable path.";
            return false;
        }

        if (Path.IsPathRooted(executableRelativePath))
        {
            error = "Imported wrapper executable must be relative to the build folder.";
            return false;
        }

        if (executableRelativePath.Contains('{', StringComparison.Ordinal) || executableRelativePath.Contains('}', StringComparison.Ordinal))
        {
            error = "Imported wrapper executable cannot use runtime tokens.";
            return false;
        }

        var fullBuildFolder = Path.GetFullPath(buildFolder);
        var candidate = Path.GetFullPath(Path.Combine(fullBuildFolder, executableRelativePath));
        var normalizedBuildFolder = EnsureTrailingSeparator(fullBuildFolder);
        if (!candidate.StartsWith(normalizedBuildFolder, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(candidate, fullBuildFolder, StringComparison.OrdinalIgnoreCase))
        {
            error = "Imported wrapper executable must stay inside the build folder.";
            return false;
        }

        if (!File.Exists(candidate))
        {
            error = $"Imported wrapper executable '{candidate}' was not found.";
            return false;
        }

        resolvedCommand = candidate;
        return true;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : $"{path}{Path.DirectorySeparatorChar}";
    }
}
