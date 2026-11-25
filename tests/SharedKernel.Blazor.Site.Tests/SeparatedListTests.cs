using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the list whose items a visual bar separates.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class SeparatedListTests : BunitContext
{
    [Fact]
    public void Items_are_list_items_of_a_list_that_keeps_its_role()
    {
        // Arrange & Act
        var cut = Render<SeparatedList>(parameters => parameters
            .Add(component => component.Class, "extra")
            .AddUnmatched("aria-label", "Contact")
            .Add(component => component.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<SeparatedListItem>(0);
                builder.AddComponentParameter(1, nameof(SeparatedListItem.ChildContent), (RenderFragment)(text => text.AddMarkupContent(0, "Telefone: <a href=\"tel:+491712198987\">+49 171 2198987</a>")));
                builder.CloseComponent();
                builder.OpenComponent<SeparatedListItem>(2);
                builder.AddComponentParameter(3, "id", "mail");
                builder.AddComponentParameter(4, nameof(SeparatedListItem.ChildContent), (RenderFragment)(text => text.AddContent(0, "E-mail")));
                builder.CloseComponent();
            })));

        // Assert
        var list = cut.Find("ul");
        list.ClassName.ShouldBe("site-separated-list extra");
        list.GetAttribute("role").ShouldBe("list");
        list.GetAttribute("aria-label").ShouldBe("Contact");
        var items = cut.FindAll("ul > li");
        items.Select(item => item.ClassName).ShouldBe(["site-separated-list__item", "site-separated-list__item"]);
        items[0].TextContent.ShouldBe("Telefone: +49 171 2198987");
        items[1].Id.ShouldBe("mail");
        cut.Markup.ShouldNotContain("|", StringComparison.Ordinal);
    }

    [Fact]
    public void An_item_outside_a_separated_list_is_refused()
    {
        // Arrange & Act
        var render = () => Render<SeparatedListItem>(parameters => parameters.AddChildContent("x"));

        // Assert
        render.ShouldThrow<InvalidOperationException>().Message.ShouldBe("A SeparatedListItem must be inside a SeparatedList.");
    }
}
