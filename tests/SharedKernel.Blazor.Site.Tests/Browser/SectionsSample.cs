using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// A page for the section tests: a section with a long paragraph, a centred section with a paragraph, a list, a paragraph with a
/// consumer class, a byline, a video link and two action rows, a centred band with a tagline, a section with two nested sections, a
/// centred band around a start section and a section that inherits, and a start band around a centred section.
/// </summary>
public sealed class SectionsSample : ComponentBase
{
    /// <summary>A paragraph of about 400 characters, far longer than a comfortable line.</summary>
    public const string LongText =
        "We ride small roads between vineyards and villages, stop where the coffee is good, and carry the luggage for you. Every day has a "
        + "short and a long route, a guide in front and a van behind, so nobody is left alone on a climb. In the evening we stay in family "
        + "hotels and eat what the region cooks, and the next morning the bikes are checked, the tyres are pumped and the map is ready again.";

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenElement(0, "main");
        builder.OpenComponent<Section>(1);
        builder.AddAttribute(2, "id", "plain");
        builder.AddComponentParameter(3, nameof(Section.Title), "About the tour");
        builder.AddComponentParameter(4, nameof(Section.Intro), LongText);
        builder.AddComponentParameter(5, nameof(Section.ChildContent), (RenderFragment)(body => body.AddMarkupContent(0, $"<p id=\"plain-text\">{LongText}</p>")));
        builder.CloseComponent();
        builder.OpenComponent<Section>(6);
        builder.AddAttribute(7, "id", "centred");
        builder.AddComponentParameter(8, nameof(Section.Title), "Upcoming tours");
        builder.AddComponentParameter(9, nameof(Section.TextAlignment), SiteAlignment.Center);
        builder.AddComponentParameter(10, nameof(Section.ChildContent), (RenderFragment)CentredBody);
        builder.CloseComponent();
        builder.OpenComponent<Band>(11);
        builder.AddAttribute(12, "id", "tagline");
        builder.AddComponentParameter(13, nameof(Band.Tone), BandTone.Muted);
        builder.AddComponentParameter(14, nameof(Band.TextAlignment), SiteAlignment.Center);
        builder.AddComponentParameter(15, nameof(Band.ChildContent), (RenderFragment)(band => band.AddMarkupContent(0, $"<p id=\"tagline-text\">{LongText}</p>")));
        builder.CloseComponent();
        builder.OpenComponent<Section>(16);
        builder.AddAttribute(17, "id", "itinerary");
        builder.AddComponentParameter(18, nameof(Section.Title), "Itinerary");
        builder.AddComponentParameter(19, nameof(Section.ChildContent), (RenderFragment)Days);
        builder.CloseComponent();
        builder.OpenComponent<Band>(20);
        builder.AddAttribute(21, "id", "centred-band");
        builder.AddComponentParameter(22, nameof(Band.TextAlignment), SiteAlignment.Center);
        builder.AddComponentParameter(23, nameof(Band.ChildContent), (RenderFragment)(band =>
        {
            NestedSection(band, "nest-start", SiteAlignment.Start);
            NestedSection(band, "nest-inherit", null);
        }));
        builder.CloseComponent();
        builder.OpenComponent<Band>(24);
        builder.AddAttribute(25, "id", "start-band");
        builder.AddComponentParameter(26, nameof(Band.TextAlignment), SiteAlignment.Start);
        builder.AddComponentParameter(27, nameof(Band.ChildContent), (RenderFragment)(band => NestedSection(band, "nest-center", SiteAlignment.Center)));
        builder.CloseComponent();
        builder.CloseElement();
    }

    private static void NestedSection(RenderTreeBuilder builder, string id, SiteAlignment? alignment)
    {
        builder.OpenComponent<Section>(0);
        builder.AddAttribute(1, "id", id);
        builder.AddComponentParameter(2, nameof(Section.Title), id);
        builder.AddComponentParameter(3, nameof(Section.TextAlignment), alignment);
        builder.AddComponentParameter(4, nameof(Section.ChildContent), (RenderFragment)(body =>
        {
            body.AddMarkupContent(0, $"<p id=\"{id}-text\">Short text.</p>");
            body.OpenComponent<ImageGrid>(1);
            body.AddComponentParameter(2, nameof(ImageGrid.Images), new[] { new GalleryImage(ResponsiveImage.Create(LinkTarget.FromString(GallerySample.TallPhoto), 900, 1600, ImageAlternative.Decorative())) });
            body.CloseComponent();
            body.OpenComponent<ActionRow>(3);
            body.AddAttribute(4, "id", $"{id}-actions");
            body.AddComponentParameter(5, nameof(ActionRow.ChildContent), (RenderFragment)(row => row.AddMarkupContent(0, "<a href=\"/one\">One</a><a href=\"/two\">Two</a>")));
            body.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private static void CentredBody(RenderTreeBuilder builder)
    {
        builder.AddMarkupContent(0, $"<p id=\"centred-text\">{LongText}</p><ul id=\"centred-list\"><li>Bikes and helmets</li><li>Luggage transfer between the hotels</li></ul><p id=\"custom-text\" class=\"custom\">{LongText}</p>");
        builder.OpenComponent<Byline>(1);
        builder.AddComponentParameter(2, nameof(Byline.Name), "Carla Giurizatto");
        builder.AddComponentParameter(3, nameof(Byline.JobTitle), "Founder");
        builder.CloseComponent();
        builder.OpenComponent<VideoLink>(4);
        builder.AddComponentParameter(5, nameof(VideoLink.Href), LinkTarget.FromString("https://www.youtube.com/watch?v=abc123"));
        builder.AddComponentParameter(6, nameof(VideoLink.Title), "Patagonia in winter");
        builder.AddComponentParameter(7, nameof(VideoLink.SiteHint), "(opens YouTube)");
        builder.AddComponentParameter(8, nameof(VideoLink.Poster), ResponsiveImage.Create(LinkTarget.FromString(BandsSample.WhitePhoto), 480, 360, ImageAlternative.Decorative()));
        builder.CloseComponent();
        builder.OpenComponent<ActionRow>(9);
        builder.AddAttribute(10, "id", "centred-actions");
        builder.AddComponentParameter(11, nameof(ActionRow.ChildContent), (RenderFragment)(row => row.AddMarkupContent(0, "<a href=\"/one\">One</a><a href=\"/two\">Two</a>")));
        builder.CloseComponent();
        builder.OpenComponent<ActionRow>(12);
        builder.AddAttribute(13, "id", "centred-end");
        builder.AddComponentParameter(14, nameof(ActionRow.Justification), ActionRowJustification.End);
        builder.AddComponentParameter(15, nameof(ActionRow.ChildContent), (RenderFragment)(row => row.AddMarkupContent(0, "<a href=\"/one\">One</a><a href=\"/two\">Two</a>")));
        builder.CloseComponent();
    }

    private static void Days(RenderTreeBuilder builder)
    {
        for (var day = 1; day <= 2; day++)
        {
            builder.OpenComponent<Section>(0);
            builder.AddAttribute(1, "id", $"day-{day}");
            builder.AddComponentParameter(2, nameof(Section.Title), $"Day {day}");
            builder.AddComponentParameter(3, nameof(Section.Level), 3);
            builder.AddComponentParameter(4, nameof(Section.Divider), false);
            builder.AddComponentParameter(5, nameof(Section.ChildContent), (RenderFragment)(body => body.AddMarkupContent(0, "<p>Arrival and a short ride.</p>")));
            builder.CloseComponent();
        }
    }
}
