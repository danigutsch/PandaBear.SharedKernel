namespace SharedKernel.Blazor.Site.Tests;

/// <summary>A rule of the stylesheet, as <see cref="SiteFiles.Rules" /> reads it.</summary>
/// <param name="Selectors">The selectors of the rule, trimmed, in source order.</param>
/// <param name="Body">The declarations between the braces.</param>
/// <param name="Position">The offset of the rule in the stylesheet without comments, for comparing source order.</param>
internal sealed record CssRule(IReadOnlyList<string> Selectors, string Body, int Position)
{
    /// <summary>Joins the trimmed declarations whose property starts with one of the prefixes, in source order.</summary>
    /// <param name="prefixes">The property prefixes, such as <c>--_site-</c>.</param>
    /// <returns>The declarations without separators between them.</returns>
    public string Declarations(params string[] prefixes) => string.Concat(Body.Split('\n')
        .Select(line => line.Trim())
        .Where(line => prefixes.Any(prefix => line.StartsWith(prefix, StringComparison.Ordinal))));
}
