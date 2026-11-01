namespace PandaBear.EntityFrameworkCore.Tests;

/// <summary>
/// Retention purge guard that records the owners of every batch it receives and keeps the owners it was told to hold.
/// </summary>
/// <param name="held">Owners the guard returns as held; a hold placed during the guard call can be modeled by adding to the set.</param>
internal sealed class RecordingPurgeGuard(params string[] held)
{
    private readonly HashSet<string> heldOwners = new(held, StringComparer.Ordinal);
    private readonly List<string[]> batches = [];

    /// <summary>The distinct owners received by each call, sorted, in call order.</summary>
    public IReadOnlyList<string[]> Batches => batches;

    /// <summary>
    /// Records the owners and returns the held owners.
    /// </summary>
    public ValueTask<IReadOnlySet<string>> Check(IReadOnlyCollection<string> owners, CancellationToken ct)
    {
        batches.Add([.. owners.Order(StringComparer.Ordinal)]);
        return ValueTask.FromResult<IReadOnlySet<string>>(heldOwners.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// Models a hold placed after the batch was selected: every owner of the batch becomes held.
    /// </summary>
    public ValueTask<IReadOnlySet<string>> HoldEveryOwner(IReadOnlyCollection<string> owners, CancellationToken ct)
    {
        heldOwners.UnionWith(owners);
        return Check(owners, ct);
    }
}
