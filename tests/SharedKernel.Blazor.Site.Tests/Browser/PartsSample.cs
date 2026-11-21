using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// A page for the tests of the smaller page parts, in the gutter container: a split with its media on the start side, a grid of fact
/// cards (five by default, the first with an icon), and a row of two button links and a plain link.
/// </summary>
public sealed class PartsSample : ComponentBase
{
    private static readonly FactCard[] Facts =
    [
        new("Duration", ["8 days", "7 nights"], icon => icon.AddMarkupContent(0, "<svg id=\"clock\" viewBox=\"0 0 24 24\"><circle cx=\"12\" cy=\"12\" r=\"10\" /></svg>")),
        new("Group", ["Up to 12"]),
        new("Level", ["Moderate"]),
        new("Guide", ["Portuguese", "English"]),
        new("Season", ["March to November"]),
        new("Departure", ["Santiago"]),
        new("Meals", ["Breakfast"]),
        new("Transport", ["Minibus"]),
    ];

    /// <summary>Gets or sets the number of fact cards, from one to eight.</summary>
    [Parameter]
    public int Cards { get; set; } = 5;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenElement(0, "main");
        builder.AddAttribute(1, "class", "site-container");
        builder.OpenComponent<Split>(2);
        builder.AddAttribute(3, "id", "day");
        builder.AddComponentParameter(4, nameof(Split.MediaSide), SplitMediaSide.Start);
        builder.AddComponentParameter(5, nameof(Split.ChildContent), (RenderFragment)(text => text.AddMarkupContent(0, "<h2>Day 1</h2><p>Arrival in San Pedro de Atacama and a walk through the village.</p>")));
        builder.AddComponentParameter(6, nameof(Split.Media), (RenderFragment)(media => media.AddMarkupContent(0, $"<img src=\"{BandsSample.WhitePhoto}\" width=\"1600\" height=\"900\" alt=\"\">")));
        builder.CloseComponent();
        builder.OpenComponent<FactGrid>(7);
        builder.AddComponentParameter(8, nameof(FactGrid.Cards), Facts[..Cards]);
        builder.CloseComponent();
        builder.OpenComponent<ActionRow>(9);
        builder.AddComponentParameter(10, nameof(ActionRow.ChildContent), (RenderFragment)Actions);
        builder.CloseComponent();
        builder.CloseElement();
    }

    private static void Actions(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ButtonLink>(0);
        builder.AddComponentParameter(1, nameof(ButtonLink.Href), LinkTarget.FromString("/sign-up"));
        builder.AddComponentParameter(2, nameof(ButtonLink.ChildContent), (RenderFragment)(text => text.AddContent(0, "Sign up")));
        builder.CloseComponent();
        builder.OpenComponent<ButtonLink>(3);
        builder.AddComponentParameter(4, nameof(ButtonLink.Href), LinkTarget.FromString("/gallery"));
        builder.AddComponentParameter(5, nameof(ButtonLink.Variant), ButtonVariant.Outline);
        builder.AddComponentParameter(6, nameof(ButtonLink.ChildContent), (RenderFragment)(text => text.AddContent(0, "Gallery")));
        builder.CloseComponent();
        builder.AddMarkupContent(7, "<a id=\"top-link\" href=\"#top\">Back to top</a>");
    }
}
