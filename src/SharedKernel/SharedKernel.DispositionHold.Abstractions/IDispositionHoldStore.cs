namespace PandaBear.DispositionHold;

/// <summary>
/// Stores holds. Changes are staged and committed by the caller's unit of work, so placing or releasing a hold shares
/// a transaction with the caller's own writes.
/// </summary>
public interface IDispositionHoldStore
{
    /// <summary>Stages a newly placed hold for the next commit.</summary>
    /// <param name="hold">The hold to store.</param>
    void Add(Hold hold);

    /// <summary>Finds a hold by identifier.</summary>
    /// <param name="id">The hold identifier.</param>
    /// <param name="ct">A token that can cancel the operation.</param>
    /// <returns>The tracked hold, or <see langword="null" /> when none exists.</returns>
    ValueTask<Hold?> Find(Guid id, CancellationToken ct);
}
