using System.Globalization;

namespace PandaBear.CI.GitHub.Tests;

/// <summary>Writes release lists as the GitHub releases API returns them.</summary>
internal static class Releases
{
    /// <summary>Returns a JSON array of <paramref name="count"/> prereleases, numbered from <paramref name="first"/>.</summary>
    public static string Page(int first, int count) =>
        "[" + string.Join(',', Enumerable.Range(first, count).Select(static id => Release(id, "v1.0.0-alpha." + id.ToString(CultureInfo.InvariantCulture), prerelease: true))) + "]";

    /// <summary>Returns the JSON of one release with the properties the store reads and one it ignores.</summary>
    public static string Release(long id, string tag, bool prerelease = false, bool draft = false) => string.Create(
        CultureInfo.InvariantCulture,
        $$"""{"id":{{id}},"tag_name":"{{tag}}","draft":{{(draft ? "true" : "false")}},"prerelease":{{(prerelease ? "true" : "false")}},"created_at":"2026-01-02T03:04:05Z","name":"ignored"}""");
}
