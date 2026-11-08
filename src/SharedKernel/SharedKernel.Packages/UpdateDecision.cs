using PandaBear.BuildingBlocks;

namespace PandaBear.Packages;

/// <summary>The policy's answer for one candidate, with the reason in words.</summary>
public sealed class UpdateDecision : ValueObject
{
    /// <summary>Creates a decision.</summary>
    /// <param name="kind">What the policy says.</param>
    /// <param name="candidate">The candidate that was judged.</param>
    /// <param name="reason">Why, in one sentence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="candidate"/> or <paramref name="reason"/> is <see langword="null"/>.</exception>
    public UpdateDecision(UpdateDecisionKind kind, UpdateCandidate candidate, string reason)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(reason);
        Kind = kind;
        Candidate = candidate;
        Reason = reason;
    }

    /// <summary>Gets what the policy says.</summary>
    public UpdateDecisionKind Kind
    {
        get;
    }

    /// <summary>Gets the candidate that was judged.</summary>
    public UpdateCandidate Candidate
    {
        get;
    }

    /// <summary>Gets why, in one sentence.</summary>
    public string Reason
    {
        get;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Kind}: {Candidate} - {Reason}";

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kind;
        yield return Candidate;
        yield return Reason;
    }
}
