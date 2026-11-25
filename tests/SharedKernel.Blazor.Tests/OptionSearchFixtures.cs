namespace SharedKernel.Blazor.Tests;

/// <summary>Searches and states for the option search tests.</summary>
internal static class OptionSearchFixtures
{
    /// <summary>Finds no options.</summary>
    /// <param name="query">The query.</param>
    /// <param name="take">The largest number of options to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An empty slice.</returns>
    public static Task<ListSlice<string>> NoOptions(string query, int take, CancellationToken cancellationToken) =>
        Task.FromResult(new ListSlice<string>([], 0));

    /// <summary>Creates a search state without debounce that has started, so every search runs at once.</summary>
    /// <typeparam name="TOption">The option type.</typeparam>
    /// <param name="search">The application search.</param>
    /// <param name="limit">The limit.</param>
    /// <param name="minQueryLength">The minimum query length.</param>
    /// <returns>The started state; its first search, for the empty query, has run.</returns>
    public static async Task<OptionSearch<TOption>> Started<TOption>(
        Func<string, int, CancellationToken, Task<ListSlice<TOption>>> search, int limit, int minQueryLength = 0)
    {
        var state = new OptionSearch<TOption>(search, limit) { Debounce = TimeSpan.Zero, MinQueryLength = minQueryLength };
        await state.Start(Xunit.TestContext.Current.CancellationToken);
        return state;
    }
}
