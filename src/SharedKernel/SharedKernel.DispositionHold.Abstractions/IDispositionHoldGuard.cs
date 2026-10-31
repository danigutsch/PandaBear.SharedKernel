namespace PandaBear.DispositionHold;

/// <summary>
/// Decides whether holds restrain disposition of the given scopes. A purge, anonymization or erasure workflow must
/// call it in the same transaction as the disposition and treat any failure as a refusal: the guard never reports
/// <see cref="DispositionHoldDecision.Clear" /> when hold state could not be determined.
/// </summary>
public interface IDispositionHoldGuard
{
    /// <summary>Checks whether any hold restrains disposition of any of the scopes at the given time.</summary>
    /// <param name="scopes">The scopes the disposition would affect.</param>
    /// <param name="at">The time at which disposition would happen.</param>
    /// <param name="ct">A token that can cancel the operation.</param>
    /// <returns>A clear decision, or a held decision naming the restraining holds.</returns>
    /// <exception cref="DispositionHoldStateUnknownException">Thrown when hold state cannot be determined; callers must refuse disposition.</exception>
    ValueTask<DispositionHoldDecision> Check(
        IReadOnlyCollection<DispositionScope> scopes,
        DateTimeOffset at,
        CancellationToken ct);
}
