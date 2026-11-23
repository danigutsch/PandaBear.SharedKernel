using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// A page for the tests of the offering display atoms: a status badge of every tone on the page, in a primary band, in an image band over
/// an all-white photo, in a cover hero without a photo and in a primary footer, and a grid of tour cards three to a row (<c>#tours</c>) whose first card
/// (<c>#tour</c>) holds an inline fact grid of three facts with icons.
/// </summary>
public sealed class OfferingSample : ComponentBase
{
    /// <summary>Gets the status tones, in declaration order.</summary>
    public static IReadOnlyList<StatusTone> Tones { get; } = Enum.GetValues<StatusTone>();

    /// <summary>Gets every place a badge is shown with every tone: <c>page</c>, <c>hero</c>, <c>primary</c>, <c>image</c> and <c>footer</c>.</summary>
    public static TheoryData<string, StatusTone> BadgeCases { get; } = Cases();

    private static readonly RenderFragment Icon = builder => builder.AddMarkupContent(0, "<svg viewBox=\"0 0 24 24\"><circle cx=\"12\" cy=\"12\" r=\"10\" /></svg>");

    private static readonly FactCard[] CardFacts =
    [
        new("Days", [new("8")], Icon),
        new("Distance", [new("320 km")], Icon),
        new("Level", [new("Moderate")], Icon),
    ];

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenElement(0, "main");
        builder.OpenComponent<Hero>(1);
        builder.AddComponentParameter(2, nameof(Hero.Layout), HeroLayout.Cover);
        builder.AddComponentParameter(3, nameof(Hero.Title), "Atacama");
        builder.AddComponentParameter(4, nameof(Hero.Actions), Badges("hero"));
        builder.CloseComponent();
        builder.OpenElement(5, "div");
        builder.AddAttribute(6, "class", "site-container");
        builder.AddContent(7, Badges("page"));
        builder.AddContent(8, Tours);
        builder.CloseElement();
        builder.OpenComponent<Band>(9);
        builder.AddComponentParameter(10, nameof(Band.Tone), BandTone.Primary);
        builder.AddComponentParameter(11, nameof(Band.ChildContent), Badges("primary"));
        builder.CloseComponent();
        builder.OpenComponent<Band>(12);
        builder.AddComponentParameter(13, nameof(Band.Tone), BandTone.Image);
        builder.AddComponentParameter(14, nameof(Band.Image), ResponsiveImage.Create(LinkTarget.FromString("/white.svg"), 1600, 900, ImageAlternative.Decorative()));
        builder.AddComponentParameter(15, nameof(Band.ChildContent), Badges("image"));
        builder.CloseComponent();
        builder.CloseElement();
        builder.OpenComponent<SiteFooter>(16);
        builder.AddComponentParameter(17, nameof(SiteFooter.Tone), FooterTone.Primary);
        builder.AddComponentParameter(18, nameof(SiteFooter.ChildContent), Badges("footer"));
        builder.CloseComponent();
    }

    /// <summary>Gets the name of a tone as its class and tokens write it.</summary>
    /// <param name="tone">The tone.</param>
    /// <returns>The lower-case name, such as <c>warning</c>.</returns>
    public static string Name(StatusTone tone) => tone switch
    {
        StatusTone.Info => "info",
        StatusTone.Success => "success",
        StatusTone.Warning => "warning",
        StatusTone.Danger => "danger",
        _ => "neutral",
    };

    private static TheoryData<string, StatusTone> Cases()
    {
        var data = new TheoryData<string, StatusTone>();
        foreach (var context in new[] { "page", "hero", "primary", "image", "footer" })
        {
            foreach (var tone in Tones)
            {
                data.Add(context, tone);
            }
        }

        return data;
    }

    private static RenderFragment Badges(string context) => builder =>
    {
        builder.OpenElement(0, "p");
        foreach (var tone in Tones)
        {
            builder.OpenComponent<StatusBadge>(1);
            builder.SetKey(tone);
            builder.AddComponentParameter(2, "id", $"{context}-{Name(tone)}");
            builder.AddComponentParameter(3, nameof(StatusBadge.Text), tone.ToString());
            builder.AddComponentParameter(4, nameof(StatusBadge.Tone), tone);
            builder.AddComponentParameter(5, nameof(StatusBadge.Label), "Status:");
            builder.CloseComponent();
        }

        builder.CloseElement();
    };

    private static void Tours(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CardGrid>(0);
        builder.AddComponentParameter(1, "id", "tours");
        builder.AddComponentParameter(2, nameof(CardGrid.MinColumnWidth), CardColumnWidth.Medium);
        builder.AddComponentParameter(3, nameof(CardGrid.ChildContent), (RenderFragment)(grid =>
        {
            for (var index = 0; index < 4; index++)
            {
                grid.OpenComponent<Card>(0);
                grid.SetKey(index);
                grid.AddComponentParameter(1, "id", index == 0 ? "tour" : $"tour-{index}");
                grid.AddComponentParameter(2, nameof(Card.Title), "Atacama");
                grid.AddComponentParameter(3, nameof(Card.Href), LinkTarget.FromString("/tours/atacama"));
                grid.AddComponentParameter(4, nameof(Card.ChildContent), (RenderFragment)(body =>
                {
                    body.OpenComponent<FactGrid>(0);
                    body.AddComponentParameter(1, nameof(FactGrid.Layout), FactGridLayout.Inline);
                    body.AddComponentParameter(2, nameof(FactGrid.Cards), CardFacts);
                    body.CloseComponent();
                }));
                grid.CloseComponent();
            }
        }));
        builder.CloseComponent();
    }
}
