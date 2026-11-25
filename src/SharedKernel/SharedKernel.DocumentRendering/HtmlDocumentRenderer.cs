using SharedKernel.InputNormalization;

namespace SharedKernel.DocumentRendering;

/// <summary>
/// Renders structured document data as deterministic semantic HTML.
/// </summary>
public sealed class HtmlDocumentRenderer : IDocumentRenderer
{
    private const int MaxLinkTargetLength = 2048;

    /// <summary>
    /// The longest embedded raster logo URI the renderer accepts. It keeps a document bounded and stays under the
    /// roughly 65,000 characters that <see cref="Uri" /> accepts, so a data URI that the snapshot can hold fits here.
    /// </summary>
    private const int MaxEmbeddedLogoLength = 60_000;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">A document link has null text or a null target.</exception>
    /// <exception cref="ArgumentException">A document link has blank text or an unsafe target.</exception>
    /// <exception cref="InvalidOperationException">A document link lacks a privacy classification.</exception>
    public byte[] Render(DocumentRenderRequest request) => RenderCore(request, null, CancellationToken.None);

    /// <summary>Renders deterministic semantic HTML within an inclusive UTF-8 byte budget.</summary>
    /// <param name="request">The structured content and branding to render.</param>
    /// <param name="maxOutputBytes">The maximum length of the complete output in bytes.</param>
    /// <param name="ct">Cancellation token for rendering.</param>
    /// <returns>The complete HTML artifact, with the same bytes as the unbounded overload.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxOutputBytes"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">A required member of the request is null.</exception>
    /// <exception cref="ArgumentException">A document link has blank text or an unsafe target.</exception>
    /// <exception cref="InvalidOperationException">A document field or block lacks a privacy classification.</exception>
    /// <exception cref="InvalidDataException">The HTML exceeds <paramref name="maxOutputBytes"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was canceled.</exception>
    public static byte[] Render(DocumentRenderRequest request, int maxOutputBytes, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxOutputBytes);
        return RenderCore(request, maxOutputBytes, ct);
    }

    private static byte[] RenderCore(DocumentRenderRequest request, int? maxOutputBytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Sections);
        ct.ThrowIfCancellationRequested();

        var builder = new HtmlDocumentWriter(maxOutputBytes, ct);
        builder.Append("<!DOCTYPE html><html lang=\"")
            .AppendEncoded(request.Language)
            .Append("\"><head><meta charset=\"utf-8\" /><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" /><title>")
            .AppendEncoded(request.Title)
            .Append("</title><style>");

        AppendBrandingStyles(builder, request.Branding, ct);
        builder.Append("@media print{header{break-inside:avoid}section{break-inside:avoid}}body{font-family:var(--document-body-font,system-ui,sans-serif);background:var(--document-background-color,#fff);color:var(--document-text-color,#111);line-height:1.5;margin:2rem}h1,h2{font-family:var(--document-heading-font,var(--document-body-font,system-ui,sans-serif));color:var(--document-primary-color,currentColor)}header{border-bottom:.25rem solid var(--document-accent-color,currentColor);margin-bottom:1rem;padding-bottom:.5rem}footer{border-top:1px solid var(--document-accent-color,currentColor);margin-top:2rem;padding-top:.5rem}dl{display:grid;grid-template-columns:max-content 1fr;gap:.25rem 1rem}dt{font-weight:700}</style></head><body>");

        AppendBranding(builder, request.Branding);
        builder.Append("<main><h1>").AppendEncoded(request.Title).Append("</h1>");

        foreach (var section in request.Sections)
        {
            ct.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(section);
            ArgumentNullException.ThrowIfNull(section.Fields);
            ArgumentNullException.ThrowIfNull(section.Paragraphs);
            ArgumentNullException.ThrowIfNull(section.Lists);
            ArgumentNullException.ThrowIfNull(section.Blocks);

            builder.Append("<section><h2>").AppendEncoded(section.Heading).Append("</h2>");
            foreach (var paragraph in section.Paragraphs)
            {
                AppendParagraph(builder, paragraph);
            }

            foreach (var list in section.Lists)
            {
                AppendList(builder, list);
            }

            foreach (var block in section.Blocks)
            {
                switch (block)
                {
                    case DocumentParagraph paragraph:
                        AppendParagraph(builder, paragraph);
                        break;
                    case DocumentList list:
                        AppendList(builder, list);
                        break;
                    case DocumentLink link:
                        AppendLink(builder, link);
                        break;
                    default:
                        ArgumentNullException.ThrowIfNull(block);
                        break;
                }
            }

            if (section.Fields.Count > 0 || (section.Paragraphs.Count == 0 && section.Lists.Count == 0 && section.Blocks.Count == 0))
            {
                builder.Append("<dl>");
                foreach (var field in section.Fields)
                {
                    ArgumentNullException.ThrowIfNull(field);
                    if (field.PrivacyClassification == DocumentPrivacyClassification.None)
                    {
                        throw new InvalidOperationException("Document fields must be privacy classified before rendering.");
                    }

                    builder.Append("<dt>").AppendEncoded(field.Label).Append("</dt><dd>")
                        .AppendEncoded(field.Value).Append("</dd>");
                }

                builder.Append("</dl>");
            }

            builder.Append("</section>");
        }

        builder.Append("</main>");
        AppendFooter(builder, request.Branding);
        builder.Append("</body></html>");
        return builder.ToUtf8();
    }

    private static void AppendParagraph(HtmlDocumentWriter builder, DocumentParagraph paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentException.ThrowIfNullOrWhiteSpace(paragraph.Text, nameof(paragraph.Text));
        if (paragraph.PrivacyClassification == DocumentPrivacyClassification.None)
        {
            throw new InvalidOperationException("Document paragraphs must be privacy classified before rendering.");
        }

        builder.Append("<p>").AppendEncoded(paragraph.Text).Append("</p>");
    }

    private static void AppendList(HtmlDocumentWriter builder, DocumentList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(list.Items);
        if (list.PrivacyClassification == DocumentPrivacyClassification.None)
        {
            throw new InvalidOperationException("Document lists must be privacy classified before rendering.");
        }

        ArgumentOutOfRangeException.ThrowIfZero(list.Items.Count, nameof(DocumentList.Items));

        var tag = list.IsOrdered ? "ol" : "ul";
        builder.Append('<').Append(tag);
        if (!string.IsNullOrWhiteSpace(list.Label))
        {
            builder.Append(" aria-label=\"").AppendEncoded(list.Label).Append('"');
        }

        builder.Append('>');
        foreach (var item in list.Items)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(item, nameof(DocumentList.Items));
            builder.Append("<li>").AppendEncoded(item).Append("</li>");
        }

        builder.Append("</").Append(tag).Append('>');
    }

    private static void AppendLink(HtmlDocumentWriter builder, DocumentLink link)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(link.Text, nameof(DocumentLink.Text));
        ArgumentNullException.ThrowIfNull(link.Target, nameof(DocumentLink.Target));
        if (link.PrivacyClassification == DocumentPrivacyClassification.None)
        {
            throw new InvalidOperationException("Document links must be privacy classified before rendering.");
        }

        var normalizedTarget = link.Target.IsAbsoluteUri && link.Target.Scheme == Uri.UriSchemeHttps
            ? WebAssetUriSanitizer.NormalizeRootRelativeOrHttps(link.Target.OriginalString, MaxLinkTargetLength)
            : null;
        var safeTarget = normalizedTarget
            ?? throw new ArgumentException("Document link targets must be safe absolute HTTPS URIs.", nameof(link));

        builder.Append("<p><a href=\"").AppendEncoded(safeTarget).Append("\">")
            .AppendEncoded(link.Text).Append("</a></p>");
    }

    private static void AppendBrandingStyles(HtmlDocumentWriter builder, DocumentBrandingSnapshot? branding, CancellationToken ct)
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
            .Append("--document-heading-font:").Append(SafeCssFont(branding.HeadingFontFamily, ct)).Append(';')
            .Append("--document-body-font:").Append(SafeCssFont(branding.BodyFontFamily, ct)).Append(";}");
    }

    private static void AppendBranding(HtmlDocumentWriter builder, DocumentBrandingSnapshot? branding)
    {
        if (branding is null)
        {
            return;
        }

        builder.Append("<header aria-label=\"Document branding\">");
        var logoText = branding.LogoUri?.OriginalString;
        var safeLogoUri = WebAssetUriSanitizer.NormalizeRootRelativeOrHttps(logoText, 2048)
            ?? WebAssetUriSanitizer.NormalizeRasterDataImage(logoText, MaxEmbeddedLogoLength);
        if (safeLogoUri is not null)
        {
            builder.Append("<img src=\"").AppendEncoded(safeLogoUri).Append("\" alt=\"")
                .AppendEncoded(branding.BrandName).Append(" logo\" />");
        }

        builder.Append("<p>").AppendEncoded(branding.BrandName).Append("</p></header>");
    }

    private static void AppendFooter(HtmlDocumentWriter builder, DocumentBrandingSnapshot? branding)
    {
        if (branding is null || string.IsNullOrWhiteSpace(branding.FooterText))
        {
            return;
        }

        builder.Append("<footer><p>").AppendEncoded(branding.FooterText).Append("</p></footer>");
    }

    private static ReadOnlySpan<char> SafeCssColor(string value, string fallback)
    {
        var trimmed = value.AsSpan().Trim();
        if (trimmed.Length is not (4 or 7 or 9) || trimmed[0] != '#')
        {
            return fallback;
        }

        foreach (var character in trimmed[1..])
        {
            if (!Uri.IsHexDigit(character))
            {
                return fallback;
            }
        }

        return trimmed;
    }

    private static ReadOnlySpan<char> SafeCssFont(string value, CancellationToken ct)
    {
        const string fallback = "system-ui, sans-serif";
        var trimmed = value.AsSpan().Trim();
        if (trimmed.IsEmpty)
        {
            return fallback;
        }

        foreach (var character in trimmed)
        {
            ct.ThrowIfCancellationRequested();
            if (!IsAllowedFontCharacter(character))
            {
                return fallback;
            }
        }

        return trimmed;
    }

    private static bool IsAllowedFontCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character)
        || character is ' ' or ',' or '-' or '_';
}
