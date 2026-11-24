namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the link styled as a button.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ButtonLinkTests : BunitContext
{
    [Theory]
    [InlineData(ButtonVariant.Primary, "site-button--primary")]
    [InlineData(ButtonVariant.Secondary, "site-button--secondary")]
    [InlineData(ButtonVariant.Outline, "site-button--outline")]
    public void Variant_selects_the_modifier_class(ButtonVariant variant, string expected)
    {
        // Arrange & Act
        var cut = Render<ButtonLink>(parameters => parameters
            .Add(component => component.Href, LinkTarget.FromString("/tours"))
            .Add(component => component.Variant, variant)
            .AddChildContent("Go"));

        // Assert
        var link = cut.Find("a");
        BunitElementAssertions.HasClass(link.ClassList, "site-button");
        BunitElementAssertions.HasClass(link.ClassList, expected);
        link.GetAttribute("href").ShouldBe("/tours");
        link.GetAttribute("target").ShouldBeNull();
        link.GetAttribute("rel").ShouldBeNull();
    }

    [Fact]
    public void New_tab_links_get_safe_rel_and_a_visually_hidden_hint()
    {
        // Arrange & Act
        var cut = Render<ButtonLink>(parameters => parameters
            .Add(component => component.Href, LinkTarget.FromString("https://example.org"))
            .Add(component => component.NewTab, true)
            .AddChildContent("Partner"));

        // Assert
        var link = cut.Find("a");
        link.GetAttribute("target").ShouldBe("_blank");
        link.GetAttribute("rel").ShouldBe("noopener noreferrer");
        link.QuerySelector(".site-visually-hidden")!.TextContent.Trim().ShouldBe("(opens in a new tab)");
    }

    [Fact]
    public void The_new_tab_hint_can_be_translated()
    {
        // Arrange & Act
        var cut = Render<ButtonLink>(parameters => parameters
            .Add(component => component.Href, LinkTarget.FromString("https://example.org"))
            .Add(component => component.NewTab, true)
            .Add(component => component.NewTabHint, "(abre em nova aba)")
            .AddChildContent("Parceiro"));

        // Assert
        cut.Find(".site-visually-hidden").TextContent.Trim().ShouldBe("(abre em nova aba)");
    }

    [Fact]
    public void The_new_tab_hint_is_separated_from_the_link_text_by_a_space_in_the_accessible_name()
    {
        // Arrange & Act
        var cut = Render<ButtonLink>(parameters => parameters
            .Add(component => component.Href, LinkTarget.FromString("https://example.org"))
            .Add(component => component.NewTab, true)
            .Add(component => component.NewTabHint, "(abre em uma nova aba)")
            .AddChildContent("Inscreva-se"));

        // Assert
        cut.Find("a").TextContent.ShouldBe("Inscreva-se (abre em uma nova aba)");
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_link_and_cannot_replace_rel()
    {
        // Arrange & Act
        var cut = Render<ButtonLink>(parameters => parameters
            .Add(component => component.Href, LinkTarget.FromString("https://example.org"))
            .Add(component => component.NewTab, true)
            .Add(component => component.Class, "extra")
            .AddUnmatched("rel", "opener")
            .AddUnmatched("data-test", "x")
            .AddChildContent("Go"));

        // Assert
        var link = cut.Find("a");
        BunitElementAssertions.HasClass(link.ClassList, "extra");
        link.GetAttribute("data-test").ShouldBe("x");
        link.GetAttribute("rel").ShouldBe("noopener noreferrer");
    }
}
