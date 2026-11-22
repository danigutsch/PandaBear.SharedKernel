using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>A card (<c>article</c>) with an optional image, a heading that can be a link, text and an actions slot.</summary>
public sealed partial class Card
{
    /// <summary>Gets or sets the heading text.</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the address the heading links to. Without it the heading is plain text.</summary>
    [Parameter]
    public LinkTarget? Href { get; set; }

    /// <summary>
    /// Gets or sets the BCP 47 language tag of the title when it is in another language than the page, such as <c>en</c> on a
    /// Portuguese page. It is written as <c>lang</c> on the heading alone, so the card's other text keeps the page's language (WCAG 3.1.2).
    /// <see langword="null"/>, the default, writes no <c>lang</c>.
    /// </summary>
    [Parameter]
    public string? TitleLanguage { get; set; }

    /// <summary>Gets or sets the heading level, from 1 to 6. The default is 3.</summary>
    [Parameter]
    public int HeadingLevel { get; set; } = 3;

    /// <summary>
    /// Gets or sets the image, loaded lazily. A <see cref="ResponsiveImage" /> with sources renders a <c>picture</c> with
    /// <c>srcset</c> and <c>sizes</c>; one without renders a single <c>img</c>.
    /// </summary>
    [Parameter]
    public ResponsiveImage? Image { get; set; }

    /// <summary>Gets or sets the card text.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets or sets the buttons or links at the bottom of the card.</summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        Heading.RequireLevel(HeadingLevel, nameof(HeadingLevel));
        LanguageTag.Require(TitleLanguage, nameof(TitleLanguage));
    }
}
