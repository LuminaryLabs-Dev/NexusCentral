using System.Threading;
using System.Threading.Tasks;

namespace NexusCentral.V1.Features.Validation;

public interface IValidationScenarioRunService
{
    Task<ValidationScenarioRunResult> StageSandboxRunAsync(ValidationScenarioSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<ValidationScenarioExecutionResult> ExecuteSandboxBatchAsync(ValidationScenarioSnapshot snapshot, int maxScenarios, CancellationToken cancellationToken = default);
}
