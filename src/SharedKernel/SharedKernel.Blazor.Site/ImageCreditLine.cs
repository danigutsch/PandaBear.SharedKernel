namespace PandaBear.Blazor.Site;

/// <summary>
/// The short credit of one image, shown under it by <see cref="ImageCreditCaption" />, such as "Photo: Ana Lima" and "CC BY 4.0". Every
/// text is the application's.
/// </summary>
/// <param name="Text">The credit, such as "Photo: Ana Lima". It must not be blank.</param>
/// <param name="LicenceText">The licence's short name, such as "CC BY 4.0", after the credit; null or blank shows none.</param>
/// <param name="LicenceHref">The licence's address, which links <paramref name="LicenceText" />; it needs that text.</param>
/// <param name="CreditsHref">The address of the full attribution, such as the site's credits page, which links <paramref name="Text" />.</param>
public sealed record ImageCreditLine(string Text, string? LicenceText = null, LinkTarget? LicenceHref = null, LinkTarget? CreditsHref = null);
