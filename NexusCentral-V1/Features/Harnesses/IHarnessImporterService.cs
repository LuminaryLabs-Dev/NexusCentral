using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Features.Harnesses;

public interface IHarnessImporterService
{
    event Action<HarnessImporterSnapshot>? StateChanged;

    HarnessImporterSnapshot GetSnapshot();

    Task<HarnessDefinition> DiscoverCodexCliAsync(CancellationToken cancellationToken = default);

    Task<HarnessDefinition> ImportHarnessAsync(HarnessImportRequest request, CancellationToken cancellationToken = default);

    Task<HarnessDefinition> ImportManifestAsync(string manifestPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HarnessManifestCandidate>> DiscoverManifestsAsync(string rootDirectory, CancellationToken cancellationToken = default);

    Task<HarnessQuestionResult> AskReadOnlyAsync(HarnessQuestionRequest request, CancellationToken cancellationToken = default);

    Task<bool> SelectHarnessAsync(string harnessId, CancellationToken cancellationToken = default);

    Task<bool> SetCapabilityEnabledAsync(string harnessId, string capabilityId, bool isEnabled, CancellationToken cancellationToken = default);

    Task<bool> RemoveHarnessAsync(string harnessId, CancellationToken cancellationToken = default);
}
