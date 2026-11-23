namespace PandaBear.Blazor.Tests;

/// <summary>
/// An image library for the picker tests: images 1 to N labelled "Photo 0001" onwards with a thumbnail at <c>/thumbs/{id}.webp</c>,
/// searched by label in order and read by key, recording every search and lookup.
/// </summary>
/// <param name="count">The number of images.</param>
internal sealed class PickerLibrary(int count)
{
    private readonly List<string> _queries = [];
    private readonly List<int[]> _lookups = [];

    /// <summary>Gets the queries searched so far.</summary>
    public IReadOnlyList<string> Queries => _queries;

    /// <summary>Gets the keys of every lookup so far.</summary>
    public IReadOnlyList<int[]> Lookups => _lookups;

    /// <summary>Gives the option of an image.</summary>
    /// <param name="id">The image.</param>
    /// <returns>The option.</returns>
    public static PickerImage Option(int id) => new(id, $"Photo {id:0000}", $"/thumbs/{id}.webp");

    /// <summary>Creates a catalog that knows the given images.</summary>
    /// <param name="ids">The images.</param>
    /// <returns>The catalog.</returns>
    public static PickerCatalog<int, PickerImage> Catalog(params IEnumerable<int> ids) => new(image => image.Id, ids.Select(Option));

    /// <summary>Searches the labels for the query, ignoring case.</summary>
    /// <param name="query">The trimmed query.</param>
    /// <param name="take">The most options to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matches and their number.</returns>
    public Task<ListSlice<PickerImage>> Search(string query, int take, CancellationToken cancellationToken)
    {
        _queries.Add(query);
        PickerImage[] matches = [.. Enumerable.Range(1, count).Select(Option).Where(image => image.Label.Contains(query, StringComparison.OrdinalIgnoreCase))];
        return Task.FromResult(new ListSlice<PickerImage>([.. matches.Take(take)], matches.Length));
    }

    /// <summary>Reads the options of the images that exist.</summary>
    /// <param name="ids">The images.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The options.</returns>
    public Task<IReadOnlyList<PickerImage>> Lookup(IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        _lookups.Add([.. ids]);
        return Task.FromResult<IReadOnlyList<PickerImage>>([.. ids.Where(id => id >= 1 && id <= count).Select(Option)]);
    }
}
