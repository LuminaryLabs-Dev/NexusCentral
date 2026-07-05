using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NexusCentral.V1.Core.Config;
using NexusCentral.V1.Core.Logging;
using NexusCentral.V1.Core.State;
using NexusCentral.V1.Integrations.Codex;

namespace NexusCentral.V1.Features.Harnesses;

public sealed class HarnessImporterService : IHarnessImporterService
{
    private static readonly JsonSerializerOptions RegistryJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppPathSettings _paths;
    private readonly JsonFileStore _json;
    private readonly ICodexCliDiscovery _codexCli;
    private readonly ICodexCliQuestionRunner _codexQuestions;
    private readonly IAppLogService _logs;
    private readonly object _gate = new();
    private HarnessRegistry _registry = new(string.Empty, new List<HarnessDefinition>());

    public HarnessImporterService(AppPathSettings paths, JsonFileStore json, ICodexCliDiscovery codexCli, IAppLogService logs)
        : this(paths, json, codexCli, new CodexCliQuestionRunner(), logs)
    {
    }

    public HarnessImporterService(
        AppPathSettings paths,
        JsonFileStore json,
        ICodexCliDiscovery codexCli,
        ICodexCliQuestionRunner codexQuestions,
        IAppLogService logs)
    {
        _paths = paths;
        _json = json;
        _codexCli = codexCli;
        _codexQuestions = codexQuestions;
        _logs = logs;
        _paths.EnsureDirectories();
        LoadRegistry();
    }

    public event Action<HarnessImporterSnapshot>? StateChanged;

