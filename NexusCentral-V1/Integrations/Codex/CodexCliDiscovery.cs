using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Integrations.Codex;

public sealed class CodexCliDiscovery : ICodexCliDiscovery
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    public async Task<CodexCliDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var executable = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(executable))
        {
            return new CodexCliDiscoveryResult(
                false,
                string.Empty,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                "Codex CLI was not found on known local paths.");
        }

        var version = await RunAsync(executable, "--version", cancellationToken);
        var help = await RunAsync(executable, "--help", cancellationToken);
        var execHelp = await RunAsync(executable, "exec --help", cancellationToken);

        if (!version.Ok && !help.Ok)
        {
            return new CodexCliDiscoveryResult(
                false,
                executable,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                string.IsNullOrWhiteSpace(help.Error) ? "Codex CLI did not respond." : help.Error);
        }

        var commands = ParseCommandNames(help.Output);
        var execOptions = ParseOptionNames(execHelp.Output);
        var versionText = FirstNonEmptyLine(version.Output);

        return new CodexCliDiscoveryResult(
            true,
            executable,
            string.IsNullOrWhiteSpace(versionText) ? "version unavailable" : versionText,
            commands,
            execOptions,
            $"Codex CLI discovered at {executable}.");
    }

    private static string ResolveExecutablePath()
    {
        var overridePath = Environment.GetEnvironmentVariable("NEXUSCENTRAL_CODEX_CLI_PATH");
        if (File.Exists(overridePath))
        {
            return overridePath;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(home, ".local", "bin", "codex"),
            "/opt/homebrew/bin/codex",
            "/usr/local/bin/codex"
        };

        return candidates.FirstOrDefault(File.Exists) ?? "codex";
    }

    private static async Task<CommandResult> RunAsync(string executable, string arguments, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(executable, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var waitTask = process.WaitForExitAsync(cancellationToken);
            var completed = await Task.WhenAny(waitTask, Task.Delay(ProbeTimeout, cancellationToken));

            if (completed != waitTask)
            {
                TryKill(process);
                return new CommandResult(false, string.Empty, "Codex CLI probe timed out.");
            }

            var output = await stdoutTask;
            var error = await stderrTask;
            return new CommandResult(process.ExitCode == 0, output, error);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            return new CommandResult(false, string.Empty, ex.Message);
        }
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

    private static IReadOnlyList<string> ParseCommandNames(string help)
    {
        var commands = new List<string>();
        var inCommands = false;

        foreach (var rawLine in help.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line == "Commands:")
            {
                inCommands = true;
                continue;
            }

            if (!inCommands)
            {
                continue;
            }

            if (line.EndsWith(":", StringComparison.Ordinal) || line.StartsWith("[", StringComparison.Ordinal))
            {
                break;
            }

            var leadingSpaces = rawLine.TakeWhile(char.IsWhiteSpace).Count();
            if (leadingSpaces != 2)
            {
                continue;
            }

            var name = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(name))
            {
                commands.Add(name);
            }
        }

        return commands.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList();
    }

    private static IReadOnlyList<string> ParseOptionNames(string help)
    {
        return help
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("-", StringComparison.Ordinal))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty)
            .Where(option => option.StartsWith("-", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(option => option)
            .ToList();
    }

    private static string FirstNonEmptyLine(string value)
    {
        return value
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))
            ?? string.Empty;
    }

    private sealed record CommandResult(bool Ok, string Output, string Error);
}
