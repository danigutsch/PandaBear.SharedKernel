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
            .Append("</title><style>@media print{header{break-inside:avoid}section{break-inside:avoid}}body{font-family:system-ui,sans-serif;line-height:1.5;margin:2rem}dl{display:grid;grid-template-columns:max-content 1fr;gap:.25rem 1rem}dt{font-weight:700}</style></head><body>");

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
                builder.Append("<dt>").Append(HtmlEncode(field.Label)).Append("</dt><dd>")
                    .Append(HtmlEncode(field.Value)).Append("</dd>");
            }

            builder.Append("</dl></section>");
        }

        builder.Append("</main></body></html>");
        return Encoding.UTF8.GetBytes(builder.ToString());
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

    private static string HtmlEncode(string value) => WebUtility.HtmlEncode(value ?? string.Empty);

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
        return value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal);
    }
}
