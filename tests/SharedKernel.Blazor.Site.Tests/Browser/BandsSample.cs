using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// A public page for the band tests: a collapsible header over a cover hero whose photo is all white (the worst photo for white text),
/// a primary band with a quote, an image band, a muted band with a split section and a mosaic, a byline, a video link, the footer and
/// a floating link.
/// </summary>
public sealed class BandsSample : ComponentBase
{
    /// <summary>The address of the all-white photo that <see cref="LayoutPage.Serve{TComponent}" /> serves.</summary>
    public const string WhitePhoto = "/white.svg";

    /// <summary>Gets or sets a value indicating whether the hero has the white photo; without it the hero is a primary band.</summary>
    [Parameter]
    public bool HeroImage { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether a paragraph comes before the hero, so the hero does not open the page.</summary>
    [Parameter]
    public bool LeadText { get; set; }

    /// <summary>Gets or sets where the header lies.</summary>
    [Parameter]
    public HeaderPlacement Placement { get; set; } = HeaderPlacement.OverHero;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenComponent<SiteHeader>(0);
        builder.AddComponentParameter(1, nameof(SiteHeader.Label), "Main");
        builder.AddComponentParameter(2, nameof(SiteHeader.Items), LayoutSample.Navigation);
        builder.AddComponentParameter(3, nameof(SiteHeader.CurrentPath), "/gallery");
        builder.AddComponentParameter(4, nameof(SiteHeader.Brand), (RenderFragment)(brand => brand.AddMarkupContent(0, "<a id=\"brand\" href=\"/\">Brand</a>")));
        builder.AddComponentParameter(5, nameof(SiteHeader.Placement), Placement);
        builder.AddComponentParameter(6, nameof(SiteHeader.MenuLabel), "Menu");
        builder.AddComponentParameter(7, nameof(SiteHeader.Actions), (RenderFragment)(actions => actions.AddMarkupContent(0, "<a id=\"action\" href=\"/en\">EN</a>")));
        builder.CloseComponent();
        builder.OpenElement(8, "main");
        if (LeadText)
        {
            builder.AddMarkupContent(9, "<p id=\"lead\" class=\"site-container\">Before the hero.</p>");
        }

        builder.OpenComponent<Hero>(10);
        builder.AddComponentParameter(11, nameof(Hero.Layout), HeroLayout.Cover);
        builder.AddComponentParameter(12, nameof(Hero.Eyebrow), "Since 2004");
        builder.AddComponentParameter(13, nameof(Hero.Title), "Travel with feeling");
        builder.AddComponentParameter(14, nameof(Hero.Subtitle), "Small groups, one guide");
        if (HeroImage)
        {
            builder.AddComponentParameter(15, nameof(Hero.Image), Photo(1600, 900));
        }

        builder.AddComponentParameter(16, nameof(Hero.Actions), (RenderFragment)HeroActions);
        builder.CloseComponent();
        builder.AddContent(17, Bands);
        builder.CloseElement();
        builder.OpenComponent<SiteFooter>(18);
        builder.AddComponentParameter(19, nameof(SiteFooter.ChildContent), (RenderFragment)(footer => footer.AddMarkupContent(0, "<p><a id=\"footer-link\" href=\"/privacy\">Privacy</a></p><p id=\"footer-end\">Viajantes, São Paulo</p>")));
        builder.CloseComponent();
        builder.OpenComponent<FloatingLink>(20);
        builder.AddComponentParameter(21, nameof(FloatingLink.Href), LinkTarget.FromString("https://wa.me/5511999990000"));
        builder.AddComponentParameter(22, nameof(FloatingLink.Label), "Chat on WhatsApp");
        builder.AddComponentParameter(23, nameof(FloatingLink.ChildContent), (RenderFragment)(icon => icon.AddMarkupContent(0, "<svg viewBox=\"0 0 24 24\"><circle cx=\"12\" cy=\"12\" r=\"10\" /></svg>")));
        builder.CloseComponent();
    }

