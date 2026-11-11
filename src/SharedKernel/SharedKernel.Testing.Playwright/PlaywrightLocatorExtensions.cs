using Microsoft.Playwright;

namespace PandaBear.Testing.Playwright;

/// <summary>Locator helpers for Blazor forms.</summary>
public static class PlaywrightLocatorExtensions
{
    private const int Attempts = 3;
    private const float QuickCheckMilliseconds = 750;

    /// <summary>
    /// Fills a field, leaves it (Blazor commits text on the change event) and checks it kept the value; a render that resets the field right
    /// after the page becomes interactive is answered by filling again, at most three times.
    /// </summary>
    /// <param name="locator">The field.</param>
    /// <param name="value">The value.</param>
    /// <returns>A task that completes when the field holds the value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="locator"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="PlaywrightException">The field did not keep the value.</exception>
    public static async Task FillAndExpectValue(this ILocator locator, string value)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(value);
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            await Assertions.Expect(locator).ToBeEditableAsync().ConfigureAwait(false);
            if (UsesNativeValueSetter(await locator.GetAttributeAsync("type").ConfigureAwait(false)))
            {
                await locator.FillAsync(value).ConfigureAwait(false);
            }
            else
            {
                await locator.ClearAsync().ConfigureAwait(false);
                if (value.Length > 0)
                {
                    await locator.PressSequentiallyAsync(value).ConfigureAwait(false);
                }
            }

            await locator.BlurAsync().ConfigureAwait(false);
            try
            {
                await Assertions.Expect(locator)
                    .ToHaveValueAsync(value, new LocatorAssertionsToHaveValueOptions { Timeout = QuickCheckMilliseconds })
                    .ConfigureAwait(false);
                return;
            }
            catch (PlaywrightException) when (attempt < Attempts)
            {
                // An interactive render can replace the prerendered field right after it accepted input.
            }
        }
    }

    private static bool UsesNativeValueSetter(string? inputType) =>
        inputType is "date" or "datetime-local" or "month" or "number" or "time" or "week";
}
