using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site.Theming;

/// <summary>
/// A small live preview of a theme draft: headings, text with a link, small print, a primary and a secondary button, a card and a
/// navigation strip, styled by a <c>style</c> element scoped to <see cref="ScopeId" />, followed by the contrast of every colour pair
/// in the shown scheme as text (ratio, minimum, pass or fail) and the errors and warnings by field path. The parent owns the
/// scheme and width toggles; the component needs no script and re-renders on every parameter change. The sample's headings are
/// text styled with the heading roles, not heading elements, so the page's outline keeps only the host's own headings; the sample
/// is <c>inert</c>, so its links stay out of the tab order. The contrast table and the issue list are outside the sample.
/// </summary>
/// <remarks>
/// The inline <c>style</c> element needs a CSP3 nonce source (<c>style-src 'nonce-…'</c>) in the Content-Security-Policy of the page
/// that shows the preview, or <c>'unsafe-inline'</c>; pass the response's nonce in <see cref="Nonce" />. The component writes no style
/// attributes, which a nonce cannot admit. The browser checks the policy whenever the style element is inserted or its text changes, and a
/// nonce match does not depend on the text, so re-renders of an interactive page keep applying as long as the element carries the nonce
/// of the document's response: on an interactive page, pass the value the static root read and handed to the interactive root, not one
/// created in the circuit.
/// </remarks>
public sealed partial class ThemePreview
{
    private static readonly System.Buffers.SearchValues<char> Base64ValueCharacters =
        System.Buffers.SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/-_");

    private string _css = string.Empty;
    private IReadOnlyList<ContrastCheck> _contrast = [];
    private List<(string Field, string Message, bool IsError)> _issues = [];

    /// <summary>Gets or sets the theme to preview, usually a draft from <see cref="SiteTheme.CreateDraft" />; the default theme when <see langword="null" />.</summary>
    [Parameter]
    public SiteTheme? Theme { get; set; }

    /// <summary>
    /// Gets or sets errors from the editor by field path, such as the result of <see cref="ThemeEditorModel.ToDraft" />; they are listed
    /// before the theme's own rule errors and warnings.
    /// </summary>
    [Parameter]
    public IReadOnlyDictionary<string, string[]>? Errors { get; set; }

    /// <summary>Gets or sets the scheme shown. The default is <see cref="ColorScheme.Light" />.</summary>
    [Parameter]
    public ColorScheme Scheme { get; set; }

    /// <summary>Gets or sets whether the sample is shown at a phone width (22rem), where fluid sizes reach their minimum.</summary>
    [Parameter]
    public bool Narrow { get; set; }

    /// <summary>
    /// Gets or sets the id of the preview element that the style is scoped to, unique on the page: a letter, then letters, digits,
    /// hyphens or underscores. The default is <c>site-theme-preview</c>.
    /// </summary>
    [Parameter]
    public string ScopeId { get; set; } = "site-theme-preview";

    /// <summary>
    /// Gets or sets the Content-Security-Policy nonce of the page's response, written to the <c>style</c> element's <c>nonce</c>
    /// attribute: a CSP3 base64 value (letters, digits, <c>+</c>, <c>/</c>, <c>-</c>, <c>_</c>, then up to two <c>=</c>). No attribute when
    /// <see langword="null" />.
    /// </summary>
    [Parameter]
    public string? Nonce { get; set; }

    /// <summary>Gets or sets the texts.</summary>
    [Parameter]
    public ThemePreviewTexts Texts { get; set; } = new();

    /// <summary>
    /// Gets or sets the names a reader sees for the colour roles, such as <c>Card background</c> for
    /// <see cref="ThemeColorRole.SurfaceAlt" />, in the contrast table's pairs and in the contrast messages (see
    /// <see cref="SiteTheme.Review" />). A role it gives <see langword="null" /> or white space for, or every role when it is
    /// <see langword="null" />, keeps its CSS name, such as <c>surface-alt</c>. Pass the same labels to
    /// <see cref="ThemeEditorModel.ToTheme" />, so a save error reads like the preview's issue and, passed back in <see cref="Errors" />,
    /// is listed once.
    /// </summary>
    [Parameter]
    public Func<ThemeColorRole, string?>? ColorLabel { get; set; }

    private string SchemeName => Scheme == ColorScheme.Dark ? Texts.DarkScheme : Texts.LightScheme;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        SiteThemeStylesheet.RequireScopeId(ScopeId);
        RequireNonce(Nonce);
        ArgumentNullException.ThrowIfNull(Texts);
        var theme = Theme ?? SiteTheme.Default;
        var review = theme.Review(ColorLabel);
        _css = SiteThemeStylesheet.RenderScoped(theme, ScopeId);
        _contrast = [.. review.Contrast.Where(check => check.Scheme == Scheme)];
        var issues = new List<(string Field, string Message, bool IsError)>();
        foreach (var (field, messages) in Errors ?? new Dictionary<string, string[]>())
        {
            issues.AddRange(messages.Select(message => (field, message, true)));
        }

        issues.AddRange(review.Errors.Select(issue => (issue.Field, issue.Message, true)));
        issues.AddRange(review.Warnings.Select(issue => (issue.Field, issue.Message, false)));
        _issues = [.. issues.Distinct()];
    }

    private string PairName(ContrastCheck check) =>
        $"{ThemeNames.Label(check.Foreground, ColorLabel)} {Texts.On} {ThemeNames.Label(check.Background, ColorLabel)}";

    private static void RequireNonce(string? nonce)
    {
        if (nonce is null)
        {
            return;
        }

        var padding = nonce.Length - nonce.TrimEnd('=').Length;
        var value = nonce.AsSpan(0, nonce.Length - padding);
        if (value.IsEmpty || padding > 2 || value.ContainsAnyExcept(Base64ValueCharacters))
        {
            throw new ArgumentException("A nonce must be a CSP base64 value: letters, digits, '+', '/', '-' or '_', then up to two '='.", nameof(nonce));
        }
    }
}
