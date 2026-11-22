using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// A page for the card tests: a grid of three linked cards with the more look, in one row on a desktop. The first card's action is a
/// button link with the same text as the more look, the second has a long text and the third a title of one long word.
/// </summary>
public sealed class CardsSample : ComponentBase
{
    /// <summary>The text of the more look and of the first card's button link.</summary>
    public const string MoreText = "More";

    /// <summary>Gets or sets the size of the more look and the button links.</summary>
    [Parameter]
    public ButtonSize Size { get; set; }

    /// <summary>Gets or sets the style of the cards.</summary>
    [Parameter]
    public CardStyle Style { get; set; }

    /// <summary>Gets the parameters of the sample.</summary>
    /// <param name="size">The size of the more look and the button links.</param>
    /// <param name="style">The style of the cards.</param>
    /// <returns>The parameters by name.</returns>
    public static Dictionary<string, object?> Parameters(ButtonSize size, CardStyle style) => new()
    {
        [nameof(Size)] = size,
        [nameof(Style)] = style,
    };

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenElement(0, "main");
        builder.AddAttribute(1, "class", "site-container");
        builder.OpenComponent<CardGrid>(2);
        builder.AddComponentParameter(3, nameof(CardGrid.ChildContent), (RenderFragment)(grid =>
        {
            grid.AddContent(0, CardFragment("one", "Alentejo", "<p>Eight days.</p>", MoreText));
            grid.AddContent(1, CardFragment("two", "Apúlia", "<p>" + string.Join(' ', Enumerable.Repeat("Olive groves, white towns and two seas on a slow ride south.", 6)) + "</p>", "Sign up"));
            grid.AddContent(2, CardFragment("three", "Pedalandoocaminhodesantiagodecompostelaemgrupo", "<p>Twelve days.</p>", "Sign up"));
        }));
        builder.CloseComponent();
        builder.CloseElement();
    }

    private RenderFragment CardFragment(string id, string title, string text, string action) => builder =>
    {
        builder.OpenComponent<Card>(0);
        builder.AddComponentParameter(1, "id", id);
        builder.AddComponentParameter(2, nameof(Card.Title), title);
        builder.AddComponentParameter(3, nameof(Card.Href), LinkTarget.FromString("/tours/" + id));
        builder.AddComponentParameter(4, nameof(Card.MoreText), MoreText);
        builder.AddComponentParameter(5, nameof(Card.ButtonSize), Size);
        builder.AddComponentParameter(6, nameof(Card.Style), Style);
        builder.AddComponentParameter(7, nameof(Card.ChildContent), (RenderFragment)(body => body.AddMarkupContent(0, text)));
        builder.AddComponentParameter(8, nameof(Card.Actions), (RenderFragment)(actions =>
        {
            actions.OpenComponent<ButtonLink>(0);
            actions.AddComponentParameter(1, nameof(ButtonLink.Href), LinkTarget.FromString("/sign-up/" + id));
            actions.AddComponentParameter(2, nameof(ButtonLink.Variant), ButtonVariant.Outline);
            actions.AddComponentParameter(3, nameof(ButtonLink.Size), Size);
            actions.AddComponentParameter(4, nameof(ButtonLink.ChildContent), (RenderFragment)(label => label.AddContent(0, action)));
            actions.CloseComponent();
        }));
        builder.CloseComponent();
    };
}
