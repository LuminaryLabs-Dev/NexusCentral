using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class ModelCatalogService
{
    private const long DefaultBitNetMaxMemoryMb = 8192;
    private const long DefaultGgufMaxMemoryMb = 2048;
    private const int DefaultBitNetThreads = 4;
    private const int DefaultBitNetContextSize = 2048;
    private static readonly HttpClient HttpClient = new();
    private readonly AppPathsService _paths;
    private readonly LogService _logs;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private List<ModelCatalogEntry> _entries = new();

    public event Action? CatalogChanged;

    public ModelCatalogService(AppPathsService paths, LogService logs)
    {
        _paths = paths;
        _logs = logs;
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NexusCentral/0.1");
        Load();
    }

    public IReadOnlyList<ModelCatalogEntry> GetEntries()
    {
        lock (_lock)
        {
            return _entries
                .Select(Clone)
                .ToList();
        }
    }

    public ModelCatalogEntry? FindById(string? modelId)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(x => string.Equals(x.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
            return entry is null ? null : Clone(entry);
        }
    }

    public ModelCatalogEntry ImportLocalModel(string path, string sourceRepo = "")
    {
        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(_paths.BaseDirectory, path));

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Model file was not found.", fullPath);
        }

        var entry = CreateEntry(fullPath, sourceRepo);
        Upsert(entry);
        return VerifyModel(entry.ModelId) ?? Clone(entry);
    }

    public ModelCatalogEntry RegisterDownloadedModel(string repo, string filePath)
    {
        var entry = CreateEntry(filePath, repo);
        Upsert(entry);
        return VerifyModel(entry.ModelId) ?? Clone(entry);
    }

    public ModelCatalogEntry? VerifyModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        ModelCatalogEntry? snapshot;
        lock (_lock)
        {
            snapshot = _entries.FirstOrDefault(x => string.Equals(x.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
            if (snapshot is null)
            {
                return null;
            }

            snapshot = Clone(snapshot);
            snapshot.HealthStatus = "verifying";
            snapshot.Status = "verifying";
            snapshot.LastVerifiedUtc = DateTimeOffset.UtcNow;
            ReplaceUnsafe(snapshot);
        }

        _logs.Append("info", "model-health", $"Verifying model {snapshot.ModelId}.", snapshot.ModelId);

        var verification = PerformVerification(snapshot);
        if (!verification.Ok)
        {
            return MarkVerificationFailure(snapshot.ModelId, verification.Reason, verification.HealthStatus, verification.Quarantine);
        }

        snapshot.HealthStatus = "healthy";
        snapshot.HealthReason = string.Empty;
        snapshot.Status = "ready";
        snapshot.AdmissionStatus = "admitted";
        snapshot.LastVerifiedUtc = DateTimeOffset.UtcNow;
        snapshot.LastRuntimeCheckUtc = DateTimeOffset.UtcNow;
        snapshot.QuarantinePath = string.Empty;
        snapshot.IsRunnable = IsBackendSupported(snapshot) && (!IsBackendMemoryBudgeted(snapshot) || IsWithinMemoryBudget(snapshot));
        snapshot.ChatCapable = IsChatCapable(snapshot);
        Upsert(snapshot);
        _logs.Append("info", "model-health", $"Model {snapshot.ModelId} verified healthy.", snapshot.ModelId);
        return Clone(snapshot);
    }

    public ModelCatalogEntry? GetModelHealth(string? modelId)
    {
        return FindById(modelId);
    }

    public ModelCatalogEntry? QuarantineModel(string? modelId, string reason)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        ModelCatalogEntry? entry;
        lock (_lock)
        {
            entry = _entries.FirstOrDefault(x => string.Equals(x.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                return null;
            }

            entry = Clone(entry);
        }

        return MarkUnhealthyAndQuarantine(entry.ModelId, string.IsNullOrWhiteSpace(reason) ? "Model was quarantined." : reason);
    }

    public async Task<ModelCatalogEntry?> RepairModelAsync(string? modelId)
    {
        var entry = FindById(modelId);
        if (entry is null)
        {
            return null;
        }

        entry.HealthStatus = "repairing";
        entry.Status = "repairing";
        Upsert(entry);
        _logs.Append("info", "model-health", $"Repairing model {entry.ModelId}.", entry.ModelId);

        if (!string.IsNullOrWhiteSpace(entry.SourceRepo) && !string.IsNullOrWhiteSpace(entry.Filename))
        {
            var destinationPath = Path.Combine(_paths.ModelsDirectory, entry.Filename);
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            var requestUrl = $"https://huggingface.co/{entry.SourceRepo}/resolve/main/{entry.Filename}?download=true";
            using var response = await HttpClient.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync();
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination);

            entry.LocalPath = destinationPath;
            entry.QuarantinePath = string.Empty;
            entry.FileSizeBytes = new FileInfo(destinationPath).Length;
            entry.Status = "downloaded";
            entry.HealthStatus = "unknown";
            Upsert(entry);
            return VerifyModel(entry.ModelId);
        }

        entry.HealthStatus = "unhealthy";
        entry.Status = "unhealthy";
        entry.HealthReason = "Repair requires manual re-import because source metadata is missing.";
        Upsert(entry);
        return entry;
    }

    public ModelCatalogEntry? MarkRuntimeFailure(string? modelId, string error)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        if (LooksLikeCorruptModel(error))
        {
            return MarkUnhealthyAndQuarantine(modelId, error);
        }

        var entry = FindById(modelId);
        if (entry is null)
        {
            return null;
        }

        entry.LastRuntimeCheckUtc = DateTimeOffset.UtcNow;
        entry.HealthReason = error;
        Upsert(entry);
        return entry;
    }

    private void Load()
    {
        lock (_lock)
        {
            if (File.Exists(_paths.CatalogPath))
            {
                try
                {
                    var json = File.ReadAllText(_paths.CatalogPath);
                    var loaded = JsonSerializer.Deserialize<List<ModelCatalogEntry>>(json);
                    _entries = loaded ?? new List<ModelCatalogEntry>();
                }
                catch
                {
                    _entries = new List<ModelCatalogEntry>();
                }
            }
            else
            {
                _entries = new List<ModelCatalogEntry>();
            }

            _entries = _entries.Select(Normalize).ToList();

            foreach (var looseModel in GetSupportedLocalModelFiles())
            {
                if (_entries.All(x => !string.Equals(x.LocalPath, looseModel, StringComparison.OrdinalIgnoreCase)))
                {
                    _entries.Add(CreateEntry(looseModel, ""));
                }
            }

            PersistUnsafe();
        }

        CatalogChanged?.Invoke();
    }

    private void Upsert(ModelCatalogEntry entry)
    {
        entry = Normalize(entry);

        lock (_lock)
        {
            var existing = _entries.FindIndex(x => string.Equals(x.ModelId, entry.ModelId, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                _entries[existing] = entry;
            }
            else
            {
                _entries.Add(entry);
            }

            PersistUnsafe();
        }

        CatalogChanged?.Invoke();
    }

    private void PersistUnsafe()
    {
        Directory.CreateDirectory(_paths.ModelsDirectory);
        File.WriteAllText(_paths.CatalogPath, JsonSerializer.Serialize(_entries, _jsonOptions));
    }

    private VerificationResult PerformVerification(ModelCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.LocalPath) || !File.Exists(entry.LocalPath))
        {
            return VerificationResult.Fail("Model file is missing.", "missing");
        }

        var fileInfo = new FileInfo(entry.LocalPath);
        if (fileInfo.Length <= 0)
        {
            return VerificationResult.Fail("Model file size is zero.", "unhealthy", quarantine: true);
        }

        if (entry.FileSizeBytes > 0 && fileInfo.Length != entry.FileSizeBytes)
        {
            _logs.Append("warning", "model-health", $"Recorded size {entry.FileSizeBytes} does not match on-disk size {fileInfo.Length} for {entry.ModelId}.", entry.ModelId);
        }

        if (!IsBackendSupported(entry))
        {
            return VerificationResult.Fail(string.IsNullOrWhiteSpace(entry.RunnableReason) ? "No runtime backend is configured for this model." : entry.RunnableReason, "unsupported");
        }

        if (IsBackendMemoryBudgeted(entry) && !IsWithinMemoryBudget(entry))
        {
            return VerificationResult.Fail(
                $"Estimated BitNet memory {entry.EstimatedMemoryMb} MB exceeds configured budget {entry.MaxMemoryMb} MB.",
                "blocked_by_memory_budget");
        }

        return VerificationResult.Success();
    }

    private VerificationResult ProbeRuntimeLoad(ModelCatalogEntry entry)
    {
        var executable = _paths.GetRuntimeExecutablePath(entry.RuntimeBackend);
        if (!File.Exists(executable))
        {
            return VerificationResult.Fail($"Runtime executable was not found at {executable}.", "runtime_unavailable");
        }

        var psi = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _paths.ResolvedRootDirectory,
        };
        psi.ArgumentList.Add("--model");
        psi.ArgumentList.Add(entry.LocalPath);
        psi.ArgumentList.Add("--n-predict");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("--ctx-size");
        psi.ArgumentList.Add(Math.Max(1, entry.ContextSize).ToString());
        psi.ArgumentList.Add("--threads");
        psi.ArgumentList.Add(Math.Max(1, entry.MaxThreads).ToString());
        psi.ArgumentList.Add("--max-memory-mb");
        psi.ArgumentList.Add(Math.Max(1, entry.MaxMemoryMb).ToString());
        psi.ArgumentList.Add("run-once");
        psi.ArgumentList.Add("--prompt");
        psi.ArgumentList.Add("Reply with OK only.");
        psi.ArgumentList.Add("--json");

        using var process = Process.Start(psi);
        if (process is null)
        {
            return VerificationResult.Fail("Failed to start the runtime verification process.", "runtime_unavailable");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim();
            if (LooksLikeCorruptModel(message))
            {
                return VerificationResult.Fail(message, "unhealthy", quarantine: true);
            }

            var status = message.Contains("BLOCKED_BY_MEMORY_BUDGET", StringComparison.OrdinalIgnoreCase)
                ? "blocked_by_memory_budget"
                : message.Contains("BITNET_RUNTIME", StringComparison.OrdinalIgnoreCase) ||
                  message.Contains("LLAMA_CPP_PYTHON", StringComparison.OrdinalIgnoreCase) ||
                  message.Contains("llama-cpp-python", StringComparison.OrdinalIgnoreCase)
                    ? "runtime_unavailable"
                    : "runtime_unavailable";
            return VerificationResult.Fail(string.IsNullOrWhiteSpace(message) ? "Runtime verification failed." : message, status);
        }

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var ok = doc.RootElement.TryGetProperty("ok", out var okElement) && okElement.GetBoolean();
            if (!ok)
            {
                var error = doc.RootElement.TryGetProperty("error", out var errorElement) && errorElement.ValueKind != JsonValueKind.Null
                    ? errorElement.GetString() ?? "Runtime verification failed."
                    : "Runtime verification failed.";
                return VerificationResult.Fail(error, "runtime_unavailable");
            }

            return VerificationResult.Success();
        }
        catch (Exception ex)
        {
            return VerificationResult.Fail($"Runtime verification output was invalid: {ex.Message}", "runtime_unavailable");
        }
    }

    private ModelCatalogEntry? MarkUnhealthyAndQuarantine(string modelId, string reason)
    {
        return MarkVerificationFailure(modelId, reason, "quarantined", quarantine: true);
    }

    private ModelCatalogEntry? MarkVerificationFailure(string modelId, string reason, string healthStatus, bool quarantine)
    {
        ModelCatalogEntry? entry;
        lock (_lock)
        {
            entry = _entries.FirstOrDefault(x => string.Equals(x.ModelId, modelId, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                return null;
            }

            entry = Clone(entry);
        }

        entry.HealthStatus = string.IsNullOrWhiteSpace(healthStatus) ? "unhealthy" : healthStatus;
        entry.Status = entry.HealthStatus;
        entry.HealthReason = string.IsNullOrWhiteSpace(reason) ? "Verification failed." : reason;
        entry.LastRuntimeCheckUtc = DateTimeOffset.UtcNow;
        entry.LastVerifiedUtc = DateTimeOffset.UtcNow;
        entry.AdmissionStatus = entry.HealthStatus;
        entry.IsRunnable = false;
        entry.ChatCapable = IsChatCapable(entry);

        if (quarantine && !string.IsNullOrWhiteSpace(entry.LocalPath) && File.Exists(entry.LocalPath) && !IsPathInQuarantine(entry.LocalPath))
        {
            var quarantinePath = GetQuarantinePath(entry.Filename);
            Directory.CreateDirectory(_paths.QuarantineDirectory);
            File.Move(entry.LocalPath, quarantinePath, true);
            entry.LocalPath = quarantinePath;
            entry.QuarantinePath = quarantinePath;
            entry.HealthStatus = "quarantined";
            entry.Status = "quarantined";
        }

        Upsert(entry);
        var action = quarantine ? "quarantined" : "blocked";
        _logs.Append("error", "model-health", $"Model {entry.ModelId} {action}: {entry.HealthReason}", entry.ModelId);
        return Clone(entry);
    }

    private bool IsPathInQuarantine(string path) =>
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(_paths.QuarantineDirectory), StringComparison.OrdinalIgnoreCase);

    private string GetQuarantinePath(string filename)
    {
        var safeFilename = string.IsNullOrWhiteSpace(filename) ? "model.gguf" : filename;
        return Path.Combine(_paths.QuarantineDirectory, $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{safeFilename}");
    }

    private void ReplaceUnsafe(ModelCatalogEntry entry)
    {
        var existing = _entries.FindIndex(x => string.Equals(x.ModelId, entry.ModelId, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            _entries[existing] = entry;
        }
        else
        {
            _entries.Add(entry);
        }

        PersistUnsafe();
        CatalogChanged?.Invoke();
    }

    private static ModelCatalogEntry CreateEntry(string fullPath, string sourceRepo)
    {
        var file = new FileInfo(fullPath);
        var filename = Path.GetFileName(fullPath);
        var quantType = ExtractBitNetQuantType($"{filename} {fullPath} {sourceRepo}");
        var family = DetectModelFamily($"{filename} {fullPath} {sourceRepo}");
        var estimatedMemoryMb = EstimateBitNetMemoryMb(file.Exists ? file.Length : 0);
        var runtimeBackend = DetermineRuntimeBackend(filename, fullPath, sourceRepo);
        var maxMemoryMb = ResolveDefaultMaxMemoryMb(runtimeBackend);
        var healthStatus = IsBackendSupported(runtimeBackend)
            ? !IsBackendMemoryBudgeted(runtimeBackend) || estimatedMemoryMb <= maxMemoryMb ? "unknown" : "blocked_by_memory_budget"
            : "unsupported";

        return new ModelCatalogEntry
        {
            ModelId = BuildModelId(filename),
            DisplayName = filename,
            LocalPath = fullPath,
            Format = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant(),
            RuntimeBackend = runtimeBackend,
            IsRunnable = IsBackendSupported(runtimeBackend) && (!IsBackendMemoryBudgeted(runtimeBackend) || estimatedMemoryMb <= maxMemoryMb),
            ChatCapable = IsBackendSupported(runtimeBackend),
            RunnableReason = string.Empty,
            Quantization = quantType,
            QuantType = quantType,
            ModelFamily = family,
            EstimatedMemoryMb = estimatedMemoryMb,
            MaxMemoryMb = maxMemoryMb,
            MaxThreads = ResolveDefaultMaxThreads(),
            ContextSize = ResolveDefaultContextSize(),
            MaxLoadedInstances = 1,
            AdmissionStatus = healthStatus,
            SourceRepo = sourceRepo,
            Filename = filename,
            Status = "ready",
            FileSizeBytes = file.Exists ? file.Length : 0,
            HealthStatus = healthStatus,
        };
    }

    private static string BuildModelId(string filename) =>
        Path.GetFileNameWithoutExtension(filename)
            .ToLowerInvariant()
            .Replace('.', '-')
            .Replace('_', '-')
            .Replace(' ', '-');

    private static string ExtractQuantization(string filename)
    {
        var parts = Path.GetFileNameWithoutExtension(filename).Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.LastOrDefault(part => part.StartsWith("Q", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }

    private static ModelCatalogEntry Clone(ModelCatalogEntry entry) =>
        new()
        {
            ModelId = entry.ModelId,
            DisplayName = entry.DisplayName,
            LocalPath = entry.LocalPath,
            Format = entry.Format,
            RuntimeBackend = entry.RuntimeBackend,
            IsRunnable = entry.IsRunnable,
            ChatCapable = entry.ChatCapable,
            RunnableReason = entry.RunnableReason,
            Quantization = entry.Quantization,
            QuantType = entry.QuantType,
            ModelFamily = entry.ModelFamily,
            EstimatedMemoryMb = entry.EstimatedMemoryMb,
            MaxMemoryMb = entry.MaxMemoryMb,
            MaxThreads = entry.MaxThreads,
            ContextSize = entry.ContextSize,
            MaxLoadedInstances = entry.MaxLoadedInstances,
            AdmissionStatus = entry.AdmissionStatus,
            SourceRepo = entry.SourceRepo,
            Filename = entry.Filename,
            Status = entry.Status,
            FileSizeBytes = entry.FileSizeBytes,
            HealthStatus = entry.HealthStatus,
            HealthReason = entry.HealthReason,
            LastVerifiedUtc = entry.LastVerifiedUtc,
            LastRuntimeCheckUtc = entry.LastRuntimeCheckUtc,
            QuarantinePath = entry.QuarantinePath,
        };

    private static ModelCatalogEntry Normalize(ModelCatalogEntry entry)
    {
        entry.RuntimeBackend = DetermineRuntimeBackend(entry.Filename, entry.LocalPath, entry.SourceRepo);
        entry.ModelFamily = DetectModelFamily($"{entry.Filename} {entry.LocalPath} {entry.SourceRepo}");
        entry.QuantType = string.IsNullOrWhiteSpace(entry.QuantType)
            ? ExtractBitNetQuantType($"{entry.Filename} {entry.LocalPath} {entry.SourceRepo}")
            : entry.QuantType;
        entry.Quantization = string.IsNullOrWhiteSpace(entry.Quantization) ? entry.QuantType : entry.Quantization;
        entry.EstimatedMemoryMb = entry.EstimatedMemoryMb > 0 ? entry.EstimatedMemoryMb : EstimateBitNetMemoryMb(entry.FileSizeBytes);
        entry.MaxMemoryMb = entry.MaxMemoryMb > 0 ? entry.MaxMemoryMb : ResolveDefaultMaxMemoryMb(entry.RuntimeBackend);
        if (string.Equals(entry.RuntimeBackend, "llama.cpp", StringComparison.OrdinalIgnoreCase))
        {
            entry.MaxMemoryMb = Math.Clamp(entry.MaxMemoryMb, 50, DefaultGgufMaxMemoryMb);
        }
        entry.MaxThreads = entry.MaxThreads > 0 ? entry.MaxThreads : ResolveDefaultMaxThreads();
        entry.ContextSize = entry.ContextSize > 0 ? entry.ContextSize : ResolveDefaultContextSize();
        entry.MaxLoadedInstances = entry.MaxLoadedInstances > 0 ? entry.MaxLoadedInstances : 1;
        entry.AdmissionStatus = BuildAdmissionStatus(entry);
        var backendRunnable = IsBackendSupported(entry) && (!IsBackendMemoryBudgeted(entry) || IsWithinMemoryBudget(entry));
        entry.IsRunnable = backendRunnable && !IsBlockingHealthStatus(entry.HealthStatus);
        entry.ChatCapable = IsChatCapable(entry);
        entry.RunnableReason = BuildRunnableReason(entry);
        if (string.IsNullOrWhiteSpace(entry.HealthStatus))
        {
            entry.HealthStatus = backendRunnable ? "unknown" : "unsupported";
        }
        return entry;
    }

    private static string DetermineRuntimeBackend(string filename, string localPath = "", string sourceRepo = "")
    {
        var format = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
        if (!string.Equals(format, "gguf", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(format, "safetensors", StringComparison.OrdinalIgnoreCase)
                ? "unconfigured-safetensors"
                : "unsupported";
        }

        return LooksLikeBitNetModel($"{filename} {localPath} {sourceRepo}")
            ? "bitnet.cpp"
            : "llama.cpp";
    }

    private IEnumerable<string> GetSupportedLocalModelFiles()
    {
        if (!Directory.Exists(_paths.ModelsDirectory))
        {
            return Enumerable.Empty<string>();
        }

        return Directory
            .EnumerateFiles(_paths.ModelsDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var extension = Path.GetExtension(path);
                return string.Equals(extension, ".gguf", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".safetensors", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static bool IsBackendSupported(ModelCatalogEntry entry) =>
        IsBackendSupported(entry.RuntimeBackend);

    private static bool IsChatCapable(ModelCatalogEntry entry) =>
        IsBackendSupported(entry);

    private static string BuildRunnableReason(ModelCatalogEntry entry)
    {
        if (string.Equals(entry.HealthStatus, "blocked_by_memory_budget", StringComparison.OrdinalIgnoreCase))
        {
            return $"Estimated BitNet memory {entry.EstimatedMemoryMb} MB exceeds configured budget {entry.MaxMemoryMb} MB.";
        }

        if (string.Equals(entry.HealthStatus, "runtime_unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(entry.HealthReason) ? "BitNet runtime is not configured." : entry.HealthReason;
        }

        if (string.Equals(entry.HealthStatus, "quarantined", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(entry.HealthReason) ? "Model has been quarantined and must be repaired before use." : entry.HealthReason;
        }

        if (string.Equals(entry.HealthStatus, "unhealthy", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(entry.HealthReason) ? "Model is unhealthy and cannot be used until repaired." : entry.HealthReason;
        }

        var format = Path.GetExtension(entry.Filename).TrimStart('.').ToLowerInvariant();
        if (string.Equals(format, "gguf", StringComparison.OrdinalIgnoreCase) && !IsBackendSupported(entry))
        {
            return "No GGUF runtime backend is configured for this model.";
        }

        return format switch
        {
            "gguf" => string.Empty,
            "safetensors" => "No safetensors runtime backend is configured yet.",
            _ => $"No runtime backend is configured for format '{format}'.",
        };
    }

    private static bool IsBackendSupported(string runtimeBackend) =>
        string.Equals(runtimeBackend, "bitnet.cpp", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(runtimeBackend, "llama.cpp", StringComparison.OrdinalIgnoreCase);

    private static bool IsWithinMemoryBudget(ModelCatalogEntry entry) =>
        entry.MaxMemoryMb <= 0 || entry.EstimatedMemoryMb <= entry.MaxMemoryMb;

    private static bool IsBackendMemoryBudgeted(ModelCatalogEntry entry) =>
        IsBackendMemoryBudgeted(entry.RuntimeBackend);

    private static bool IsBackendMemoryBudgeted(string runtimeBackend) =>
        string.Equals(runtimeBackend, "bitnet.cpp", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlockingHealthStatus(string? healthStatus) =>
        string.Equals(healthStatus, "unhealthy", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(healthStatus, "quarantined", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(healthStatus, "unsupported", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(healthStatus, "blocked_by_memory_budget", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(healthStatus, "runtime_unavailable", StringComparison.OrdinalIgnoreCase);

    private static string BuildAdmissionStatus(ModelCatalogEntry entry)
    {
        if (!IsBackendSupported(entry))
        {
            return "unsupported";
        }

        return !IsBackendMemoryBudgeted(entry) || IsWithinMemoryBudget(entry) ? "admissible" : "blocked_by_memory_budget";
    }

    private static bool LooksLikeBitNetModel(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Contains("bitnet", StringComparison.Ordinal) ||
            lower.Contains("b1.58", StringComparison.Ordinal) ||
            lower.Contains("b1_58", StringComparison.Ordinal) ||
            lower.Contains("1.58bit", StringComparison.Ordinal) ||
            lower.Contains("1-58", StringComparison.Ordinal) ||
            lower.Contains("i2_s", StringComparison.Ordinal) ||
            lower.Contains("tl1", StringComparison.Ordinal) ||
            lower.Contains("tl2", StringComparison.Ordinal);
    }

    private static string DetectModelFamily(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("falcon", StringComparison.Ordinal))
        {
            return "falcon_1_58";
        }

        if (lower.Contains("llama", StringComparison.Ordinal))
        {
            return "llama_1_58";
        }

        if (lower.Contains("qwen3.5", StringComparison.Ordinal) ||
            lower.Contains("qwen35", StringComparison.Ordinal) ||
            lower.Contains("qwen", StringComparison.Ordinal))
        {
            return "qwen_gguf";
        }

        if (lower.Contains("lfm", StringComparison.Ordinal) ||
            lower.Contains("liquid", StringComparison.Ordinal))
        {
            return "lfm_gguf";
        }

        return LooksLikeBitNetModel(text) ? "bitnet_b1_58" : "unsupported";
    }

    private static string ExtractBitNetQuantType(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("i2_s", StringComparison.Ordinal))
        {
            return "i2_s";
        }

        if (lower.Contains("tl1", StringComparison.Ordinal))
        {
            return "tl1";
        }

        if (lower.Contains("tl2", StringComparison.Ordinal))
        {
            return "tl2";
        }

        return string.Empty;
    }

    private static long EstimateBitNetMemoryMb(long fileSizeBytes)
    {
        if (fileSizeBytes <= 0)
        {
            return 0;
        }

        var fileMb = fileSizeBytes / (1024d * 1024d);
        return Math.Max(512, (long)Math.Ceiling(fileMb * 1.25d + 512d));
    }

    private static long ResolveDefaultMaxMemoryMb(string runtimeBackend)
    {
        var variable = string.Equals(runtimeBackend, "llama.cpp", StringComparison.OrdinalIgnoreCase)
            ? "NEXUSCENTRAL_GGUF_MAX_MEMORY_MB"
            : "NEXUSCENTRAL_BITNET_MAX_MEMORY_MB";
        var configured = Environment.GetEnvironmentVariable(variable);
        if (long.TryParse(configured, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return string.Equals(runtimeBackend, "llama.cpp", StringComparison.OrdinalIgnoreCase)
            ? DefaultGgufMaxMemoryMb
            : DefaultBitNetMaxMemoryMb;
    }

    private static int ResolveDefaultMaxThreads()
    {
        var configured = Environment.GetEnvironmentVariable("NEXUSCENTRAL_BITNET_THREADS");
        return int.TryParse(configured, out var parsed) && parsed > 0 ? parsed : Math.Max(1, Math.Min(DefaultBitNetThreads, Environment.ProcessorCount));
    }

    private static int ResolveDefaultContextSize()
    {
        var configured = Environment.GetEnvironmentVariable("NEXUSCENTRAL_BITNET_CONTEXT_SIZE");
        return int.TryParse(configured, out var parsed) && parsed > 0 ? parsed : DefaultBitNetContextSize;
    }

    private static bool LooksLikeCorruptModel(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return false;
        }

        return error.Contains("corrupted or incomplete", StringComparison.OrdinalIgnoreCase)
            || error.Contains("failed to load model", StringComparison.OrdinalIgnoreCase)
            || error.Contains("data is not within the file bounds", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Model file is missing", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct VerificationResult(bool Ok, string Reason, string HealthStatus, bool Quarantine)
    {
        public static VerificationResult Success() => new(true, string.Empty, "healthy", false);
        public static VerificationResult Fail(string reason, string healthStatus = "unhealthy", bool quarantine = false) => new(false, reason, healthStatus, quarantine);
    }
}
