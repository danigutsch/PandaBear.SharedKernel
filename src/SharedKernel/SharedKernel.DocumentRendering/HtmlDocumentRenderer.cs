using System.Net;
using System.Text;
using SharedKernel.InputNormalization;

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
        builder.Append("<!DOCTYPE html><html lang=\"")
            .Append(HtmlEncode(request.Language))
            .Append("\"><head><meta charset=\"utf-8\" /><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" /><title>")
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

        builder.Append("</main>");
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
            .Append("--document-primary-color:").Append(SafeCssColor(branding.PrimaryColor, "#000000")).Append(';')
            .Append("--document-accent-color:").Append(SafeCssColor(branding.AccentColor, "#000000")).Append(';')
            .Append("--document-background-color:").Append(SafeCssColor(branding.BackgroundColor, "#ffffff")).Append(';')
            .Append("--document-text-color:").Append(SafeCssColor(branding.TextColor, "#000000")).Append(';')
            .Append("--document-heading-font:").Append(SafeCssFont(branding.HeadingFontFamily)).Append(';')
            .Append("--document-body-font:").Append(SafeCssFont(branding.BodyFontFamily)).Append(";}");
    }

    private static void AppendBranding(StringBuilder builder, DocumentBrandingSnapshot? branding)
    {
        if (branding is null)
        {
            return;
        }

        builder.Append("<header aria-label=\"Document branding\">");
        var safeLogoUri = WebAssetUriSanitizer.NormalizeRootRelativeOrHttps(branding.LogoUri?.OriginalString, 2048);
        if (safeLogoUri is not null)
        {
            builder.Append("<img src=\"").Append(HtmlEncode(safeLogoUri)).Append("\" alt=\"")
                .Append(HtmlEncode($"{branding.BrandName} logo")).Append("\" />");
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

    private static string SafeCssColor(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        if (trimmed.Length is not (4 or 7 or 9) || trimmed[0] != '#')
        {
            return fallback;
        }

        return trimmed.Skip(1).All(Uri.IsHexDigit) ? trimmed : fallback;
    }

    private static string SafeCssFont(string value)
    {
        const string fallback = "system-ui, sans-serif";
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        return trimmed.All(IsAllowedFontCharacter) ? trimmed : fallback;
    }

    private static bool IsAllowedFontCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character)
        || character is ' ' or ',' or '-' or '_';
}
