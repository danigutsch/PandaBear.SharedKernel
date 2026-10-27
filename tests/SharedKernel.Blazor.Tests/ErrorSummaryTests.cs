using Microsoft.AspNetCore.Components;
using static PandaBear.Blazor.Tests.ComponentFixtures;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the accessible error summary component.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class ErrorSummaryTests : BunitContext
{
    [Fact]
    public void Nothing_is_rendered_without_items()
    {
        // Arrange & Act
        var cut = Render<ErrorSummary>(parameters => parameters
            .Add(component => component.Title, "There is a problem")
            .Add(component => component.Items, []));

        // Assert
        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void Summary_is_a_labelled_focusable_region_with_links_to_the_fields()
    {
        // Arrange & Act
        var cut = Render<ErrorSummary>(parameters => parameters
            .Add(component => component.Title, "There is a problem")
            .Add(component => component.Items, Items(("email", "Email is required."), ("name", "Name is required."))));

        // Assert
        var region = cut.Find("section");
        region.GetAttribute("tabindex").ShouldBe("-1");
        var heading = cut.Find("h2");
        heading.TextContent.ShouldBe("There is a problem");
        region.GetAttribute("aria-labelledby").ShouldBe(heading.Id);
        var links = cut.FindAll("li a");
        links.Count.ShouldBe(2);
        links[0].GetAttribute("href").ShouldBe("#email");
        links[0].TextContent.ShouldBe("Email is required.");
        links[1].GetAttribute("href").ShouldBe("#name");
    }

    [Fact]
    public void Summary_takes_focus_when_new_items_appear_but_not_on_an_unchanged_rerender()
    {
        // Arrange
        var cut = Render<ErrorSummary>(parameters => parameters
            .Add(component => component.Title, "There is a problem")
            .Add(component => component.Items, Items(("email", "Email is required."))));

        // Act
        var first = JSInterop.VerifyFocusAsyncInvoke(1)[0];
        cut.Render();

        // Assert
        var focused = first.Arguments[0].ShouldBeOfType<ElementReference>();
        focused.Id.ShouldBe(cut.Find("section").GetAttribute("blazor:elementreference"));
        JSInterop.VerifyFocusAsyncInvoke(1);

        // Act
        cut.Render(parameters => parameters
            .Add(component => component.Title, "There is a problem")
            .Add(component => component.Items, Items(("name", "Name is required."))));

        // Assert
        JSInterop.VerifyFocusAsyncInvoke(2);
    }

    [Fact]
    public void Hostile_text_is_encoded_and_bounded_and_cannot_break_out_of_the_link_target()
    {
        // Arrange & Act
        var cut = Render<ErrorSummary>(parameters => parameters
            .Add(component => component.Title, "There is a problem")
            .Add(component => component.Items, Items(("a b\"><script>", HostileText.Markup), ("x", HostileText.VeryLong(10_000)))));

        // Assert
        cut.FindAll("script, img").ShouldBeEmpty();
        var links = cut.FindAll("li a");
        links[0].GetAttribute("href").ShouldStartWith("#", StringComparison.Ordinal);
        links[0].TextContent.ShouldBe(HostileText.Markup);
        links[1].TextContent.Length.ShouldBeLessThanOrEqualTo(ErrorSummary.MaxMessageLength);
    }
}
