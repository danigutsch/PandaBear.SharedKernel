using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A labelled navigation with one link per language. Each link carries <c>hreflang</c> and <c>lang</c> so its label is read in
/// its own language, and the current language gets <c>aria-current="true"</c>. No script is involved.
/// </summary>
public sealed partial class LanguageSwitcher
{
    /// <summary>Gets or sets the accessible name of the navigation, for example "Language". Caller-supplied so it can be localized.</summary>
    [Parameter]
    [EditorRequired]
    public string Label
    {
        get; set;
    } = string.Empty;

    /// <summary>Gets or sets the languages.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<LanguageLink> Languages
    {
        get; set;
    } = [];

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
        ArgumentNullException.ThrowIfNull(Languages);
        foreach (var language in Languages)
        {
            ArgumentNullException.ThrowIfNull(language, nameof(Languages));
            ArgumentNullException.ThrowIfNull(language.Culture, nameof(Languages));
            ArgumentException.ThrowIfNullOrWhiteSpace(language.Label, nameof(Languages));
            ArgumentNullException.ThrowIfNull(language.Href, nameof(Languages));
        }
    }
}
