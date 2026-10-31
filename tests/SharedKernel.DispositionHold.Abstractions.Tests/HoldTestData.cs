namespace PandaBear.DispositionHold.Tests;

/// <summary>
/// Builds holds with valid defaults for tests.
/// </summary>
internal static class HoldTestData
{
    /// <summary>Places a hold with valid defaults that individual tests override.</summary>
    /// <param name="scope">The scope, or a default document scope.</param>
    /// <param name="basis">The basis.</param>
    /// <param name="purposeCode">The purpose code.</param>
    /// <param name="authorityReference">The authority reference.</param>
    /// <param name="placedBy">The placing actor.</param>
    /// <param name="reviewDueAt">The review date, or six months after placement.</param>
    /// <param name="expiresAt">The optional expiry.</param>
    /// <returns>The placed hold.</returns>
    public static Hold Place(
        DispositionScope? scope = null,
        HoldBasis basis = HoldBasis.LegalClaim,
        string purposeCode = "claim-defence",
        string authorityReference = "matter-ref-17",
        string placedBy = "actor-1",
        DateTimeOffset? reviewDueAt = null,
        DateTimeOffset? expiresAt = null)
    {
        var placed = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        return Hold.Place(
            scope ?? DispositionScope.From("document", "doc-1"),
            basis,
            purposeCode,
            authorityReference,
            placedBy,
            placed,
            placed.AddDays(1),
            reviewDueAt ?? placed.AddMonths(6),
            expiresAt);
    }
}
