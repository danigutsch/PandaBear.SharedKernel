namespace PandaBear.Blazor;

/// <summary>
/// A bounded part of a larger list: the items a query returned, at most the number it was asked to take, and the total number of
/// items that match it. A search or a page of a list returns one, so the page holds the slice and never the whole list.
/// </summary>
/// <typeparam name="TItem">The item type.</typeparam>
public sealed class ListSlice<TItem>
{
    /// <summary>Initializes the slice.</summary>
    /// <param name="items">The items of the slice; the caller bounds their number.</param>
    /// <param name="total">The number of items that match the query, including those outside the slice.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="total"/> is less than the number of items.</exception>
    public ListSlice(IReadOnlyList<TItem> items, int total)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(total, items.Count);

        Items = items;
        Total = total;
    }

    /// <summary>Gets the items of the slice.</summary>
    public IReadOnlyList<TItem> Items { get; }

    /// <summary>Gets the number of items that match the query, including those outside the slice.</summary>
    public int Total { get; }
}