    public HarnessImporterSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var harnesses = _registry.Harnesses.OrderBy(harness => harness.Name).ToList();
            var active = harnesses.FirstOrDefault(harness => string.Equals(harness.Id, _registry.ActiveHarnessId, StringComparison.OrdinalIgnoreCase));
            return CreateSnapshot(active, harnesses);
        }
    }

    public async Task<HarnessDefinition> DiscoverCodexCliAsync(CancellationToken cancellationToken = default)
    {
        var result = await _codexCli.DiscoverAsync(cancellationToken);
        var existing = FindHarness("codex-cli");
        var existingEnabled = existing?.Capabilities
            .Where(capability => capability.IsEnabled)
            .Select(capability => capability.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? DefaultEnabledCodexCapabilityIds(result);

        var capabilities = CreateCodexCliCapabilities(result.Commands)
            .Select(capability => capability with { IsEnabled = existingEnabled.Contains(capability.Id) })
            .ToList();

        var health = result.Found ? HarnessHealth.Discovered : HarnessHealth.Missing;
        var harness = new HarnessDefinition(
            "codex-cli",
            "Codex CLI",
            HarnessKind.CodexCli,
            result.ExecutablePath,
            result.Version,
            health,
            DateTimeOffset.UtcNow,
            result.Message,
            result.Commands,
            result.ExecOptions,
            capabilities,
            CreateCodexCliWrapperEndpoints());

        await UpsertHarnessAsync(harness, cancellationToken);
        _logs.Append(result.Found ? "info" : "warn", "harnesses", result.Message, harness.Id);
        return harness;
    }

    public async Task<HarnessQuestionResult> AskReadOnlyAsync(HarnessQuestionRequest request, CancellationToken cancellationToken = default)
    {
        var harnessId = string.IsNullOrWhiteSpace(request.HarnessId)
            ? GetSnapshot().ActiveHarness?.Id ?? string.Empty
            : request.HarnessId.Trim();

        var harness = FindHarness(harnessId);
        if (harness is null)
        {
            return CreateQuestionFailure(request, harnessId, "Harness was not found.");
        }

        if (harness.Kind != HarnessKind.CodexCli)
        {
            return CreateQuestionFailure(request, harnessId, "Only Codex CLI harnesses can answer through this endpoint.");
        }

        if (harness.Health != HarnessHealth.Discovered)
        {
            return CreateQuestionFailure(request, harnessId, "Codex CLI harness is not discovered.");
        }

        if (!IsCapabilityEnabled(harness, "codex.cli.exec.readonly"))
        {
            return CreateQuestionFailure(request, harnessId, "Read-only exec capability is not enabled.");
        }

        var endpointRoute = ResolveEndpointRoute(harness, "codex.cli.exec.readonly");
        var result = await _codexQuestions.AskReadOnlyAsync(
            new CodexCliQuestionRequest(
                harness.CommandPath,
                request.WorkingDirectory,
                request.Question,
                request.Timeout),
            cancellationToken);

        _logs.Append(result.Ok ? "info" : "warn", "harnesses", result.Message, harness.Id);

        return new HarnessQuestionResult(
            result.Ok,
            harness.Id,
            endpointRoute,
            request.Question,
            result.Answer,
            result.ExitCode,
            result.Message,
            result.Duration);
    }

    public async Task<HarnessDefinition> ImportHarnessAsync(HarnessImportRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Harness name is required.");
        }

        var id = string.IsNullOrWhiteSpace(request.Id)
            ? CreateHarnessId(request.Name, request.CommandPath)
            : request.Id.Trim();

        var harness = new HarnessDefinition(
            id,
            request.Name.Trim(),
            request.Kind,
            request.CommandPath.Trim(),
            request.Version.Trim(),
            HarnessHealth.Discovered,
            DateTimeOffset.UtcNow,
            "Harness imported.",
            Array.Empty<string>(),
            Array.Empty<string>(),
            request.Capabilities,
            request.ProposedEndpoints);

        await UpsertHarnessAsync(harness, cancellationToken);
        _logs.Append("info", "harnesses", $"Imported harness '{harness.Name}'.", harness.Id);
        return harness;
    }

    public async Task<HarnessDefinition> ImportManifestAsync(string manifestPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new InvalidOperationException("Harness manifest path is required.");
        }

        var fullPath = Path.GetFullPath(manifestPath.Trim());
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Harness manifest was not found.", fullPath);
        }

        await using var stream = File.OpenRead(fullPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var name = ReadRequiredString(root, "name");
        var id = ReadString(root, "id");
        var kindText = ReadString(root, "kind");
        var kind = Enum.TryParse<HarnessKind>(kindText, ignoreCase: true, out var parsedKind)
            ? parsedKind
            : HarnessKind.Manual;
        var version = ReadString(root, "version");
        var workingDirectory = ReadString(root, "workingDirectory");
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            workingDirectory = Path.GetDirectoryName(fullPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        var command = ReadRequiredString(root, "command");
        var commandPath = ComposeManifestCommand(workingDirectory, ReadEnvironment(root), command);
        var request = new HarnessImportRequest(
            id,
            name,
            kind,
            commandPath,
            string.IsNullOrWhiteSpace(version) ? "manifest" : version,
            ReadCapabilities(root),
            ReadEndpoints(root));
        var harness = await ImportHarnessAsync(request, cancellationToken);
        _logs.Append("info", "harnesses", $"Imported harness manifest '{Path.GetFileName(fullPath)}'.", harness.Id);
        return harness;
    }

    public Task<IReadOnlyList<HarnessManifestCandidate>> DiscoverManifestsAsync(string rootDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new InvalidOperationException("Harness search root is required.");
        }

        var root = Path.GetFullPath(rootDirectory.Trim());
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Harness search root was not found: {root}");
        }

        var candidates = new List<HarnessManifestCandidate>();
        foreach (var manifestPath in EnumerateManifestFiles(root).Take(100))
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidates.Add(ReadManifestCandidate(manifestPath));
        }

        return Task.FromResult<IReadOnlyList<HarnessManifestCandidate>>(
            candidates
                .OrderBy(candidate => candidate.Name)
                .ThenBy(candidate => candidate.ManifestPath)
                .ToList());
    }

    public async Task<bool> SelectHarnessAsync(string harnessId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(harnessId))
        {
            return false;
        }

        var selected = false;
        lock (_gate)
        {
            selected = _registry.Harnesses.Any(harness => string.Equals(harness.Id, harnessId, StringComparison.OrdinalIgnoreCase));
            if (selected)
            {
                _registry = _registry with { ActiveHarnessId = harnessId };
            }
        }

        if (!selected)
        {
            return false;
        }

        await SaveRegistryAsync(cancellationToken);
        PublishStateChanged();
        return true;
    }

    public async Task<bool> SetCapabilityEnabledAsync(string harnessId, string capabilityId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        HarnessDefinition? updated = null;
        lock (_gate)
        {
            var harness = _registry.Harnesses.FirstOrDefault(candidate => string.Equals(candidate.Id, harnessId, StringComparison.OrdinalIgnoreCase));
            if (harness is null || !harness.Capabilities.Any(capability => string.Equals(capability.Id, capabilityId, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            updated = harness with
            {
                Capabilities = harness.Capabilities
                    .Select(capability => string.Equals(capability.Id, capabilityId, StringComparison.OrdinalIgnoreCase)
                        ? capability with { IsEnabled = isEnabled }
                        : capability)
                    .ToList()
            };

            _registry.Harnesses.RemoveAll(candidate => string.Equals(candidate.Id, harnessId, StringComparison.OrdinalIgnoreCase));
            _registry.Harnesses.Add(updated);
        }

        await SaveRegistryAsync(cancellationToken);
        _logs.Append("info", "harnesses", $"{updated!.Name} capability {(isEnabled ? "enabled" : "disabled")}: {capabilityId}.", updated.Id);
        PublishStateChanged();
        return true;
    }

    public async Task<bool> RemoveHarnessAsync(string harnessId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(harnessId))
        {
            return false;
        }

        HarnessDefinition? removed = null;
        lock (_gate)
        {
            removed = _registry.Harnesses.FirstOrDefault(candidate => string.Equals(candidate.Id, harnessId, StringComparison.OrdinalIgnoreCase));
            if (removed is null)
            {
                return false;
            }

            _registry.Harnesses.RemoveAll(candidate => string.Equals(candidate.Id, harnessId, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(_registry.ActiveHarnessId, harnessId, StringComparison.OrdinalIgnoreCase))
            {
                _registry = _registry with { ActiveHarnessId = _registry.Harnesses.OrderBy(harness => harness.Name).FirstOrDefault()?.Id ?? string.Empty };
            }
        }

        await SaveRegistryAsync(cancellationToken);
        _logs.Append("info", "harnesses", $"Removed harness '{removed.Name}'.", removed.Id);
        PublishStateChanged();
        return true;
    }

    private HarnessDefinition? FindHarness(string harnessId)
    {
        lock (_gate)
        {
            return _registry.Harnesses.FirstOrDefault(harness => string.Equals(harness.Id, harnessId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task UpsertHarnessAsync(HarnessDefinition harness, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _registry.Harnesses.RemoveAll(candidate => string.Equals(candidate.Id, harness.Id, StringComparison.OrdinalIgnoreCase));
            _registry.Harnesses.Add(harness);
            if (string.IsNullOrWhiteSpace(_registry.ActiveHarnessId))
            {
                _registry = _registry with { ActiveHarnessId = harness.Id };
            }
        }

        await SaveRegistryAsync(cancellationToken);
        PublishStateChanged();
    }

    private void LoadRegistry()
    {
        if (!File.Exists(_paths.HarnessRegistryPath))
        {
            _registry = new HarnessRegistry(string.Empty, new List<HarnessDefinition>());
            return;
        }

        using var stream = File.OpenRead(_paths.HarnessRegistryPath);
        _registry = JsonSerializer.Deserialize<HarnessRegistry>(stream, RegistryJsonOptions)
            ?? new HarnessRegistry(string.Empty, new List<HarnessDefinition>());
    }

    private async Task SaveRegistryAsync(CancellationToken cancellationToken)
    {
        HarnessRegistry snapshot;
        lock (_gate)
        {
            snapshot = _registry with { Harnesses = _registry.Harnesses.OrderBy(harness => harness.Name).ToList() };
        }

        await _json.WriteAsync(_paths.HarnessRegistryPath, snapshot, cancellationToken);
    }

    private void PublishStateChanged()
    {
        StateChanged?.Invoke(GetSnapshot());
    }

    private static IReadOnlyDictionary<string, string> ReadEnvironment(JsonElement root)
    {
        if (!root.TryGetProperty("environment", out var environment) || environment.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>();
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in environment.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                values[property.Name] = property.Value.ToString();
            }
        }

        return values;
    }

    private static IEnumerable<string> EnumerateManifestFiles(string rootDirectory)
    {
        var pending = new Stack<string>();
        pending.Push(rootDirectory);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            string[] manifests;
            try
            {
                manifests = Directory.GetFiles(current, "harness.manifest.json", SearchOption.TopDirectoryOnly);
            }
            catch (Exception) when (IsDirectoryEnumerationException())
            {
                continue;
            }

            foreach (var manifest in manifests)
            {
                yield return manifest;
            }

            string[] children;
            try
            {
                children = Directory.GetDirectories(current);
            }
            catch (Exception) when (IsDirectoryEnumerationException())
            {
                continue;
            }

            foreach (var child in children)
            {
                pending.Push(child);
            }
        }

        static bool IsDirectoryEnumerationException() => true;
    }

    private static HarnessManifestCandidate ReadManifestCandidate(string manifestPath)
    {
        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var name = ReadString(root, "name");
            var id = ReadString(root, "id");
            var command = ReadString(root, "command");
            return new HarnessManifestCandidate(
                manifestPath,
                id,
                string.IsNullOrWhiteSpace(name) ? Path.GetFileName(Path.GetDirectoryName(manifestPath)) ?? "Harness" : name,
                ReadString(root, "kind"),
                ReadString(root, "version"),
                command,
                string.IsNullOrWhiteSpace(command) ? "Manifest is missing command." : "Ready to import.");
        }
        catch (Exception exc)
        {
            return new HarnessManifestCandidate(
                manifestPath,
                string.Empty,
                Path.GetFileName(Path.GetDirectoryName(manifestPath)) ?? "Harness",
                string.Empty,
                string.Empty,
                string.Empty,
                $"Invalid manifest: {exc.Message}");
        }
    }

    private static IReadOnlyList<HarnessCapabilityDefinition> ReadCapabilities(JsonElement root)
    {
        if (!root.TryGetProperty("capabilities", out var capabilities) || capabilities.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<HarnessCapabilityDefinition>();
        }

        return capabilities
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new HarnessCapabilityDefinition(
                ReadRequiredString(item, "id"),
                ReadRequiredString(item, "name"),
                ReadString(item, "description"),
                ReadBool(item, "enabled", defaultValue: true),
                ReadBool(item, "requiresApproval", defaultValue: false)))
            .ToList();
    }

    private static IReadOnlyList<HarnessEndpointDefinition> ReadEndpoints(JsonElement root)
    {
        if (!root.TryGetProperty("endpoints", out var endpoints) || endpoints.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<HarnessEndpointDefinition>();
        }

        return endpoints
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new HarnessEndpointDefinition(
                ReadString(item, "method").ToUpperInvariant(),
                ReadRequiredString(item, "route"),
                ReadString(item, "capabilityId"),
                ReadString(item, "safety"),
                ReadString(item, "description")))
            .ToList();
    }

    private static string ComposeManifestCommand(string workingDirectory, IReadOnlyDictionary<string, string> environment, string command)
    {
        var prefix = new StringBuilder();
        prefix.Append("cd ");
        prefix.Append(ShellQuote(workingDirectory));
        prefix.Append(" && ");

        foreach (var item in environment.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            prefix.Append(item.Key);
            prefix.Append('=');
            prefix.Append(ShellQuote(item.Value));
            prefix.Append(' ');
        }

        prefix.Append(command.Trim());
        return prefix.ToString();
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
    {
        var value = ReadString(root, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Harness manifest requires '{propertyName}'.");
        }

        return value;
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.ToString().Trim()
            : string.Empty;
    }

    private static bool ReadBool(JsonElement root, string propertyName, bool defaultValue)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }

    private static string ShellQuote(string value)
    {
        return $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";
    }

    private static HarnessImporterSnapshot CreateSnapshot(HarnessDefinition? active, IReadOnlyList<HarnessDefinition> harnesses)
    {
        var orchestrationSteps = BuildOrchestrationSteps(active, harnesses);
        var orchestrationSummary = orchestrationSteps.Count == 0
            ? "Orchestration: attach harnesses"
            : $"Orchestration: {orchestrationSteps.Count} enabled steps across {orchestrationSteps.Select(step => step.HarnessId).Distinct(StringComparer.OrdinalIgnoreCase).Count()} harnesses";

        if (active is null)
        {
            return new HarnessImporterSnapshot(
                null,
                harnesses,
                "Harness: inactive",
                "No harnesses imported",
                "Wrapper: not proposed",
                orchestrationSummary,
                orchestrationSteps);
        }

        var enabled = active.Capabilities.Count(capability => capability.IsEnabled);
        var status = active.Health switch
        {
            HarnessHealth.Discovered => $"Harness: {active.Name}",
            HarnessHealth.Missing => "Harness: missing",
            HarnessHealth.Unavailable => "Harness: unavailable",
            _ => "Harness: inactive"
        };

        return new HarnessImporterSnapshot(
            active,
            harnesses,
            status,
            $"{enabled}/{active.Capabilities.Count} capabilities enabled",
            $"{active.ProposedEndpoints.Count} wrapper endpoints proposed",
            orchestrationSummary,
            orchestrationSteps);
    }

    private static IReadOnlyList<HarnessOrchestrationStep> BuildOrchestrationSteps(HarnessDefinition? active, IReadOnlyList<HarnessDefinition> harnesses)
    {
        var orderedHarnesses = harnesses
            .OrderByDescending(harness => active is not null && string.Equals(harness.Id, active.Id, StringComparison.OrdinalIgnoreCase))
            .ThenBy(harness => harness.Kind == HarnessKind.Manual ? 0 : 1)
            .ThenBy(harness => harness.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var steps = new List<HarnessOrchestrationStep>();

        foreach (var harness in orderedHarnesses)
        {
            var enabledCapabilities = harness.Capabilities
                .Where(capability => capability.IsEnabled)
                .OrderBy(capability => capability.RequiresApproval)
                .ThenBy(capability => capability.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var capability in enabledCapabilities)
            {
                var endpoint = harness.ProposedEndpoints.FirstOrDefault(candidate => string.Equals(candidate.CapabilityId, capability.Id, StringComparison.OrdinalIgnoreCase))
                    ?? harness.ProposedEndpoints.FirstOrDefault();
                steps.Add(new HarnessOrchestrationStep(
                    steps.Count + 1,
                    harness.Id,
                    harness.Name,
                    ResolveHarnessRole(harness, capability),
                    capability.Id,
                    endpoint is null ? "No endpoint proposed" : $"{endpoint.Method} {endpoint.Route}",
                    ResolveHarnessStepStatus(harness, capability, endpoint)));
            }
        }

        return steps;
    }

    private static string ResolveHarnessRole(HarnessDefinition harness, HarnessCapabilityDefinition capability)
    {
        if (harness.Kind == HarnessKind.CodexCli)
        {
            return capability.RequiresApproval ? "Approval-gated Codex action" : "Read-only Codex action";
        }

        if (capability.Id.Contains("chat", StringComparison.OrdinalIgnoreCase)
            || capability.Id.Contains("conversation", StringComparison.OrdinalIgnoreCase))
        {
            return "Hosted conversation turn";
        }

        return harness.Kind == HarnessKind.Manual ? "Hosted harness action" : "Harness action";
    }

    private static string ResolveHarnessStepStatus(HarnessDefinition harness, HarnessCapabilityDefinition capability, HarnessEndpointDefinition? endpoint)
    {
        if (harness.Health != HarnessHealth.Discovered)
        {
            return $"Inactive: {harness.Health}";
        }

        if (endpoint is null)
        {
            return "Blocked: no endpoint";
        }

        return capability.RequiresApproval ? "Ready with approval" : "Ready";
    }

    private static IReadOnlyList<HarnessCapabilityDefinition> CreateCodexCliCapabilities(IReadOnlyList<string> commands)
    {
        var hasExec = commands.Any(command => string.Equals(command, "exec", StringComparison.OrdinalIgnoreCase));
        var hasAppServer = commands.Any(command => string.Equals(command, "app-server", StringComparison.OrdinalIgnoreCase));
        var hasMcpServer = commands.Any(command => string.Equals(command, "mcp-server", StringComparison.OrdinalIgnoreCase));
        var hasResume = commands.Any(command => string.Equals(command, "resume", StringComparison.OrdinalIgnoreCase));

        return new[]
        {
            new HarnessCapabilityDefinition(
                "codex.cli.discover",
                "Discover CLI",
                "Probe codex path, version, command list, and noninteractive flags.",
                true,
                false),
            new HarnessCapabilityDefinition(
                "codex.cli.exec.readonly",
                "Read-only exec",
                hasExec ? "Run codex exec with read-only sandbox and captured output." : "codex exec was not discovered.",
                hasExec,
                false),
            new HarnessCapabilityDefinition(
                "codex.cli.exec.workspace_write",
                "Workspace-write exec",
                hasExec ? "Run codex exec with workspace-write sandbox through an approval-gated job." : "codex exec was not discovered.",
                false,
                true),
            new HarnessCapabilityDefinition(
                "codex.cli.resume",
                "Resume session",
                hasResume ? "Resume a prior Codex session when an approved job references one." : "codex resume was not discovered.",
                false,
                true),
            new HarnessCapabilityDefinition(
                "codex.cli.app_server",
                "App-server bridge",
                hasAppServer ? "Launch codex app-server behind a local wrapper process." : "codex app-server was not discovered.",
                false,
                true),
            new HarnessCapabilityDefinition(
                "codex.cli.mcp_server",
                "MCP bridge",
                hasMcpServer ? "Launch codex mcp-server for other agents to consume." : "codex mcp-server was not discovered.",
                false,
                true)
        };
    }

    private static HashSet<string> DefaultEnabledCodexCapabilityIds(CodexCliDiscoveryResult result)
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "codex.cli.discover"
        };

        if (result.Commands.Any(command => string.Equals(command, "exec", StringComparison.OrdinalIgnoreCase)))
        {
            selected.Add("codex.cli.exec.readonly");
        }

        return selected;
    }

    private static IReadOnlyList<HarnessEndpointDefinition> CreateCodexCliWrapperEndpoints()
    {
        return new[]
        {
            new HarnessEndpointDefinition(
                "GET",
                "/harnesses/codex-cli/status",
                "codex.cli.discover",
                "safe",
                "Return discovered Codex CLI path, version, and enabled capabilities."),
            new HarnessEndpointDefinition(
                "POST",
                "/harnesses/codex-cli/discover",
                "codex.cli.discover",
                "safe",
                "Refresh local Codex CLI discovery."),
            new HarnessEndpointDefinition(
                "POST",
                "/harnesses/codex-cli/exec/read-only",
                "codex.cli.exec.readonly",
                "safe",
                "Queue a read-only codex exec run and capture final output."),
            new HarnessEndpointDefinition(
                "POST",
                "/harnesses/codex-cli/exec/workspace-write",
                "codex.cli.exec.workspace_write",
                "approval",
                "Queue a workspace-write codex exec run after explicit approval."),
            new HarnessEndpointDefinition(
                "POST",
                "/harnesses/codex-cli/app-server",
                "codex.cli.app_server",
                "approval",
                "Start a local Codex app-server bridge when that capability is enabled.")
        };
    }

    private static bool IsCapabilityEnabled(HarnessDefinition harness, string capabilityId)
    {
        return harness.Capabilities.Any(capability =>
            string.Equals(capability.Id, capabilityId, StringComparison.OrdinalIgnoreCase)
            && capability.IsEnabled);
    }

    private static string ResolveEndpointRoute(HarnessDefinition harness, string capabilityId)
    {
        var endpoint = harness.ProposedEndpoints
            .FirstOrDefault(candidate => string.Equals(candidate.CapabilityId, capabilityId, StringComparison.OrdinalIgnoreCase));

        return endpoint is null
            ? "POST /harnesses/codex-cli/exec/read-only"
            : $"{endpoint.Method} {endpoint.Route}";
    }

    private static HarnessQuestionResult CreateQuestionFailure(HarnessQuestionRequest request, string harnessId, string message)
    {
        return new HarnessQuestionResult(
            false,
            harnessId,
            "POST /harnesses/codex-cli/exec/read-only",
            request.Question,
            string.Empty,
            -1,
            message,
            TimeSpan.Zero);
    }

    private static string CreateHarnessId(string name, string commandPath)
    {
        var input = $"{name.Trim().ToLowerInvariant()}|{commandPath.Trim().ToLowerInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"harness-{Convert.ToHexString(hash)[..10].ToLowerInvariant()}";
    }
}
