namespace PandaBear.Blazor;

/// <summary>The "are you sure" state of a list in which at most one row waits for confirmation, for example before a delete.</summary>
/// <typeparam name="TKey">The row key type.</typeparam>
public sealed class ConfirmationState<TKey>
    where TKey : notnull
{
    private bool _hasPending;
    private TKey? _pending;

    /// <summary>Gets whether a row waits for confirmation.</summary>
    public bool HasPending => _hasPending;

    /// <summary>Starts waiting for confirmation of a row; a row that was waiting before stops waiting.</summary>
    /// <param name="key">The row key.</param>
    public void Ask(TKey key)
    {
        _pending = key;
        _hasPending = true;
    }

    /// <summary>Gets whether a specific row waits for confirmation.</summary>
    /// <param name="key">The row key.</param>
    /// <returns><see langword="true"/> when the row waits.</returns>
    public bool IsPending(TKey key) => _hasPending && EqualityComparer<TKey>.Default.Equals(_pending, key);

    /// <summary>Stops waiting for confirmation.</summary>
    public void Cancel()
    {
        _pending = default;
        _hasPending = false;
    }
}
