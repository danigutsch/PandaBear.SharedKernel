using SharedKernel.DocumentWorkflow;
using SharedKernel.DocumentWorkflow.EntityFrameworkCore;
using SharedKernel.Idempotency;
using SharedKernel.Review;

namespace SharedKernel.EntityFrameworkCore.Tests;

/// <summary>Creates the authorized finalization request for an approved neutral release document.</summary>
internal static class ReleaseDocumentFinalizationSetup
{
    /// <summary>Gets the operator pseudonym used by the test host.</summary>
    public const string Actor = "release-operator";

    /// <summary>Gets the fixed clock value used by the test host.</summary>
    public static DateTimeOffset RecordedAt { get; } = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a finalization request from a committed approved revision.</summary>
    public static DocumentFinalizationRequest ToRequest(PersistedDocumentRevision approved) => new(
        approved.Revision.LineageId, approved.Revision.Number, approved.Version, approved.Revision.Review.Revision,
        Actor, IdempotencyKey.From("private-client-token"));

    /// <summary>Creates the first draft and saves the approved state through the normal store.</summary>
    public static async ValueTask<PersistedDocumentRevision> PersistApproved(
        DocumentGenerator<string> generator, PostgreSqlDocumentWorkflowScenario scenario, CancellationToken ct)
    {
        var generated = await generator.Generate(Guid.NewGuid(), Guid.NewGuid(), ["release"], ct);
        var draft = generated.ShouldBeSuccess();
        var created = await scenario.Run(store => store.Create(draft, ct));
        var review = draft.BeginReview().ShouldBeSuccess();
        var approved = review.Decide(review.Review.Revision, "release", ReviewDecision.Approved, Actor,
            RecordedAt, "checked", review.Origin).ShouldBeSuccess();
        var persisted = await scenario.Run(store => store.Save(approved, created.ShouldBeSuccess().Version, ct));
        return persisted.ShouldBeSuccess();
    }
}
