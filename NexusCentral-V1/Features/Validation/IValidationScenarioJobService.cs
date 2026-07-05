using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Features.Validation;

public interface IValidationScenarioJobService
{
    event EventHandler<ValidationScenarioJobState>? StateChanged;

    ValidationScenarioJobState? ActiveJob { get; }

    IReadOnlyList<ValidationScenarioJobState> GetJobs();

    Task<ValidationScenarioJobState> EnqueueAsync(ValidationScenarioSnapshot snapshot, int maxScenarios, CancellationToken cancellationToken = default);
}
