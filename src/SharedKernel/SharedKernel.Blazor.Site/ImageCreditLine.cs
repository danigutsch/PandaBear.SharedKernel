using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site;

/// <summary>
/// The short credit of one image, shown under it by <see cref="ImageCreditCaption" />, such as "Photo: Ana Lima" and "CC BY 4.0". Every
/// text is the application's; <see cref="FromCredit" /> builds one from a shared <see cref="ImageCredit" />.
/// </summary>
/// <param name="Text">The credit, such as "Photo: Ana Lima". It must not be blank.</param>
/// <param name="LicenseText">The licence's short name, such as "CC BY 4.0", after the credit; null or blank shows none.</param>
/// <param name="LicenseHref">The licence's address, which links <paramref name="LicenseText" />; it needs that text.</param>
/// <param name="CreditsHref">The address of the full attribution, such as the site's credits page, which links <paramref name="Text" />.</param>
public sealed record ImageCreditLine(string Text, string? LicenseText = null, LinkTarget? LicenseHref = null, LinkTarget? CreditsHref = null)
{
    /// <summary>
    /// Builds the line of a shared credit: the text from the application's format, which should name the author and leave the licence
    /// out, because the licence follows it with its link: the credit's <see cref="ImageLicense.Code" /> linked to its
    /// <see cref="ImageLicense.Url" />.
    /// </summary>
    /// <param name="credit">The credit.</param>
    /// <param name="format">
    /// The application's format, such as "Foto: {0}", with the placeholders of <see cref="ImageCredit.ToShortText" />: <c>{0}</c> the
    /// author, <c>{2}</c> the title and <c>{3}</c> the source address.
    /// </param>
    /// <param name="provider">The culture.</param>
    /// <param name="creditsHref">The optional address of the full attribution, which links the text.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="credit" /> or <paramref name="provider" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="format" /> is blank.</exception>
    public static ImageCreditLine FromCredit(ImageCredit credit, string format, IFormatProvider provider, LinkTarget? creditsHref = null)
    {
        ArgumentNullException.ThrowIfNull(credit);
        return new ImageCreditLine(
            credit.ToShortText(format, provider),
            credit.License.Code,
            credit.License.Url is { } url ? LinkTarget.FromString(url.Value) : null,
            creditsHref);
    }

    /// <summary>Checks a line where a component receives it, so every component refuses the same lines with the same message.</summary>
    /// <param name="line">The line.</param>
    /// <param name="parameterName">The caller's parameter that holds it, reported on failure.</param>
    /// <exception cref="ArgumentException">The text is blank, or a licence address has no licence text to link.</exception>
    internal static void Require(ImageCreditLine line, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(line.Text))
        {
            throw new ArgumentException("An image credit needs its text.", parameterName);
        }

        if (line.LicenseHref is not null && string.IsNullOrWhiteSpace(line.LicenseText))
        {
            throw new ArgumentException("An image credit's license address needs the license text that it links.", parameterName);
        }
    }
}
