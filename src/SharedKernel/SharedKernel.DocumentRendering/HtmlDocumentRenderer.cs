using System.Net;
using System.Text;

namespace SharedKernel.DocumentRendering;

/// <summary>
/// Renders structured document data as deterministic semantic HTML.
/// </summary>
public sealed class HtmlDocumentRenderer : IDocumentRenderer
{
    /// <inheritdoc />
    public byte[] Render(DocumentRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Sections);

        var builder = new StringBuilder();
        builder.Append("<!doctype html><html lang=\"")
            .Append(HtmlEncode(request.Language))
            .Append("\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>")
            .Append(HtmlEncode(request.Title))
            .Append("</title><style>");

        AppendBrandingStyles(builder, request.Branding);
        builder.Append("@media print{header{break-inside:avoid}section{break-inside:avoid}}body{font-family:var(--document-body-font,system-ui,sans-serif);background:var(--document-background-color,#fff);color:var(--document-text-color,#111);line-height:1.5;margin:2rem}h1,h2{font-family:var(--document-heading-font,var(--document-body-font,system-ui,sans-serif));color:var(--document-primary-color,currentColor)}header{border-bottom:.25rem solid var(--document-accent-color,currentColor);margin-bottom:1rem;padding-bottom:.5rem}footer{border-top:1px solid var(--document-accent-color,currentColor);margin-top:2rem;padding-top:.5rem}dl{display:grid;grid-template-columns:max-content 1fr;gap:.25rem 1rem}dt{font-weight:700}</style></head><body>");

        AppendBranding(builder, request.Branding);
        builder.Append("<main><h1>").Append(HtmlEncode(request.Title)).Append("</h1>");

        foreach (var section in request.Sections)
        {
            ArgumentNullException.ThrowIfNull(section);
            ArgumentNullException.ThrowIfNull(section.Fields);

            builder.Append("<section><h2>").Append(HtmlEncode(section.Heading)).Append("</h2><dl>");
            foreach (var field in section.Fields)
            {
                ArgumentNullException.ThrowIfNull(field);
                if (field.PrivacyClassification == DocumentPrivacyClassification.None)
                {
                    throw new InvalidOperationException("Document fields must be privacy classified before rendering.");
                }

                builder.Append("<dt>").Append(HtmlEncode(field.Label)).Append("</dt><dd>")
                    .Append(HtmlEncode(field.Value)).Append("</dd>");
            }

            builder.Append("</dl></section>");
        }

        AppendFooter(builder, request.Branding);
        builder.Append("</body></html>");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendBrandingStyles(StringBuilder builder, DocumentBrandingSnapshot? branding)
    {
        if (branding is null)
        {
            return;
        }

        builder.Append(":root{")
            .Append("--document-primary-color:").Append(CssEncode(branding.PrimaryColor)).Append(';')
            .Append("--document-accent-color:").Append(CssEncode(branding.AccentColor)).Append(';')
            .Append("--document-background-color:").Append(CssEncode(branding.BackgroundColor)).Append(';')
            .Append("--document-text-color:").Append(CssEncode(branding.TextColor)).Append(';')
            .Append("--document-heading-font:").Append(CssEncode(branding.HeadingFontFamily)).Append(';')
            .Append("--document-body-font:").Append(CssEncode(branding.BodyFontFamily)).Append(";}");
    }

    private static void AppendBranding(StringBuilder builder, DocumentBrandingSnapshot? branding)
    {
        if (branding is null)
        {
            return;
        }

        builder.Append("<header aria-label=\"Document branding\">");
        var logoUri = branding.LogoUri;
        if (logoUri is not null && IsAllowedLogoUri(logoUri))
        {
            builder.Append("<img src=\"").Append(HtmlEncode(logoUri.OriginalString)).Append("\" alt=\"")
                .Append(HtmlEncode($"{branding.BrandName} logo")).Append("\">");
        }

        builder.Append("<p>").Append(HtmlEncode(branding.BrandName)).Append("</p></header>");
    }

    private static void AppendFooter(StringBuilder builder, DocumentBrandingSnapshot? branding)
    {
        if (branding is null || string.IsNullOrWhiteSpace(branding.FooterText))
        {
            return;
        }

        builder.Append("<footer><p>").Append(HtmlEncode(branding.FooterText)).Append("</p></footer>");
    }

    private static string HtmlEncode(string value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string CssEncode(string value) => HtmlEncode(value).Replace(";", string.Empty, StringComparison.Ordinal);

    private static bool IsAllowedLogoUri(Uri? logoUri)
    {
        if (logoUri is null)
        {
            return false;
        }

        if (logoUri.IsAbsoluteUri)
        {
            return logoUri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(logoUri.UserInfo);
        }

        var value = logoUri.OriginalString;
        return value.StartsWith('/')
            && !value.StartsWith("//", StringComparison.Ordinal)
            && !value.Contains('\\', StringComparison.Ordinal)
            && !value.Any(static character => char.IsWhiteSpace(character) || char.IsControl(character));
    }
}
