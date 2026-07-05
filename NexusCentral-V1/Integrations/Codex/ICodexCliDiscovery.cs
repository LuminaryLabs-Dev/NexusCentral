using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Integrations.Codex;

public interface ICodexCliDiscovery
{
    Task<CodexCliDiscoveryResult> DiscoverAsync(CancellationToken cancellationToken = default);
}

public interface ICodexCliQuestionRunner
{
    Task<CodexCliQuestionResult> AskReadOnlyAsync(CodexCliQuestionRequest request, CancellationToken cancellationToken = default);
}

public sealed record CodexCliDiscoveryResult(
    bool Found,
    string ExecutablePath,
    string Version,
    IReadOnlyList<string> Commands,
    IReadOnlyList<string> ExecOptions,
    string Message);

public sealed record CodexCliQuestionRequest(
    string ExecutablePath,
    string WorkingDirectory,
    string Question,
    TimeSpan Timeout);

public sealed record CodexCliQuestionResult(
    bool Ok,
    int ExitCode,
    string Answer,
    string StandardOutput,
    string StandardError,
    string Message,
    TimeSpan Duration);
