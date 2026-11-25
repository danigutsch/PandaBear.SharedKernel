using PandaBear.DocumentWorkflow.EntityFrameworkCore;
using PandaBear.Mediator;
using PandaBear.Results;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Exposes an authorized document finalization request through the host mediator.</summary>
/// <param name="Request">The reusable input accepted by the finalization handler.</param>
internal sealed record FinalizeDocumentRevisionCommand(DocumentFinalizationRequest Request) : ICommand<Result<DocumentFinalizationReceipt>>
{
    /// <inheritdoc />
    public override string ToString() => nameof(FinalizeDocumentRevisionCommand);
}
