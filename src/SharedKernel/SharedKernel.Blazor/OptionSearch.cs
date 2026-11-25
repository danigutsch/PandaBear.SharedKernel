using System.Globalization;

namespace SharedKernel.Blazor;

/// <summary>
/// The search state of a picker over a large option set, such as an image library: the query, the options that match it, at most
/// <see cref="Limit"/> of them, and how many match in total. Interactive Server pages must not render or hold an unbounded list, and
/// a picker that renders every option as a <c>select</c> option or checkbox, or keeps them in prerendered state, does both; this
/// state shows the first <see cref="Limit"/> matches and lets the user narrow the rest with search text.
/// </summary>
/// <typeparam name="TOption">The option type, typically a small projection with a key and a display name.</typeparam>
/// <remarks>
/// It holds no <c>[PersistentState]</c> and no options beyond the current results, and it fetches nothing until <see cref="Start"/>,
/// which the component calls from <c>OnAfterRenderAsync(firstRender)</c>; that method never runs while prerendering, so nothing is
/// fetched twice or written into the prerendered page. After that, <see cref="Search"/> waits <see cref="Debounce"/> so a burst of
/// keystrokes, each a round trip on Interactive Server, sends one query. Only the latest search applies: a newer one cancels the token
/// of the older one, and a superseded result is dropped even when it completes later. A failing search is not swallowed: its exception
/// propagates from <see cref="Search"/>, as with <see cref="ListDelete{TKey}.Run"/>, and only a cancellation caused by a newer search
/// or <see cref="Dispose"/> completes quietly. Use it on the renderer's synchronization context and dispose it with the component.
/// </remarks>
public sealed class OptionSearch<TOption> : IDisposable
{
    /// <summary>The longest query passed to the search; longer text is cut.</summary>
    public const int MaxQueryLength = 200;

    /// <summary>The default <see cref="Debounce"/> in milliseconds.</summary>
    public const int DefaultDebounceMilliseconds = 300;

    private const char AnnouncementToggle = ' ';

    private readonly Func<string, int, CancellationToken, Task<ListSlice<TOption>>> _search;
    private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(DefaultDebounceMilliseconds);
    private readonly int _minQueryLength;
    private readonly TimeProvider _timeProvider = TimeProvider.System;
    private CancellationTokenSource? _running;
    private long _generation;
    private bool _disposed;

