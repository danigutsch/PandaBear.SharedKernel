using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A page section with a heading, an optional eyebrow above it, an optional intro and a decorative divider. With
/// <see cref="HeadingId" /> the heading gets that id and the section is labelled by it (<c>aria-labelledby</c>), which makes it a named
/// region landmark; without it no id is generated, so the markup is the same during prerendering and in the interactive circuit. With
/// an <see cref="Eyebrow" /> the heading is an <c>hgroup</c> of the eyebrow <c>p</c> and the heading, in reading order; without one it
/// is the heading alone.
/// </summary>
public sealed partial class Section
{
    /// <summary>
    /// Gets or sets the short label above the heading, such as a tour's category. It is a <c>p</c>, not a heading, so it stays out of
    /// the document outline and the section's name; null or blank renders nothing.
    /// </summary>
    [Parameter]
    public string? Eyebrow { get; set; }

    /// <summary>Gets or sets the heading text.</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the id of the heading, unique on the page and without white space. When set, the section is labelled by its heading.
    /// </summary>
    [Parameter]
    public string? HeadingId { get; set; }

    /// <summary>Gets or sets the heading level, from 1 to 6. The default is 2.</summary>
    [Parameter]
    public int Level { get; set; } = 2;

    /// <summary>Gets or sets the introduction under the heading.</summary>
    [Parameter]
    public string? Intro { get; set; }

    /// <summary>
    /// Gets or sets the BCP 47 language tag of the title when it is in another language than the page. It is written as <c>lang</c> on
    /// the heading alone, so the intro and the body keep the page's language (WCAG 3.1.2). <see langword="null"/>, the default, writes
    /// no <c>lang</c>.
    /// </summary>
    [Parameter]
    public string? TitleLanguage { get; set; }

    /// <summary>Gets or sets whether the decorative divider is shown. The default is <see langword="true" />.</summary>
    [Parameter]
    public bool Divider { get; set; } = true;

    /// <summary>
    /// Gets or sets content beside the text, such as photos. The section's content is then a <see cref="Split" />: the heading, intro
    /// and body in its first column and the media in the second from the <c>48rem</c> breakpoint on, the media after them below it,
    /// so reading order matches visual order.
    /// </summary>
    [Parameter]
    public RenderFragment? Media { get; set; }

    /// <summary>Gets or sets the section body.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private bool HasEyebrow => !string.IsNullOrWhiteSpace(Eyebrow);

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        Heading.RequireLevel(Level, nameof(Level));
        LanguageTag.Require(TitleLanguage, nameof(TitleLanguage));
        if (HeadingId is not null && (HeadingId.Length == 0 || HeadingId.AsSpan().ContainsAnyWhiteSpace()))
        {
            throw new ArgumentException("A heading id must not be empty or contain white space.", nameof(HeadingId));
        }
    }
}