    private static ResponsiveImage Photo(int width, int height) =>
        ResponsiveImage.Create(LinkTarget.FromString(WhitePhoto), width, height, ImageAlternative.Decorative());

    private static void HeroActions(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ButtonLink>(0);
        builder.AddComponentParameter(1, nameof(ButtonLink.Href), LinkTarget.FromString("/trips"));
        builder.AddComponentParameter(2, nameof(ButtonLink.Variant), ButtonVariant.Outline);
        builder.AddComponentParameter(3, nameof(ButtonLink.ChildContent), (RenderFragment)(text => text.AddContent(0, "See the trips")));
        builder.CloseComponent();
    }

    private static void Bands(RenderTreeBuilder builder)
    {
        builder.OpenComponent<Band>(0);
        builder.AddComponentParameter(1, nameof(Band.Tone), BandTone.Primary);
        builder.AddComponentParameter(2, nameof(Band.ChildContent), (RenderFragment)(quote =>
        {
            quote.OpenComponent<Quote>(0);
            quote.AddComponentParameter(1, nameof(Quote.Cite), "Ana, Lisbon");
            quote.AddComponentParameter(2, nameof(Quote.ChildContent), (RenderFragment)(text => text.AddContent(0, "Unforgettable.")));
            quote.CloseComponent();
        }));
        builder.CloseComponent();
        builder.OpenComponent<Band>(3);
        builder.AddComponentParameter(4, nameof(Band.Tone), BandTone.Image);
        builder.AddComponentParameter(5, nameof(Band.Image), Photo(1600, 900));
        builder.AddComponentParameter(6, nameof(Band.ChildContent), (RenderFragment)(text => text.AddMarkupContent(0, "<p id=\"band-text\">Explore Chile with us.</p>")));
        builder.CloseComponent();
        builder.OpenComponent<Band>(7);
        builder.AddComponentParameter(8, nameof(Band.Tone), BandTone.Muted);
        builder.AddComponentParameter(9, nameof(Band.ChildContent), (RenderFragment)SplitSection);
        builder.CloseComponent();
        builder.OpenComponent<Byline>(10);
        builder.AddComponentParameter(11, nameof(Byline.Name), "Carla Giurizatto");
        builder.AddComponentParameter(12, nameof(Byline.JobTitle), "Founder");
        builder.AddComponentParameter(13, nameof(Byline.Photo), Photo(400, 600));
        builder.CloseComponent();
        builder.OpenComponent<VideoLink>(14);
        builder.AddComponentParameter(15, nameof(VideoLink.Href), LinkTarget.FromString("https://www.youtube.com/watch?v=abc123"));
        builder.AddComponentParameter(16, nameof(VideoLink.Title), "Patagonia in winter");
        builder.AddComponentParameter(17, nameof(VideoLink.SiteHint), "(opens YouTube)");
        builder.AddComponentParameter(18, nameof(VideoLink.Poster), Photo(1600, 900));
        builder.CloseComponent();
    }

    private static void SplitSection(RenderTreeBuilder builder)
    {
        builder.OpenComponent<Section>(0);
        builder.AddComponentParameter(1, nameof(Section.Title), "Past trips");
        builder.AddComponentParameter(2, nameof(Section.ChildContent), (RenderFragment)(text => text.AddMarkupContent(0, "<p>Where we have been.</p>")));
        builder.AddComponentParameter(3, nameof(Section.Media), (RenderFragment)(media =>
        {
            media.OpenComponent<ImageGrid>(0);
            media.AddComponentParameter(1, nameof(ImageGrid.Layout), ImageGridLayout.Mosaic);
            media.AddComponentParameter(2, nameof(ImageGrid.EagerCount), 4);
            media.AddComponentParameter(3, nameof(ImageGrid.Images), new GalleryImage[]
            {
                new(Photo(1600, 900)),
                new(Photo(400, 600)),
                new(Photo(800, 800)),
                new(Photo(1600, 900)),
            });
            media.CloseComponent();
        }));
        builder.CloseComponent();
    }
}