    /// <summary>Initializes the search state.</summary>
    /// <param name="search">
    /// Finds the options that match a trimmed query (empty when the user entered nothing, so the application chooses which options to
    /// offer first), returns at most the given number of them, and the total number of matches. It receives a token that is cancelled
    /// when a newer search starts.
    /// </param>
    /// <param name="limit">The largest number of options shown at once.</param>
    /// <exception cref="ArgumentNullException"><paramref name="search"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is zero or negative.</exception>
    public OptionSearch(Func<string, int, CancellationToken, Task<ListSlice<TOption>>> search, int limit)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        _search = search;
        Limit = limit;
    }

    /// <summary>Gets the largest number of options shown at once.</summary>
    public int Limit { get; }

    /// <summary>
    /// Gets how long <see cref="Search"/> waits for more typing before it queries; a newer search during the wait replaces it. The default
    /// is <see cref="DefaultDebounceMilliseconds"/> (300 ms); <see cref="TimeSpan.Zero"/> queries at once. <see cref="Start"/> never waits.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan Debounce
    {
        get => _debounce;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            _debounce = value;
        }
    }

    /// <summary>
    /// Gets the shortest query, after trimming, that is searched, from 0 (the default, where an empty query asks for the application's
    /// first options) to <see cref="MaxQueryLength"/>. A shorter query fetches nothing and empties the results.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or above <see cref="MaxQueryLength"/>.</exception>
    public int MinQueryLength
    {
        get => _minQueryLength;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxQueryLength);
            _minQueryLength = value;
        }
    }

    /// <summary>Gets the clock of the <see cref="Debounce"/> wait; <see cref="TimeProvider.System"/> by default.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public TimeProvider TimeProvider
    {
        get => _timeProvider;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _timeProvider = value;
        }
    }

    /// <summary>Gets whether <see cref="Start"/> was called, so searches fetch options.</summary>
    public bool IsStarted { get; private set; }

    /// <summary>Gets the query of the latest search, trimmed and at most <see cref="MaxQueryLength"/> characters long.</summary>
    public string Query { get; private set; } = string.Empty;

    /// <summary>Gets whether <see cref="Query"/> is shorter than <see cref="MinQueryLength"/>, so the user should type more.</summary>
    public bool IsQueryTooShort => Query.Length < MinQueryLength;

    /// <summary>
    /// Gets the options of the last completed search, at most <see cref="Limit"/>. They stay while a newer search runs and are emptied
    /// when the latest search fails or its query is too short.
    /// </summary>
    public IReadOnlyList<TOption> Results { get; private set; } = [];

    /// <summary>Gets the number of options that match the query of <see cref="Results"/>, including those not shown.</summary>
    public int Total { get; private set; }

    /// <summary>Gets whether more options match than <see cref="Results"/> shows, so the user should narrow the search.</summary>
    public bool IsTruncated => Total > Results.Count;

    /// <summary>Gets whether the latest search waits for its <see cref="Debounce"/> or runs.</summary>
    public bool IsSearching { get; private set; }

    /// <summary>Gets whether <see cref="Results"/> belong to <see cref="Query"/>: the latest search completed and did not fail.</summary>
    public bool HasResults { get; private set; }

    /// <summary>
    /// Gets a number that changes with every completed search, also when its announcement reads the same as the previous one. Use it as
    /// the <c>@key</c> of content that must be rendered afresh for every search; <see cref="Status"/> already changes with it.
    /// </summary>
    public int AnnouncementKey { get; private set; }

    /// <summary>
    /// Allows searches to fetch options and searches the current <see cref="Query"/> at once, without <see cref="Debounce"/>. Call it
    /// from <c>OnAfterRenderAsync</c> when <c>firstRender</c> is <see langword="true"/>, which never runs while prerendering; later
    /// calls do nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels this search.</param>
    /// <returns>A task that completes when the first search has finished or was superseded.</returns>
    /// <exception cref="ObjectDisposedException">The state was disposed.</exception>
    /// <exception cref="InvalidOperationException">The search returned no slice.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled while this was the latest search.</exception>
    public Task Start(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsStarted)
        {
            return Task.CompletedTask;
        }

        IsStarted = true;
        return Run(Query, TimeSpan.Zero, cancellationToken);
    }

    /// <summary>
    /// Searches for the options that match <paramref name="query"/> after <see cref="Debounce"/>, and keeps them when this is still the
    /// latest search. A search started while another waits or runs cancels the older one. Before <see cref="Start"/> it only records
    /// the query.
    /// </summary>
    /// <param name="query">The search text; it is trimmed and cut to <see cref="MaxQueryLength"/>, and <see langword="null"/> is empty.</param>
    /// <param name="cancellationToken">Cancels this search, for example when the component is disposed.</param>
    /// <returns>A task that completes when the search has finished or was superseded.</returns>
    /// <exception cref="ObjectDisposedException">The state was disposed.</exception>
    /// <exception cref="InvalidOperationException">The search returned no slice.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled while this was the latest search.</exception>
    public Task Search(string? query, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var text = Bound(query);
        if (!IsStarted)
        {
            Query = text;
            return Task.CompletedTask;
        }

        return Run(text, _debounce, cancellationToken);
    }

    /// <summary>
    /// Builds the announcement of the latest search for one polite live region (<c>role="status"</c> with <c>aria-atomic="true"</c>),
    /// never the results list itself, from caller-authored formats. The text of two consecutive searches always differs, by a trailing
    /// no-break space that assistive technology does not read, so a search that yields the same counts is announced again.
    /// </summary>
    /// <param name="shownFormat">
    /// A composite format for matches, for example <c>"{0} of {1} images for ‘{2}’"</c>: <c>{0}</c> is the number of options shown,
    /// <c>{1}</c> the total, both formatted with the current culture, and <c>{2}</c> the query.
    /// </param>
    /// <param name="noMatchesFormat">A composite format for no matches, for example <c>"No images for ‘{0}’"</c>; <c>{0}</c> is the query.</param>
    /// <returns>The announcement, or <see langword="null"/> while no search for <see cref="Query"/> has completed.</returns>
    /// <exception cref="ArgumentNullException">A format is null.</exception>
    /// <exception cref="FormatException">A format is not a valid composite format.</exception>
    public string? Status(string shownFormat, string noMatchesFormat)
    {
        ArgumentNullException.ThrowIfNull(shownFormat);
        ArgumentNullException.ThrowIfNull(noMatchesFormat);

        if (!HasResults)
        {
            return null;
        }

        var text = Total == 0
            ? string.Format(CultureInfo.CurrentCulture, noMatchesFormat, Query)
            : string.Format(CultureInfo.CurrentCulture, shownFormat, Results.Count, Total, Query);
        return AnnouncementKey % 2 == 0 ? text + AnnouncementToggle : text;
    }

    /// <summary>Cancels a waiting or running search, whose result is then dropped, and refuses further searches.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _generation++;
        IsSearching = false;
        _running?.Cancel();
        _running?.Dispose();
        _running = null;
    }

    private async Task Run(string text, TimeSpan delay, CancellationToken cancellationToken)
    {
        var previous = _running;
        var generation = ++_generation;
        Query = text;
        HasResults = false;
        if (text.Length < _minQueryLength)
        {
            _running = null;
            IsSearching = false;
            Results = [];
            Total = 0;
            if (previous is not null)
            {
                await previous.CancelAsync().ConfigureAwait(true);
            }

            return;
        }

        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _running = source;
        IsSearching = true;
        try
        {
            if (previous is not null)
            {
                await previous.CancelAsync().ConfigureAwait(true);
                if (generation != _generation)
                {
                    return;
                }
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, _timeProvider, source.Token).ConfigureAwait(true);
                if (generation != _generation)
                {
                    return;
                }
            }

            var slice = await _search(text, Limit, source.Token).ConfigureAwait(true)
                ?? throw new InvalidOperationException("The option search returned no slice.");
            if (generation != _generation)
            {
                return;
            }

            Results = slice.Items.Count > Limit ? [.. slice.Items.Take(Limit)] : slice.Items;
            Total = slice.Total;
            HasResults = true;
            AnnouncementKey++;
        }
        catch (OperationCanceledException) when (generation != _generation)
        {
        }
        catch when (generation == _generation)
        {
            Results = [];
            Total = 0;
            throw;
        }
        finally
        {
            if (generation == _generation)
            {
                IsSearching = false;
                _running = null;
            }
        }
    }

    private static string Bound(string? query)
    {
        var text = query?.Trim() ?? string.Empty;
        if (text.Length <= MaxQueryLength)
        {
            return text;
        }

        var keep = char.IsHighSurrogate(text[MaxQueryLength - 1]) ? MaxQueryLength - 1 : MaxQueryLength;
        return text[..keep].TrimEnd();
    }
}
