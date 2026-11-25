using SharedKernel.DocumentWorkflow.EntityFrameworkCore;
using SharedKernel.Mediator;
using SharedKernel.Results;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>Exposes an authorized document finalization request through the host mediator.</summary>
/// <param name="Request">The reusable input accepted by the finalization handler.</param>
internal sealed record FinalizeDocumentRevisionCommand(DocumentFinalizationRequest Request) : ICommand<Result<DocumentFinalizationReceipt>>
{
    /// <inheritdoc />
    public override string ToString() => nameof(FinalizeDocumentRevisionCommand);
}
