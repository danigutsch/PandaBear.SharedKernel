using SharedKernel.BuildingBlocks;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// One entry of a <see cref="CreditsList" />: the attribution of an image as Creative Commons recommends it (title, author, source,
/// licence), with what was changed. Every text is the application's; <see cref="FromCredit" /> builds one from a shared
/// <see cref="ImageCredit" />.
/// </summary>
/// <param name="Author">The author as the licence asks to credit them, such as "Ana Lima". It must not be blank.</param>
/// <param name="LicenseText">The licence, such as "CC BY-SA 4.0" or "All rights reserved". It must not be blank.</param>
public sealed record CreditItem(string Author, string LicenseText)
{
    /// <summary>Gets an optional small image of the work, loaded lazily; usually decorative, because the text names the work.</summary>
    public ResponsiveImage? Thumbnail { get; init; }

    /// <summary>Gets the work's title, linked to <see cref="SourceHref" />; null or blank shows none.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the address of the author's page, such as their profile, which links <see cref="Author" />.</summary>
    public LinkTarget? AuthorHref { get; init; }

    /// <summary>
    /// Gets the address of the original work. It links the title; without a title it links the author, so a credit without a title
    /// cannot also have an <see cref="AuthorHref" />.
    /// </summary>
    public LinkTarget? SourceHref { get; init; }

    /// <summary>Gets the licence's address, such as the Creative Commons deed, which links <see cref="LicenseText" />.</summary>
    public LinkTarget? LicenseHref { get; init; }

    /// <summary>Gets what was changed, such as "Cropped"; null or blank shows none.</summary>
    public string? ChangesText { get; init; }

    /// <summary>
    /// Builds the entry of a shared credit: its author, title, source address, licence code and licence address, and
    /// <paramref name="changesText" /> when the image was changed.
    /// </summary>
    /// <param name="credit">The credit.</param>
    /// <param name="changesText">The application's note for a changed image, such as "Cropped"; used only when the credit is modified.</param>
    /// <returns>The entry; set <see cref="Thumbnail" /> and <see cref="AuthorHref" /> with <c>with</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="credit" /> is <see langword="null" />.</exception>
    public static CreditItem FromCredit(ImageCredit credit, string? changesText)
    {
        ArgumentNullException.ThrowIfNull(credit);
        return new CreditItem(credit.Author, credit.License.Code)
        {
            Title = credit.Title,
            SourceHref = credit.SourceUrl is { } source ? LinkTarget.FromString(source.Value) : null,
            LicenseHref = credit.License.Url is { } license ? LinkTarget.FromString(license.Value) : null,
            ChangesText = credit.Modified ? changesText : null,
        };
    }

    /// <summary>Checks an entry where a component receives it.</summary>
    /// <param name="item">The entry, which may be missing.</param>
    /// <param name="parameterName">The caller's parameter that holds it, reported on failure.</param>
    /// <exception cref="ArgumentException">
    /// The entry is missing, has a blank author or licence text, or has no title but links both its author and its source.
    /// </exception>
    internal static void Require(CreditItem? item, string parameterName)
    {
        if (item is null)
        {
            throw new ArgumentException("A credit is missing.", parameterName);
        }

        if (string.IsNullOrWhiteSpace(item.Author) || string.IsNullOrWhiteSpace(item.LicenseText))
        {
            throw new ArgumentException("Every credit needs an author and a license text.", parameterName);
        }

        if (string.IsNullOrWhiteSpace(item.Title) && item.SourceHref is not null && item.AuthorHref is not null)
        {
            throw new ArgumentException("A credit without a title links its source through the author; give it a title to link both the source and the author.", parameterName);
        }
    }
}
