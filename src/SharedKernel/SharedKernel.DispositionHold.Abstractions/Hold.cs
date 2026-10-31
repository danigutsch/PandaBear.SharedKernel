namespace PandaBear.DispositionHold;

/// <summary>
/// A hold that blocks deletion or anonymization of whatever its <see cref="Scope" /> names. It records who placed and
/// released it and why, using opaque identifiers only, never content or personal data. A hold restrains disposition;
/// it does not define retention periods or any legal rule.
/// </summary>
/// <remarks>
/// The identifier alphabet excludes spaces and <c>@</c>, but it cannot tell an opaque code from one derived from
/// personal data. Callers must use identifiers that are not derivable from a person's name, address or document number.
/// </remarks>
public sealed class Hold
{
    /// <summary>The maximum length of purpose, authority, actor and reason identifiers.</summary>
    public const int MaxIdentifierLength = 128;

    private Hold(
        Guid id,
        DispositionScope scope,
        HoldBasis basis,
        string purposeCode,
        string authorityReference,
        string placedBy,
        DateTimeOffset placedAt,
        DateTimeOffset effectiveFrom,
        DateTimeOffset reviewDueAt,
        DateTimeOffset? expiresAt)
    {
        Id = id;
        Scope = scope;
        Basis = basis;
        PurposeCode = purposeCode;
        AuthorityReference = authorityReference;
        PlacedBy = placedBy;
        PlacedAt = placedAt;
        EffectiveFrom = effectiveFrom;
        ReviewDueAt = reviewDueAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>Required by object-relational mappers; do not call.</summary>
    private Hold()
    {
    }

    /// <summary>Gets the opaque hold identifier.</summary>
    public Guid Id
    {
        get; private init;
    }

    /// <summary>Gets what the hold restrains.</summary>
    public DispositionScope Scope
    {
        get; private init;
    }

    /// <summary>Gets why the hold exists.</summary>
    public HoldBasis Basis
    {
        get; private init;
    }

    /// <summary>Gets the opaque purpose code the hold serves.</summary>
    public string PurposeCode { get; private init; } = default!;

    /// <summary>Gets the opaque reference to the restricted record that authorizes the hold.</summary>
    public string AuthorityReference { get; private init; } = default!;

    /// <summary>Gets the opaque actor that placed the hold.</summary>
    public string PlacedBy { get; private init; } = default!;

    /// <summary>Gets when the hold was placed.</summary>
    public DateTimeOffset PlacedAt
    {
        get; private init;
    }

    /// <summary>Gets when the hold starts to restrain disposition.</summary>
    public DateTimeOffset EffectiveFrom
    {
        get; private init;
    }

    /// <summary>Gets when the hold is next due for review.</summary>
    public DateTimeOffset ReviewDueAt
    {
        get; private init;
    }

    /// <summary>Gets when the hold stops restraining disposition by itself, or <see langword="null" /> when it needs an explicit release.</summary>
    public DateTimeOffset? ExpiresAt
    {
        get; private init;
    }

    /// <summary>Gets the opaque actor that released the hold, or <see langword="null" /> while it has not been released.</summary>
    public string? ReleasedBy
    {
        get; private set;
    }

    /// <summary>Gets when the hold was released, or <see langword="null" /> while it has not been released.</summary>
    public DateTimeOffset? ReleasedAt
    {
        get; private set;
    }

    /// <summary>Gets the opaque reason code recorded at release, or <see langword="null" /> while the hold has not been released.</summary>
    public string? ReleaseReasonCode
    {
        get; private set;
    }

    /// <summary>Gets whether the hold has been explicitly released.</summary>
    public bool IsReleased => ReleasedAt is not null;

    /// <summary>Places a hold.</summary>
    /// <param name="scope">What the hold restrains.</param>
    /// <param name="basis">Why the hold exists.</param>
    /// <param name="purposeCode">The opaque purpose code.</param>
    /// <param name="authorityReference">The opaque reference to the authorizing record.</param>
    /// <param name="placedBy">The opaque actor placing the hold.</param>
    /// <param name="placedAt">When the hold is placed.</param>
    /// <param name="effectiveFrom">When the hold starts to restrain disposition.</param>
    /// <param name="reviewDueAt">When the hold is next due for review.</param>
    /// <param name="expiresAt">When the hold stops restraining disposition by itself, or <see langword="null" />.</param>
    /// <returns>The placed hold.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an identifier is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the scope is default, the basis is undefined, an identifier is invalid, or the dates are not ordered.</exception>
    public static Hold Place(
        DispositionScope scope,
        HoldBasis basis,
        string? purposeCode,
        string? authorityReference,
        string? placedBy,
        DateTimeOffset placedAt,
        DateTimeOffset effectiveFrom,
        DateTimeOffset reviewDueAt,
        DateTimeOffset? expiresAt = null)
    {
        _ = scope.Kind;
        if (!Enum.IsDefined(basis))
        {
            throw new ArgumentException("The hold basis is not defined.", nameof(basis));
        }

        if (reviewDueAt <= effectiveFrom)
        {
            throw new ArgumentException("The review date must be after the effective date.", nameof(reviewDueAt));
        }

        if (expiresAt is { } expiry && expiry <= effectiveFrom)
        {
            throw new ArgumentException("The expiry must be after the effective date.", nameof(expiresAt));
        }

        return new Hold(
            Guid.CreateVersion7(),
            scope,
            basis,
            DispositionIdentifier.Require(purposeCode, nameof(purposeCode), MaxIdentifierLength),
            DispositionIdentifier.Require(authorityReference, nameof(authorityReference), MaxIdentifierLength),
            DispositionIdentifier.Require(placedBy, nameof(placedBy), MaxIdentifierLength),
            placedAt,
            effectiveFrom,
            reviewDueAt,
            expiresAt);
    }

    /// <summary>Releases the hold explicitly.</summary>
    /// <param name="releasedBy">The opaque actor releasing the hold.</param>
    /// <param name="reasonCode">The opaque reason code for the release.</param>
    /// <param name="releasedAt">When the hold is released.</param>
    /// <exception cref="ArgumentNullException">Thrown when an identifier is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an identifier is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the hold was already released.</exception>
    public void Release(string? releasedBy, string? reasonCode, DateTimeOffset releasedAt)
    {
        var actor = DispositionIdentifier.Require(releasedBy, nameof(releasedBy), MaxIdentifierLength);
        var reason = DispositionIdentifier.Require(reasonCode, nameof(reasonCode), MaxIdentifierLength);
        if (IsReleased)
        {
            throw new InvalidOperationException("The hold was already released.");
        }

        ReleasedBy = actor;
        ReleaseReasonCode = reason;
        ReleasedAt = releasedAt;
    }

    /// <summary>Gets whether the hold restrains disposition at the given time.</summary>
    /// <param name="at">The time to evaluate.</param>
    /// <returns><see langword="true" /> when the hold is effective, not expired and not released.</returns>
    public bool RestrainsAt(DateTimeOffset at) =>
        !IsReleased && EffectiveFrom <= at && (ExpiresAt is null || at < ExpiresAt);
}
