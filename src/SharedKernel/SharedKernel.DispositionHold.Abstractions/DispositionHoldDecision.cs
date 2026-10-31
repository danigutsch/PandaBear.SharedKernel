namespace PandaBear.DispositionHold;

/// <summary>
/// The outcome of a guard check. A clear decision is the only one that permits disposition.
/// </summary>
public readonly record struct DispositionHoldDecision
{
    private readonly IReadOnlyList<Guid>? holdIds;

    private DispositionHoldDecision(IReadOnlyList<Guid> holdIds)
    {
        this.holdIds = holdIds;
    }

    /// <summary>Gets the identifiers of the holds that restrain disposition; empty when the decision is clear.</summary>
    public IReadOnlyList<Guid> HoldIds => holdIds ?? [];

    /// <summary>Gets whether no hold restrains disposition.</summary>
    public bool IsClear => HoldIds.Count == 0;

    /// <summary>Gets a decision that permits disposition.</summary>
    public static DispositionHoldDecision Clear { get; } = new([]);

    /// <summary>Creates a decision that forbids disposition.</summary>
    /// <param name="holdIds">The restraining holds.</param>
    /// <returns>The held decision.</returns>
    /// <exception cref="ArgumentException">Thrown when no hold is given.</exception>
    public static DispositionHoldDecision HeldBy(IReadOnlyList<Guid> holdIds)
    {
        ArgumentNullException.ThrowIfNull(holdIds);
        if (holdIds.Count == 0)
        {
            throw new ArgumentException("A held decision needs at least one hold.", nameof(holdIds));
        }

        return new DispositionHoldDecision(holdIds);
    }
}
