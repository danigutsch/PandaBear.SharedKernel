namespace PandaBear.Blazor.Site;

/// <summary>
/// One entry of a <see cref="CreditsList" />: the attribution of an image as Creative Commons recommends it (title, author, source,
/// licence), with what was changed. Every text is the application's.
/// </summary>
/// <param name="Thumbnail">An optional small image of the work, loaded lazily; usually decorative, because the text names the work.</param>
/// <param name="Title">The work's title, linked to <paramref name="SourceHref" />; null or blank shows none.</param>
/// <param name="Author">The author as the licence asks to credit them, such as "Ana Lima". It must not be blank.</param>
/// <param name="SourceHref">The address of the original work; it links the title, or the author when there is no title.</param>
/// <param name="LicenceText">The licence, such as "CC BY-SA 4.0" or "All rights reserved". It must not be blank.</param>
/// <param name="LicenceHref">The licence's address, such as the Creative Commons deed, which links <paramref name="LicenceText" />.</param>
/// <param name="ChangesText">What was changed, such as "Cropped"; null or blank shows none.</param>
public sealed record CreditItem(
    ResponsiveImage? Thumbnail,
    string? Title,
    string Author,
    LinkTarget? SourceHref,
    string LicenceText,
    LinkTarget? LicenceHref,
    string? ChangesText);
