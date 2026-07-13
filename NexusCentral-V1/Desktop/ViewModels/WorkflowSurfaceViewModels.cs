using NexusCentral.V1.Features.Harnesses;
using NexusCentral.V1.Features.Lab;
using NexusCentral.V1.Features.Models;
using NexusCentral.V1.Features.Router;

namespace NexusCentral.V1.Desktop.ViewModels;

public sealed class SetupWorkflowViewModel
{
    public SetupWorkflowViewModel(
        ModelsViewModel models,
        HarnessImporterViewModel harnesses,
        RouterViewModel router)
    {
        Models = models;
        Harnesses = harnesses;
        Router = router;
    }

    public ModelsViewModel Models { get; }

    public HarnessImporterViewModel Harnesses { get; }

    public RouterViewModel Router { get; }
}

public sealed class ReviewProofViewModel
{
    public ReviewProofViewModel(LabViewModel lab)
    {
        Lab = lab;
    }

    public LabViewModel Lab { get; }
}
