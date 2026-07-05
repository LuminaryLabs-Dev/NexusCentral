using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Integrations.Codex;

public sealed class CodexCliQuestionRunner : ICodexCliQuestionRunner
{
    public async Task<CodexCliQuestionResult> AskReadOnlyAsync(CodexCliQuestionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ExecutablePath))
        {
            return CreateFailure("Codex CLI path is missing.");
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return CreateFailure("Question is required.");
        }

        var workingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
            ? Environment.CurrentDirectory
            : request.WorkingDirectory;

        if (!Directory.Exists(workingDirectory))
        {
            return CreateFailure($"Working directory does not exist: {workingDirectory}");
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"nexuscentral-codex-answer-{Guid.NewGuid():N}.txt");
        var timeout = request.Timeout <= TimeSpan.Zero ? TimeSpan.FromMinutes(2) : request.Timeout;
        var started = Stopwatch.GetTimestamp();

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(request.ExecutablePath)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory
                }
            };

            process.StartInfo.ArgumentList.Add("exec");
            process.StartInfo.ArgumentList.Add("--sandbox");
            process.StartInfo.ArgumentList.Add("read-only");
            process.StartInfo.ArgumentList.Add("--ephemeral");
            process.StartInfo.ArgumentList.Add("--cd");
            process.StartInfo.ArgumentList.Add(workingDirectory);
            process.StartInfo.ArgumentList.Add("-o");
            process.StartInfo.ArgumentList.Add(outputPath);
            process.StartInfo.ArgumentList.Add(request.Question);

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var waitTask = process.WaitForExitAsync(cancellationToken);
            var completed = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken));

            if (completed != waitTask)
            {
                TryKill(process);
                return new CodexCliQuestionResult(
                    false,
                    -1,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "Codex CLI question timed out.",
                    Stopwatch.GetElapsedTime(started));
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            var answer = File.Exists(outputPath)
                ? await File.ReadAllTextAsync(outputPath, cancellationToken)
                : stdout;

            return new CodexCliQuestionResult(
                process.ExitCode == 0,
                process.ExitCode,
                answer.Trim(),
                stdout.Trim(),
                stderr.Trim(),
                process.ExitCode == 0 ? "Codex CLI answered through read-only exec." : ResolveFailureMessage(stderr),
                Stopwatch.GetElapsedTime(started));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            return new CodexCliQuestionResult(
                false,
                -1,
                string.Empty,
                string.Empty,
                ex.Message,
                ex.Message,
                Stopwatch.GetElapsedTime(started));
        }
        finally
        {
            TryDelete(outputPath);
        }
    }

    private static CodexCliQuestionResult CreateFailure(string message)
    {
        return new CodexCliQuestionResult(false, -1, string.Empty, string.Empty, message, message, TimeSpan.Zero);
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
        catch (InvalidOperationException)
        {
        }
    }

    private static string ResolveFailureMessage(string stderr)
    {
        var message = stderr
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

        return string.IsNullOrWhiteSpace(message) ? "Codex CLI exec failed." : message;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
