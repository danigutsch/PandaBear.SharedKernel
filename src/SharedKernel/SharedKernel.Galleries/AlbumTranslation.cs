using PandaBear.BuildingBlocks;

namespace PandaBear.Galleries;

/// <summary>The validated text of an album in one culture: its title, slug and publication status.</summary>
public sealed class AlbumTranslation
{
    private AlbumTranslation(ContentCulture culture, string title, Slug slug, PublicationStatus status)
    {
        Culture = culture;
        Title = title;
        Slug = slug;
        Status = status;
    }

    /// <summary>Gets the culture.</summary>
    public ContentCulture Culture { get; }

    /// <summary>Gets the sanitized title.</summary>
    public string Title { get; }

    /// <summary>Gets the slug of the album's address in this culture.</summary>
    public Slug Slug { get; }

    /// <summary>Gets whether the translation is published.</summary>
    public PublicationStatus Status { get; }

    /// <summary>For persistence mappers: rebuilds a translation stored earlier; it performs no validation beyond null checks.</summary>
    /// <param name="culture">The culture.</param>
    /// <param name="title">The title.</param>
    /// <param name="slug">The slug.</param>
    /// <param name="status">The status.</param>
    /// <returns>The translation.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static AlbumTranslation Restore(ContentCulture culture, string title, Slug slug, PublicationStatus status)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(slug);

        return new AlbumTranslation(culture, title, slug, status);
    }

    /// <summary>Determines whether another translation has the same data.</summary>
    /// <param name="other">The other translation.</param>
    /// <returns><see langword="true"/> when culture, title, slug and status are equal.</returns>
    internal bool SameAs(AlbumTranslation other) =>
        Culture == other.Culture && string.Equals(Title, other.Title, StringComparison.Ordinal) && Slug == other.Slug && Status == other.Status;
}
