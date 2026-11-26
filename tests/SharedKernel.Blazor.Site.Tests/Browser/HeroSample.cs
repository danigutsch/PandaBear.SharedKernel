using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor.Site.Tests.Browser;

/// <summary>A page for the hero tests: a header whose brand is a text link, then a hero with a title, an optional subtitle and one action.</summary>
public sealed class HeroSample : ComponentBase
{
    /// <summary>Gets or sets the hero layout.</summary>
    [Parameter]
    public HeroLayout Layout { get; set; }

    /// <summary>Gets or sets the scale of the hero's title.</summary>
    [Parameter]
    public HeroTitleScale TitleScale { get; set; }

    /// <summary>Gets or sets a value indicating whether the hero has a subtitle.</summary>
    [Parameter]
    public bool Subtitle { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenComponent<SiteHeader>(0);
        builder.AddComponentParameter(1, nameof(SiteHeader.Brand), (RenderFragment)(brand => brand.AddMarkupContent(0, "<a id=\"brand\" href=\"/\">Viajantes</a>")));
        builder.CloseComponent();
        builder.OpenElement(2, "main");
        builder.OpenComponent<Hero>(3);
        builder.AddComponentParameter(4, nameof(Hero.Layout), Layout);
        builder.AddComponentParameter(5, nameof(Hero.Heading), "Travel with feeling");
        builder.AddComponentParameter(6, nameof(Hero.Subtitle), Subtitle ? "Small groups, one guide" : null);
        builder.AddComponentParameter(7, nameof(Hero.Actions), (RenderFragment)(actions =>
        {
            actions.OpenComponent<ButtonLink>(0);
            actions.AddComponentParameter(1, "id", "hero-action");
            actions.AddComponentParameter(2, nameof(ButtonLink.Href), LinkTarget.FromString("/trips"));
            actions.AddComponentParameter(3, nameof(ButtonLink.ChildContent), (RenderFragment)(text => text.AddContent(0, "See the trips")));
            actions.CloseComponent();
        }));
        builder.AddComponentParameter(8, nameof(Hero.TitleScale), TitleScale);
        builder.CloseComponent();
        builder.CloseElement();
    }
}
