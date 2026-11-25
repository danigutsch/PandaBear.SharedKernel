namespace SharedKernel.Blazor;

/// <summary>
/// The options a picker shows as chosen, never the whole option set: the options of the first chosen page that an edit page loads once
/// and persists into its prerendered state, plus every option the picker looked up for another page or chose from a search since.
/// </summary>
/// <typeparam name="TKey">The key of an option, such as an image id.</typeparam>
/// <typeparam name="TOption">The option, typically a small projection with the key, a label and a thumbnail address.</typeparam>
/// <remarks>Use it on the renderer's synchronization context, like the component that reads it.</remarks>
public sealed class PickerCatalog<TKey, TOption>
    where TKey : struct, IEquatable<TKey>
    where TOption : class
{
    private readonly Dictionary<TKey, TOption> _known = [];
    private readonly Func<TOption, TKey> _keyOf;

    /// <summary>Initializes a catalog that knows the given options.</summary>
    /// <param name="keyOf">Gives the key of an option.</param>
    /// <param name="options">The options it knows at first, such as those restored from the prerendered state; a later option with the same key replaces an earlier one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keyOf"/>, <paramref name="options"/> or an option is null.</exception>
    public PickerCatalog(Func<TOption, TKey> keyOf, IEnumerable<TOption> options)
    {
        ArgumentNullException.ThrowIfNull(keyOf);
        ArgumentNullException.ThrowIfNull(options);

        _keyOf = keyOf;
        foreach (var option in options)
        {
            Remember(option);
        }
    }

    /// <summary>Gets every option the catalog knows, to persist into the prerendered state.</summary>
    public IReadOnlyList<TOption> Known => [.. _known.Values];

    /// <summary>Gives the key of an option.</summary>
    /// <param name="option">The option.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is null.</exception>
    public TKey KeyOf(TOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        return _keyOf(option);
    }

    /// <summary>Finds the option of a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The option, or <see langword="null"/> when the catalog does not know it, for example an image deleted since.</returns>
    public TOption? Find(TKey key) => _known.GetValueOrDefault(key);

    /// <summary>Remembers an option, replacing the one with the same key.</summary>
    /// <param name="option">The option.</param>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is null.</exception>
    public void Remember(TOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        _known[_keyOf(option)] = option;
    }
}
