using System.Security.Cryptography;
using System.Text;
using Microsoft.Playwright;

namespace PandaBear.Testing.Playwright;

/// <summary>Saves what explains a failed browser test: a full-page screenshot and the trace of the page's context.</summary>
public static class PlaywrightArtifacts
{
    private const int MaximumReadableLength = 100;
    private const int HashLength = 8;

    /// <summary>
    /// Saves <c>&lt;name&gt;-&lt;hash&gt;.png</c> (full page) and <c>&lt;name&gt;-&lt;hash&gt;.zip</c> (the trace; open it with
    /// <c>playwright show-trace</c>) in a folder, stopping the context's tracing, which must have been started. Characters that cannot appear
    /// in a file name, spaces, quotes, colons and parentheses in <paramref name="name"/> become <c>_</c>, the result is cut to
    /// 100 characters, and the first eight hexadecimal digits of the SHA-256 of the full <paramref name="name"/> keep names that look alike
    /// apart.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="folder">The folder; created when missing.</param>
    /// <param name="name">The base name, such as the test's display name.</param>
    /// <returns>The screenshot and trace paths.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="folder"/> or <paramref name="name"/> is blank.</exception>
    public static async Task<IReadOnlyList<string>> Save(IPage page, string folder, string name)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _ = Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, FileName(name));
        var screenshot = file + ".png";
        var trace = file + ".zip";
        _ = await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshot, FullPage = true }).ConfigureAwait(false);
        await page.Context.Tracing.StopAsync(new TracingStopOptions { Path = trace }).ConfigureAwait(false);
        return [screenshot, trace];
    }

    private static string FileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var readable = new string([.. name.Select(character => invalid.Contains(character) || character is ' ' or '(' or ')' or '"' or ':' ? '_' : character)]);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..HashLength];
        if (readable.Length > MaximumReadableLength)
        {
            readable = readable[..(char.IsHighSurrogate(readable[MaximumReadableLength - 1]) ? MaximumReadableLength - 1 : MaximumReadableLength)];
        }

        return string.Concat(readable, "-", hash);
    }
}
