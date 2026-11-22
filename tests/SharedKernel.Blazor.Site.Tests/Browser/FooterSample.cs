using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// A page for the footer tests: a language switcher, then a centred primary footer with a separated list of an address, a phone line and
/// an e-mail line, and a separated list of three legal links.
/// </summary>
public sealed class FooterSample : ComponentBase
{
    private static readonly LanguageLink[] Languages =
    [
        new(SiteFixtures.Culture("pt-BR"), "Português", LinkTarget.FromString("/pt"), IsCurrent: true),
        new(SiteFixtures.Culture("en"), "English", LinkTarget.FromString("/en")),
    ];

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenComponent<LanguageSwitcher>(0);
        builder.AddComponentParameter(1, nameof(LanguageSwitcher.Label), "Language");
        builder.AddComponentParameter(2, nameof(LanguageSwitcher.Languages), Languages);
        builder.CloseComponent();
        builder.OpenElement(3, "main");
        builder.AddContent(4, "Content");
        builder.CloseElement();
        builder.OpenComponent<SiteFooter>(5);
        builder.AddComponentParameter(6, nameof(SiteFooter.Tone), FooterTone.Primary);
        builder.AddComponentParameter(7, nameof(SiteFooter.TextAlignment), SiteAlignment.Center);
        builder.AddComponentParameter(8, nameof(SiteFooter.ChildContent), (RenderFragment)(content =>
        {
            content.AddContent(0, List(
                "contact",
                Item("Travessa do Fala Só, 11, 2.º Esq., 1250-109 Lisboa, Portugal"),
                Item("<span id=\"phone-label\">Telefone:</span> <a id=\"phone\" href=\"tel:+491712198987\">+491712198987</a>"),
                Item("E-mail: <a href=\"mailto:info@example.org\">info@example.org</a>")));
            content.AddContent(1, List(
                "legal",
                Item("<a href=\"/company\">Company details</a>"),
                Item("<a href=\"/terms\">Terms and conditions</a>"),
                Item("<a href=\"/payment\">Payment and cancellation policies</a>")));
        }));
        builder.AddComponentParameter(9, nameof(SiteFooter.Legal), (RenderFragment)(legal => legal.AddMarkupContent(0, "<p>© 2026 Example</p>")));
        builder.CloseComponent();
    }

    private static RenderFragment List(string id, params RenderFragment[] items) => builder =>
    {
        builder.OpenComponent<SeparatedList>(0);
        builder.AddComponentParameter(1, "id", id);
        builder.AddComponentParameter(2, nameof(SeparatedList.ChildContent), (RenderFragment)(list =>
        {
            foreach (var item in items)
            {
                list.AddContent(0, item);
            }
        }));
        builder.CloseComponent();
    };

    private static RenderFragment Item(string markup) => builder =>
    {
        builder.OpenComponent<SeparatedListItem>(0);
        builder.AddComponentParameter(1, nameof(SeparatedListItem.ChildContent), (RenderFragment)(text => text.AddMarkupContent(0, markup)));
        builder.CloseComponent();
    };
}
