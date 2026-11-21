using System.Text.Json;

namespace CommunityVenue;

/// <summary>Imports and validates the read-only published venue content once at startup.</summary>
internal static class VenueCatalog
{
    /// <summary>Loads the venue translations and validates their identities and display text.</summary>
    internal static IReadOnlyList<VenuePage> Import(string path)
    {
        using var input = File.OpenRead(path);
        var pages = JsonSerializer.Deserialize(input, VenueFixtureJson.Default.VenuePageArray);
        if (pages is null || pages.Length == 0)
        {
            throw new InvalidDataException("The venue fixture needs at least one translation.");
        }

        var identities = new HashSet<(string Key, string Language)>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            if (page is null || page.Key is not ("visit" or "workshops") || page.Language is not ("en" or "pt")
                || string.IsNullOrWhiteSpace(page.Slug) || !page.Slug.All(character => char.IsAsciiLetterLower(character) || character == '-')
                || string.IsNullOrWhiteSpace(page.Title) || string.IsNullOrWhiteSpace(page.Intro)
                || string.IsNullOrWhiteSpace(page.Heading) || string.IsNullOrWhiteSpace(page.Body)
                || page.Modified == default || !identities.Add((page.Key, page.Language)) || !paths.Add(page.Path))
            {
                throw new InvalidDataException("The venue fixture contains an invalid or duplicate translation.");
            }
        }

        if (!pages.Any(page => page.Key == "visit") || !pages.Any(page => page.Key == "workshops"))
        {
            throw new InvalidDataException("The venue fixture needs a published visit and workshop page.");
        }

        return Array.AsReadOnly(pages);
    }
}
