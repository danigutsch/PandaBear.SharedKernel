using System.Globalization;

namespace PandaBear.CI.Forgejo.Tests;

/// <summary>Writes release lists as the Forgejo releases API returns them.</summary>
internal static class Releases
{
    /// <summary>Returns a JSON array of <paramref name="count"/> prereleases, numbered from <paramref name="first"/>.</summary>
    public static string Page(int first, int count) =>
        "[" + string.Join(',', Enumerable.Range(first, count).Select(static id => string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"id":{{id}},"tag_name":"v1.0.0-alpha.{{id}}","draft":false,"prerelease":true,"created_at":"2026-01-02T03:04:05Z","hide_archive_links":false}"""))) + "]";
}
