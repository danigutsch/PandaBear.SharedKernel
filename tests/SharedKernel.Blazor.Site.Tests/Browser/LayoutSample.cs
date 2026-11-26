using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor.Site.Tests.Browser;

/// <summary>
/// A public page for the layout tests: the header on a page inside the "Trips" group (followed by "Gallery" and "Contact"), optionally
/// with a language switcher, a section of cards, and text in the gutter container without a heading.
/// </summary>
public sealed class LayoutSample : ComponentBase
{
    /// <summary>The navigation of the sample: a link, a group with two children, and two more links.</summary>
    public static readonly SiteNavItem[] Navigation =
    [
        new("Home", LinkTarget.FromString("/")),
        new("Trips", Children: [new("Atacama", LinkTarget.FromString("/trips/atacama")), new("Patagonia", LinkTarget.FromString("/trips/patagonia"))]),
        new("Gallery", LinkTarget.FromString("/gallery")),
        new("Contact", LinkTarget.FromString("/contact")),
    ];

    /// <summary>Gets or sets the number of cards in the grid.</summary>
    [Parameter]
    public int Cards { get; set; } = 1;

    /// <summary>Gets or sets a value indicating whether the header's actions hold a language switcher.</summary>
    [Parameter]
    public bool Languages { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenComponent<SiteHeader>(0);
        builder.AddComponentParameter(1, nameof(SiteHeader.Label), "Main");
        builder.AddComponentParameter(2, nameof(SiteHeader.Items), Navigation);
        builder.AddComponentParameter(3, nameof(SiteHeader.CurrentPath), "/trips/atacama");
        builder.AddComponentParameter(4, nameof(SiteHeader.Brand), (RenderFragment)(brand => brand.AddMarkupContent(0, "<a href=\"/\">Brand</a>")));
        if (Languages)
        {
            builder.AddComponentParameter(12, nameof(SiteHeader.Actions), (RenderFragment)Switcher);
        }

        builder.CloseComponent();
        builder.OpenElement(5, "main");
        builder.OpenComponent<Section>(6);
        builder.AddComponentParameter(7, nameof(Section.Heading), "Trips");
        builder.AddComponentParameter(8, nameof(Section.ChildContent), (RenderFragment)Grid);
        builder.CloseComponent();
        builder.OpenElement(9, "div");
        builder.AddAttribute(10, "class", "site-container");
        builder.AddMarkupContent(11, "<p id=\"gutter-text\">Write to us; we answer in Portuguese and English.</p>");
        builder.CloseElement();
        builder.CloseElement();
    }

    private static void Switcher(RenderTreeBuilder builder)
    {
        builder.OpenComponent<LanguageSwitcher>(0);
        builder.AddComponentParameter(1, nameof(LanguageSwitcher.Label), "Language");
        builder.AddComponentParameter(2, nameof(LanguageSwitcher.Languages), new LanguageLink[]
        {
            new(BuildingBlocks.ContentCulture.Create("pt-BR").Value, "Português", LinkTarget.FromString("/pt"), IsCurrent: true),
            new(BuildingBlocks.ContentCulture.Create("en").Value, "English", LinkTarget.FromString("/en")),
        });
        builder.CloseComponent();
    }

    private void Grid(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CardGrid>(0);
        builder.AddComponentParameter(1, nameof(CardGrid.ChildContent), (RenderFragment)(cards =>
        {
            for (var index = 0; index < Cards; index++)
            {
                cards.OpenComponent<Card>(0);
                cards.AddComponentParameter(1, nameof(Card.Heading), $"Trip {index + 1}");
                cards.AddComponentParameter(2, nameof(Card.Image), ResponsiveImage.Create(LinkTarget.FromString("/trip.jpg"), 800, 600, ImageAlternative.Decorative()));
                cards.CloseComponent();
            }
        }));
        builder.CloseComponent();
    }
}
