using PandaBear.Results;

namespace PandaBear.Galleries.Tests;

/// <summary>Cultures, rules and definitions shared by the album tests.</summary>
internal static class AlbumFixtures
{
    /// <summary>Gets Brazilian Portuguese, the default culture.</summary>
    public static ContentCulture Portuguese { get; } = ContentCulture.Restore("pt-BR");

    /// <summary>Gets English.</summary>
    public static ContentCulture English { get; } = ContentCulture.Restore("en");

    /// <summary>Gets the album cultures: Brazilian Portuguese (default) and English.</summary>
    public static SupportedCultures Cultures { get; } = SupportedCultures.Create(Portuguese, [Portuguese, English]).Value;

    /// <summary>Gets rules with at most four photos and titles of at most 40 characters.</summary>
    public static AlbumRules Rules { get; } = new(Cultures, maxPhotos: 4) { MaxTitleLength = 40 };

    /// <summary>Builds a translation definition.</summary>
    /// <param name="culture">The culture name.</param>
    /// <param name="title">The title.</param>
    /// <param name="status">The status.</param>
    /// <param name="slug">The slug, or <see langword="null"/> to derive it from the title.</param>
    /// <returns>The definition.</returns>
    public static AlbumTranslationDefinition Translation(string? culture, string? title, PublicationStatus status = PublicationStatus.Draft, string? slug = null)
        => new(culture, title, slug, status);

    /// <summary>Builds an album definition.</summary>
    /// <param name="photos">The photos.</param>
    /// <param name="translations">The translations; a Portuguese draft when none is given.</param>
    /// <returns>The definition.</returns>
    public static AlbumDefinition<Guid, Guid> Definition(IReadOnlyList<Guid> photos, params AlbumTranslationDefinition[] translations)
        => new(0, null, photos, null, translations.Length == 0 ? [Translation("pt-BR", "Patagônia")] : translations);

    /// <summary>Reads the field paths of the error of a failed result, in order.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The field paths.</returns>
    public static string[] Fields(ResultError? error) =>
        [.. (error?.ValidationErrors ?? throw new InvalidOperationException("No validation errors.")).Keys.Order(StringComparer.Ordinal)];

    /// <summary>Reads the messages of one field of the error of a failed result.</summary>
    /// <param name="error">The error.</param>
    /// <param name="field">The field path.</param>
    /// <returns>The messages.</returns>
    public static IReadOnlyList<string> Messages(ResultError? error, string field) =>
        (error?.ValidationErrors ?? throw new InvalidOperationException("No validation errors."))[field];

    /// <summary>Creates an album published in both cultures, with photos eligible in both.</summary>
    /// <param name="library">The photo library that receives the photos.</param>
    /// <param name="count">The number of photos.</param>
    /// <param name="photos">The photos, in album order.</param>
    /// <returns>The album, sort order 4, without a related item.</returns>
    public static Album<Guid, Guid> PublishedAlbum(PhotoLibrary library, int count, out Guid[] photos)
    {
        photos = [.. Enumerable.Range(0, count).Select(_ => library.Add(Portuguese, English))];
        return Album.Create(
            new AlbumDefinition<Guid, Guid>(4, null, photos, null,
                [Translation("pt-BR", "Patagônia", PublicationStatus.Published), Translation("en", "Patagonia", PublicationStatus.Published)]),
            Rules,
            library).ShouldBeSuccess();
    }
}
