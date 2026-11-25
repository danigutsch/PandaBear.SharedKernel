using SharedKernel.BuildingBlocks;

namespace SharedKernel.Galleries;

/// <summary>
/// The field paths of album validation errors, so an application can place each error on its control. The fields of a translation are
/// prefixed with its culture, for example <c>pt-BR.title</c>; see <see cref="Of"/>.
/// </summary>
public static class AlbumFields
{
    /// <summary>The photo sequence: too many photos, a photo that does not exist, or a photo that appears twice.</summary>
    public const string PhotoIds = "photoIds";

    /// <summary>The chosen cover, which is not one of the photos.</summary>
    public const string CoverId = "coverId";

    /// <summary>The set of translations: an unknown culture, a culture given twice, or no translation in the default culture.</summary>
    public const string Translations = "translations";

    /// <summary>The title of a translation.</summary>
    public const string Title = "title";

    /// <summary>The slug of a translation.</summary>
    public const string Slug = "slug";

    /// <summary>The publication status of a translation.</summary>
    public const string Status = "status";

    /// <summary>The publish rule of a translation: no photo, or photos that are not eligible in its culture.</summary>
    public const string Photos = "photos";

    /// <summary>Builds the path of a translation's field, such as <c>pt-BR.title</c>.</summary>
    /// <param name="culture">The culture of the translation.</param>
    /// <param name="field">The field, one of <see cref="Title"/>, <see cref="Slug"/>, <see cref="Status"/> and <see cref="Photos"/>.</param>
    /// <returns>The path.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static string Of(ContentCulture culture, string field)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(field);

        return culture.Value + "." + field;
    }
}
