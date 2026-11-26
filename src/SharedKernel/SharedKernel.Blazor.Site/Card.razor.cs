using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// A card (<c>article</c>) with an optional image, an optional eyebrow, a heading that can be a link covering the card, text, and an
/// <see cref="ActionRow" /> at the bottom with the more look and the actions. Typed options set its style, image aspect and shadow, text
/// alignment and the more look, so a site needs no stylesheet of its own for them.
/// </summary>
public sealed partial class Card
{
    /// <summary>Gets or sets the heading text.</summary>
    [Parameter]
    [EditorRequired]
    public string Heading { get; set; } = string.Empty;

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

    /// <summary>Gets or sets the optional short credit shown with <see cref="Image" />.</summary>
    [Parameter]
    public ImageCreditLine? ImageCredit { get; set; }

    /// <summary>
    /// Gets or sets whether the image loads eagerly, for a card on the first screen; by default it loads lazily. The default is
    /// <see langword="false" />.
    /// </summary>
    [Parameter]
    public bool EagerImage { get; set; }

    /// <summary>Gets or sets the card text.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets the buttons or links at the bottom of the card, after the more look in one <see cref="ActionRow" /> that the cards of
    /// a row line up at their bottom. They stay clickable above the covering title link.
    /// </summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <summary>
    /// Gets or sets a short label above the title, such as a tour's category: with it the heading is an <c>hgroup</c>
    /// (<c>site-card__heading</c>) of the eyebrow <c>p</c> (<c>site-card__eyebrow</c>) and the heading, so it is read first and stays
    /// out of the outline. Null or blank renders none.
    /// </summary>
    [Parameter]
    public string? Eyebrow { get; set; }

    /// <summary>
    /// Gets or sets the text of the more look, such as "Learn more": a <c>span</c> with exactly the classes of a <see cref="ButtonLink" />
    /// of <see cref="MoreVariant" /> and <see cref="MoreSize" /> plus <c>site-card__more</c>, first in the action row, with
    /// <c>aria-hidden="true"</c>. It is not a control: the title link covers the card and is the one control, so a click on the look
    /// reaches that link and the look follows its hover. It needs <see cref="Href" />. Null or blank renders none.
    /// </summary>
    [Parameter]
    public string? MoreText { get; set; }

    /// <summary>Gets or sets the variant of the more look. The default is <see cref="ButtonVariant.Outline" />.</summary>
    [Parameter]
    public ButtonVariant MoreVariant { get; set; } = ButtonVariant.Outline;

    /// <summary>
    /// Gets or sets the size of the more look; give a <see cref="ButtonLink" /> in <see cref="Actions" /> the same size. The default is
    /// <see cref="ButtonSize.Regular" />.
    /// </summary>
    [Parameter]
    public ButtonSize MoreSize { get; set; }

    /// <summary>
    /// Gets or sets the look of the card. The default is <see cref="CardStyle.Boxed" />. It is not named <c>Style</c>, because
    /// Blazor matches parameter names regardless of case and would take the HTML <c>style</c> attribute, which reaches the
    /// <c>article</c>.
    /// </summary>
    [Parameter]
    public CardStyle CardStyle { get; set; }

    /// <summary>Gets or sets the aspect ratio of the image. The default is <see cref="CardImageAspect.FourByThree" />.</summary>
    [Parameter]
    public CardImageAspect ImageAspect { get; set; }

    /// <summary>
    /// Gets or sets the decoration behind the image. The default is <see cref="CardImageShadow.None" />; <see cref="CardImageShadow.Offset" />
    /// needs <see cref="CardStyle.Plain" />.
    /// </summary>
    [Parameter]
    public CardImageShadow ImageShadow { get; set; }

    /// <summary>
    /// Gets or sets the alignment of the text, the heading and the action row. The default is <see cref="SiteAlignment.Start" />;
    /// <see cref="SiteAlignment.Center" /> adds <c>site-card--text-center</c>.
    /// </summary>
    [Parameter]
    public SiteAlignment TextAlignment { get; set; }

    private bool HasMore => !string.IsNullOrWhiteSpace(MoreText);

    private string RootClass
    {
        get
        {
            var classes = "site-card";
            classes += CardStyle == CardStyle.Plain ? " site-card--plain" : string.Empty;
            classes += ImageAspect switch
            {
                CardImageAspect.SixteenByNine => " site-card--aspect-16x9",
                CardImageAspect.Square => " site-card--aspect-1x1",
                CardImageAspect.Natural => " site-card--aspect-natural",
                _ => string.Empty,
            };
            classes += ImageShadow == CardImageShadow.Offset ? " site-card--image-shadow" : string.Empty;
            classes += TextAlignment == SiteAlignment.Center ? " site-card--text-center" : string.Empty;
            return classes;
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Heading);
        HeadingMarkup.RequireLevel(HeadingLevel, nameof(HeadingLevel));
        LanguageTag.Require(TitleLanguage, nameof(TitleLanguage));
        EnumParameter.RequireDefined(CardStyle, nameof(CardStyle), "The value is not defined.");
        EnumParameter.RequireDefined(ImageAspect, nameof(ImageAspect), "The value is not defined.");
        EnumParameter.RequireDefined(ImageShadow, nameof(ImageShadow), "The value is not defined.");
        EnumParameter.RequireDefined(TextAlignment, nameof(TextAlignment), "The value is not defined.");
        EnumParameter.RequireDefined(MoreVariant, nameof(MoreVariant), "The value is not defined.");
        ButtonStyle.RequireSize(MoreSize, nameof(MoreSize));
        if (ImageShadow == CardImageShadow.Offset && CardStyle != CardStyle.Plain)
        {
            throw new ArgumentException("Only a plain card has an image shadow; a box would clip it. Set the style to Plain or leave the shadow out.", nameof(ImageShadow));
        }

        if (HasMore && Href is null)
        {
            throw new ArgumentException("The more look stands for the title link; give the card an address or leave the more text out.", nameof(MoreText));
        }
    }
}
