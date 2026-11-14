using System.Diagnostics;
using Microsoft.Playwright;

namespace PandaBear.Testing.Playwright;

/// <summary>Locator helpers for Blazor forms.</summary>
public static class PlaywrightLocatorExtensions
{
    private const int Attempts = 3;
    private const float QuickCheckMilliseconds = 750;

    // Blazor's InputFile.init sets this property on the element in the same call that attaches the change listener.
    private const string BlazorInputFileReadyScript = "element => element._blazorInputFileNextFileId !== undefined";

    // Playwright's default timeout, and how often the input is checked within it.
    private static readonly TimeSpan InputFileReadyTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InputFileReadyPollInterval = TimeSpan.FromMilliseconds(50);

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

    /// <summary>
    /// Chooses a file in a Blazor <c>InputFile</c> once the input can report it. Blazor attaches the input's change listener with a JS
    /// interop call from the component's first interactive after-render, which reaches the browser after the page already shows the
    /// interactivity marker; a file set before that is never sent to the server, and the form then acts as if no file was chosen. The wait
    /// polls, through the locator so that an input replaced by a render is found again, for the property that Blazor's listener setup
    /// writes on the element.
    /// </summary>
    /// <param name="input">The file input that an <c>InputFile</c> component rendered.</param>
    /// <param name="file">The file to choose.</param>
    /// <returns>A task that completes when the input has been handed the file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> or <paramref name="file"/> is <see langword="null"/>.</exception>
    /// <exception cref="TimeoutException">Blazor did not set up the input within 30 seconds, Playwright's default timeout.</exception>
    public static async Task SetBlazorInputFile(this ILocator input, FilePayload file)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(file);

        // Poll through the locator, which finds the element again each time: an interactive render can replace the prerendered input,
        // and a handle to the replaced element would never become ready.
        var waited = Stopwatch.StartNew();
        while (!await input.EvaluateAsync<bool>(BlazorInputFileReadyScript).ConfigureAwait(false))
        {
            if (waited.Elapsed > InputFileReadyTimeout)
            {
                throw new TimeoutException($"Blazor did not set up the file input within {InputFileReadyTimeout.TotalSeconds} seconds.");
            }

            await Task.Delay(InputFileReadyPollInterval).ConfigureAwait(false);
        }

        await input.SetInputFilesAsync(file).ConfigureAwait(false);
    }

    private static bool UsesNativeValueSetter(string? inputType) =>
        inputType is "date" or "datetime-local" or "month" or "number" or "time" or "week";
}
