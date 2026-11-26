using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace SharedKernel.Blazor.Site.Tests.Browser;

/// <summary>
/// A page for the card tests: a grid of three linked cards with the more look, in one row on a desktop. Each card's action is a button
/// link with the same text as the more look, so the action rows wrap alike in any font; the second card has a long text and the third a
/// title of one long word. With <see cref="LongAction" /> the second card's action is long enough to wrap its row in any font.
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

    /// <summary>Gets or sets a value indicating whether the second card's action has a label too long to share a row with the more look.</summary>
    [Parameter]
    public bool LongAction { get; set; }

    /// <summary>Gets the parameters of the sample.</summary>
    /// <param name="size">The size of the more look and the button links.</param>
    /// <param name="style">The style of the cards.</param>
    /// <returns>The parameters by name.</returns>
    public static Dictionary<string, object?> Parameters(ButtonSize size, CardStyle style) => new()
    {
        [nameof(Size)] = size,
        [nameof(Style)] = style,
    };

    /// <summary>Gets the parameters of the sample with the second card's long action.</summary>
    /// <param name="style">The style of the cards.</param>
    /// <returns>The parameters by name.</returns>
    public static Dictionary<string, object?> LongActionParameters(CardStyle style) => new()
    {
        [nameof(Size)] = ButtonSize.Regular,
        [nameof(Style)] = style,
        [nameof(LongAction)] = true,
    };

    /// <summary>
    /// Gets the exact bottom edges of every element that matches a selector, from <c>getBoundingClientRect</c>, without the rounding of
    /// <see cref="ElementBox" />, which rounds the top and the height apart and can make equal edges differ by a tenth.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="selector">The CSS selector.</param>
    /// <returns>The bottom edges in CSS pixels, in document order.</returns>
    internal static Task<double[]> Bottoms(LayoutPage page, string selector)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page.Page.EvaluateAsync<double[]>("(selector) => [...document.querySelectorAll(selector)].map(element => element.getBoundingClientRect().bottom)", selector);
    }

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
            grid.AddContent(1, CardFragment("two", "Apúlia", "<p>" + string.Join(' ', Enumerable.Repeat("Olive groves, white towns and two seas on a slow ride south.", 6)) + "</p>", LongAction ? "Sign up for the next departure of this tour" : MoreText));
            grid.AddContent(2, CardFragment("three", "Pedalandoocaminhodesantiagodecompostelaemgrupo", "<p>Twelve days.</p>", MoreText));
        }));
        builder.CloseComponent();
        builder.CloseElement();
    }

    private RenderFragment CardFragment(string id, string title, string text, string action) => builder =>
    {
        builder.OpenComponent<Card>(0);
        builder.AddComponentParameter(1, "id", id);
        builder.AddComponentParameter(2, nameof(Card.Heading), title);
        builder.AddComponentParameter(3, nameof(Card.Href), LinkTarget.FromString("/tours/" + id));
        builder.AddComponentParameter(4, nameof(Card.MoreText), MoreText);
        builder.AddComponentParameter(5, nameof(Card.MoreSize), Size);
        builder.AddComponentParameter(6, nameof(Card.CardStyle), Style);
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
