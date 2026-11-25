using PandaBear.DocumentWorkflow.EntityFrameworkCore;
using PandaBear.Mediator;
using PandaBear.Results;

namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>Lets the generated host dispatch delegate to the reusable finalizer.</summary>
/// <param name="handler">The EF-backed finalization handler.</param>
internal sealed class TestFinalizeDocumentRevisionHandler(
    FinalizeDocumentRevisionHandler<DocumentWorkflowTestContext, string> handler)
    : ICommandHandler<FinalizeDocumentRevisionCommand, Result<DocumentFinalizationReceipt>>
{
    /// <inheritdoc />
    public ValueTask<Result<DocumentFinalizationReceipt>> Handle(FinalizeDocumentRevisionCommand request, CancellationToken ct) =>
        handler.Handle(request.Request, ct);
}
