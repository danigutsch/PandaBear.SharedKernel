using System.Text;

namespace PandaBear.Tabular.Tests;

/// <summary>Parses the same content as text and as UTF-8, so every reading rule is checked on both entry points.</summary>
internal static class CsvParsing
{
    /// <summary>Parses the content with <see cref="CsvDocument.Parse"/> and with <see cref="CsvDocument.ParseUtf8"/>, asserting success.</summary>
    /// <param name="content">The CSV text.</param>
    /// <param name="options">The parse options.</param>
    /// <returns>The two documents.</returns>
    internal static IEnumerable<CsvDocument> ParseBoth(string content, CsvParseOptions? options = null)
    {
        yield return CsvDocument.Parse(content, options: options).ShouldBeSuccess();
        yield return CsvDocument.ParseUtf8(Encoding.UTF8.GetBytes(content), options: options).ShouldBeSuccess();
    }
}
