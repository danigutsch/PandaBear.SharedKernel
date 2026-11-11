using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site.Theming;

/// <summary>
/// A small live preview of a theme draft: headings, text with a link, small print, a primary and a secondary button, a card and a
/// navigation strip, styled by a <c>style</c> element scoped to <see cref="ScopeId" />, followed by the contrast of every colour pair
/// in the shown scheme as text (ratio, minimum, pass or fail) and the errors and warnings by field path. The parent owns the
/// scheme and width toggles; the component needs no script and re-renders on every parameter change. The sample is
/// <c>inert</c>, so its headings and links do not join the page's outline or tab order.
/// </summary>
/// <remarks>
/// The inline <c>style</c> element needs <c>style-src 'unsafe-inline'</c> (or a nonce) in the Content-Security-Policy of the page
/// that shows the preview, which should be an administration host only. The public site keeps a strict policy and loads the
/// generated stylesheet from a URL instead.
/// </remarks>
public sealed partial class ThemePreview
{
    private string _css = string.Empty;
    private IReadOnlyList<ContrastCheck> _contrast = [];
    private List<(string Field, string Message, bool IsError)> _issues = [];

    /// <summary>Gets or sets the theme to preview, usually a draft from <see cref="SiteTheme.CreateDraft" />; the default theme when <see langword="null" />.</summary>
    [Parameter]
    public SiteTheme? Theme
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets errors from the editor by field path, such as the result of <see cref="ThemeEditorModel.ToDraft" />; they are listed
    /// before the theme's own rule errors and warnings.
    /// </summary>
    [Parameter]
    public IReadOnlyDictionary<string, string[]>? Errors
    {
        get; set;
    }

    /// <summary>Gets or sets the scheme shown. The default is <see cref="ColorScheme.Light" />.</summary>
    [Parameter]
    public ColorScheme Scheme
    {
        get; set;
    }

    /// <summary>Gets or sets whether the sample is shown at a phone width (22rem), where fluid sizes reach their minimum.</summary>
    [Parameter]
    public bool Narrow
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets the id of the preview element that the style is scoped to, unique on the page: a letter, then letters, digits,
    /// hyphens or underscores. The default is <c>site-theme-preview</c>.
    /// </summary>
    [Parameter]
    public string ScopeId
    {
        get; set;
    } = "site-theme-preview";

    /// <summary>Gets or sets the texts.</summary>
    [Parameter]
    public ThemePreviewTexts Texts
    {
        get; set;
    } = new();

    private string SchemeName => Scheme == ColorScheme.Dark ? Texts.DarkScheme : Texts.LightScheme;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        SiteThemeStylesheet.RequireScopeId(ScopeId);
        ArgumentNullException.ThrowIfNull(Texts);
        var theme = Theme ?? SiteTheme.Default;
        var review = theme.Review();
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

    private string PairName(ContrastCheck check) => $"{ThemeNames.Css(check.Foreground)} {Texts.On} {ThemeNames.Css(check.Background)}";

    private string Swatch(ContrastCheck check)
    {
        var colors = (Theme ?? SiteTheme.Default).Colors;
        return $"color: {colors[check.Foreground].For(Scheme).Value}; background: {colors[check.Background].For(Scheme).Value}";
    }
}
