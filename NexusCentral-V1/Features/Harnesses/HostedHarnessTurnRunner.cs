using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Features.Harnesses;

public sealed class HostedHarnessTurnRunner : IHostedHarnessTurnRunner
{
    public async Task<HostedHarnessTurnResult> RunTurnAsync(HarnessDefinition harness, HostedHarnessTurnRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(harness.CommandPath))
        {
            return CreateFailure(harness, request, "Harness command is empty.");
        }

        if (harness.Kind != HarnessKind.Manual)
        {
            return CreateFailure(harness, request, "Only manual hosted harness commands use the generic turn runner.");
        }

        var started = DateTimeOffset.UtcNow;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/zsh",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    : request.WorkingDirectory
            }
        };
        process.StartInfo.Environment["CONVERSATION_HARNESS_MAX_TOKENS"] = "16000";
        process.StartInfo.ArgumentList.Add("-lc");
        process.StartInfo.ArgumentList.Add(harness.CommandPath);

        try
        {
            process.Start();
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { message = request.Message }));
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(timeout.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            var assistant = ExtractAssistant(stdout);

            return new HostedHarnessTurnResult(
                process.ExitCode == 0 && !string.IsNullOrWhiteSpace(assistant),
                harness.Id,
                assistant,
                stdout,
                stderr,
                process.ExitCode,
                process.ExitCode == 0 ? "Harness turn completed." : "Harness turn failed.",
                DateTimeOffset.UtcNow - started);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return new HostedHarnessTurnResult(
                false,
                harness.Id,
                string.Empty,
                string.Empty,
                string.Empty,
                -1,
                "Harness turn timed out.",
                DateTimeOffset.UtcNow - started);
        }
        catch (Exception exc)
        {
            TryKill(process);
            return new HostedHarnessTurnResult(
                false,
                harness.Id,
                string.Empty,
                string.Empty,
                exc.Message,
                -1,
                exc.Message,
                DateTimeOffset.UtcNow - started);
        }
    }

    private static HostedHarnessTurnResult CreateFailure(HarnessDefinition harness, HostedHarnessTurnRequest request, string message)
    {
        return new HostedHarnessTurnResult(false, harness.Id, string.Empty, string.Empty, string.Empty, -1, message, request.Timeout);
    }

    private static string ExtractAssistant(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(stdout);
            if (document.RootElement.TryGetProperty("assistant", out var assistant) && assistant.ValueKind == JsonValueKind.String)
            {
                var assistantText = assistant.GetString();
                if (!string.IsNullOrWhiteSpace(assistantText))
                {
                    return assistantText;
                }
            }

            if (document.RootElement.TryGetProperty("nextBest", out var nextBest) && nextBest.ValueKind == JsonValueKind.String)
            {
                var nextBestText = nextBest.GetString();
                if (!string.IsNullOrWhiteSpace(nextBestText))
                {
                    return nextBestText;
                }
            }

            if (document.RootElement.TryGetProperty("raw", out var raw) && raw.ValueKind == JsonValueKind.String)
            {
                var rawText = raw.GetString();
                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    return rawText;
                }
            }
        }
        catch (JsonException)
        {
            return stdout.Trim();
        }

        return stdout.Trim();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
