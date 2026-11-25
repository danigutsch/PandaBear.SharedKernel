namespace SharedKernel.Blazor;

/// <summary>The bounds of an <see cref="ImagePicker{TKey, TOption}"/> and the helper that prepares its prerendered state.</summary>
public static class ImagePicker
{
    /// <summary>The default number of options one search shows.</summary>
    public const int DefaultSearchLimit = 24;

    /// <summary>The default number of chosen options listed at a time.</summary>
    public const int DefaultChosenPageSize = 24;

    /// <summary>
    /// Gives the chosen keys a picker lists first, whose options an edit page loads and persists into its prerendered state; the options
    /// of later pages are looked up when they are shown, so the persisted state stays small however many keys are chosen.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="chosen">The chosen keys, in order.</param>
    /// <param name="pageSize">The picker's <see cref="ImagePicker{TKey, TOption}.ChosenPageSize"/>.</param>
    /// <returns>At most <paramref name="pageSize"/> keys, without repeats.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="chosen"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is zero or negative.</exception>
    public static IReadOnlyList<TKey> FirstChosenPage<TKey>(IEnumerable<TKey> chosen, int pageSize = DefaultChosenPageSize)
        where TKey : struct, IEquatable<TKey>
    {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        return [.. chosen.Take(pageSize).Distinct()];
    }
}
