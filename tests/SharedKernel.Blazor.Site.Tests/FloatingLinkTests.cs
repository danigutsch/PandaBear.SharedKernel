using static SharedKernel.Blazor.Site.Tests.SiteFixtures;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the floating corner link.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class FloatingLinkTests : BunitContext
{
    [Fact]
    public void A_floating_link_is_named_by_its_label_and_hides_its_icon()
    {
        // Arrange & Act
        var cut = Render<FloatingLink>(parameters => parameters
            .Add(component => component.Href, Link("https://wa.me/5511999990000"))
            .Add(component => component.Label, "Chat on WhatsApp")
            .AddChildContent("<svg id=\"icon\" viewBox=\"0 0 24 24\"></svg>"));

        // Assert
        var link = cut.Find("a.site-floating-link");
        link.GetAttribute("href").ShouldBe("https://wa.me/5511999990000");
        link.Children.Select(child => child.ClassName).ShouldBe(["site-floating-link__icon", "site-visually-hidden"]);
        link.QuerySelector(".site-floating-link__icon")!.GetAttribute("aria-hidden").ShouldBe("true");
        link.QuerySelector(".site-floating-link__icon #icon").ShouldNotBeNull();
        link.QuerySelector(".site-visually-hidden")!.TextContent.ShouldBe("Chat on WhatsApp");
        link.HasAttribute("aria-label").ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_floating_link_without_a_label_is_rejected(string label)
    {
        // Arrange & Act
        var render = () => Render<FloatingLink>(parameters => parameters
            .Add(component => component.Href, Link("/contact"))
            .Add(component => component.Label, label));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(FloatingLink.Label));
    }

    [Fact]
    public void A_floating_link_without_an_address_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<FloatingLink>(parameters => parameters.Add(component => component.Label, "Chat"));

        // Assert
        render.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(FloatingLink.Href));
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_link()
    {
        // Arrange & Act
        var cut = Render<FloatingLink>(parameters => parameters
            .Add(component => component.Href, Link("/contact"))
            .Add(component => component.Label, "Contact")
            .Add(component => component.Class, "whatsapp")
            .AddUnmatched("data-test", "x"));

        // Assert
        var link = cut.Find("a");
        BunitElementAssertions.HasClass(link.ClassList, "site-floating-link");
        BunitElementAssertions.HasClass(link.ClassList, "whatsapp");
        link.GetAttribute("data-test").ShouldBe("x");
    }
}
