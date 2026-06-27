using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NexusCentral_Avalonia.Models;

namespace NexusCentral_Avalonia.Services;

public class DownloadService
{
    private static readonly HttpClient HttpClient = new();
    private readonly ConcurrentDictionary<string, DownloadJob> _jobs = new();
    private readonly AppPathsService _paths;
    private readonly ModelCatalogService _catalog;
    private readonly LogService _logService;

    public event Action<DownloadJob>? JobUpdated;

    public DownloadService(AppPathsService paths, ModelCatalogService catalog, LogService logService)
    {
        _paths = paths;
        _catalog = catalog;
        _logService = logService;
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NexusCentral/0.1");
    }

    public DownloadJob StartDownload(string repo, string filename, string revision = "main")
    {
        var downloadId = Guid.NewGuid().ToString("N");
        var destinationPath = Path.Combine(_paths.ModelsDirectory, filename);

        var job = new DownloadJob
        {
            DownloadId = downloadId,
            Repo = repo,
            Revision = string.IsNullOrWhiteSpace(revision) ? "main" : revision,
            Filename = filename,
            DestinationPath = destinationPath,
            Status = "queued",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        _jobs[downloadId] = job;
        Emit(job, "info", "Download job queued.", new { repo, filename, revision = job.Revision, destinationPath });
        _ = Task.Run(() => RunDownloadAsync(job));
        return Clone(job);
    }

    public DownloadJob? GetJob(string downloadId)
    {
        return _jobs.TryGetValue(downloadId, out var job) ? Clone(job) : null;
    }

    private async Task RunDownloadAsync(DownloadJob job)
    {
        try
        {
            Update(job, "downloading", 0, 0, null, 0, null);
            var requestUrl = $"https://huggingface.co/{job.Repo}/resolve/{job.Revision}/{job.Filename}?download=true";
            Emit(job, "info", "Resolved download URL.", new { requestUrl });

            using var response = await HttpClient.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            Emit(job, "info", "Response headers received.", new { totalBytes });

            await using var source = await response.Content.ReadAsStreamAsync();
            await using var destination = File.Create(job.DestinationPath);
            var buffer = new byte[1024 * 128];
            var bytesDownloaded = 0L;
            var stopwatch = Stopwatch.StartNew();
            var lastLogAt = TimeSpan.Zero;

            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (read <= 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read));
                bytesDownloaded += read;
                var progress = totalBytes.HasValue && totalBytes.Value > 0
                    ? bytesDownloaded * 100d / totalBytes.Value
                    : 0d;
                var elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
                var bytesPerSecond = bytesDownloaded / elapsedSeconds;

                Update(job, "downloading", progress, bytesDownloaded, totalBytes, bytesPerSecond, null);

                if ((stopwatch.Elapsed - lastLogAt).TotalMilliseconds >= 750)
                {
                    lastLogAt = stopwatch.Elapsed;
                    Emit(job, "info", "Download progress.", new
                    {
                        bytesDownloaded,
                        totalBytes,
                        progressPercent = progress,
                        bytesPerSecond,
                    });
                }
            }

            stopwatch.Stop();
            var entry = _catalog.RegisterDownloadedModel(job.Repo, job.DestinationPath);
            Update(job, "completed", 100, bytesDownloaded, totalBytes ?? bytesDownloaded, bytesDownloaded / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001), null);
            Emit(job, "info", "Download completed.", new { bytesDownloaded, totalBytes, destination = job.DestinationPath, healthStatus = entry.HealthStatus });
        }
        catch (Exception ex)
        {
            Update(job, "failed", job.ProgressPercent, job.BytesDownloaded, job.TotalBytes, job.BytesPerSecond, ex.Message);
            Emit(job, "error", "Download failed.", new { error = ex.Message });
        }
    }

    private void Update(DownloadJob job, string status, double progress, long bytesDownloaded, long? totalBytes, double bytesPerSecond, string? error)
    {
        job.Status = status;
        job.ProgressPercent = progress;
        job.BytesDownloaded = bytesDownloaded;
        job.TotalBytes = totalBytes;
        job.BytesPerSecond = bytesPerSecond;
        job.ErrorMessage = error;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
        JobUpdated?.Invoke(Clone(job));
    }

    private void Emit(DownloadJob job, string level, string message, object data)
    {
        _logService.Append(level, "downloads", $"{job.DownloadId}: {message}", job.DownloadId);

        var logEntry = new
        {
            timestamp_utc = DateTimeOffset.UtcNow,
            download_id = job.DownloadId,
            level,
            message,
            data
        };

        var path = _paths.GetDownloadLogPath(job.DownloadId);
        File.AppendAllText(path, JsonSerializer.Serialize(logEntry) + Environment.NewLine);
    }

    private static DownloadJob Clone(DownloadJob job) =>
        new()
        {
            DownloadId = job.DownloadId,
            Repo = job.Repo,
            Revision = job.Revision,
            Filename = job.Filename,
            DestinationPath = job.DestinationPath,
            Status = job.Status,
            ProgressPercent = job.ProgressPercent,
            BytesDownloaded = job.BytesDownloaded,
            TotalBytes = job.TotalBytes,
            BytesPerSecond = job.BytesPerSecond,
            ErrorMessage = job.ErrorMessage,
            CreatedAtUtc = job.CreatedAtUtc,
            UpdatedAtUtc = job.UpdatedAtUtc,
        };
}
