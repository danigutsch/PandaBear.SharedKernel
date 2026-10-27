using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
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
        links[0].GetAttribute("href").ShouldBe("http://localhost/#email");
        links[0].TextContent.ShouldBe("Email is required.");
        links[1].GetAttribute("href").ShouldBe("http://localhost/#name");
    }

    [Fact]
    public void Summary_takes_focus_on_every_new_submission_but_not_on_an_unchanged_rerender()
    {
        // Arrange
        var items = Items(("email", "Email is required."));
        var cut = Render<ErrorSummary>(parameters => parameters
            .Add(component => component.Title, "There is a problem")
            .Add(component => component.Items, items));

        // Act
        var first = JSInterop.VerifyFocusAsyncInvoke(1)[0];
        cut.Render();

        // Assert
        var focused = first.Arguments[0].ShouldBeOfType<ElementReference>();
        focused.Id.ShouldBe(cut.Find("section").GetAttribute("blazor:elementreference"));
        JSInterop.VerifyFocusAsyncInvoke(1);

        // Act: a resubmission with identical content arrives as a fresh list.
        cut.Render(parameters => parameters
            .Add(component => component.Title, "There is a problem")
            .Add(component => component.Items, Items(("email", "Email is required."))));

        // Assert
        JSInterop.VerifyFocusAsyncInvoke(2);
    }

    [Fact]
    public void Hostile_text_is_encoded_and_bounded_and_the_link_target_is_exactly_escaped()
    {
        // Arrange & Act
        var cut = Render<ErrorSummary>(parameters => parameters
            .Add(component => component.Title, "There is a problem")
            .Add(component => component.Items, Items(("a b\"><script>", HostileText.Markup), ("x", HostileText.VeryLong(10_000)))));

        // Assert
        cut.FindAll("script, img").ShouldBeEmpty();
        var links = cut.FindAll("li a");
        links[0].GetAttribute("href").ShouldBe("http://localhost/#a%20b%22%3E%3Cscript%3E");
        links[0].TextContent.ShouldBe(HostileText.Markup);
        links[1].TextContent.Length.ShouldBeLessThanOrEqualTo(ErrorSummary.MaxMessageLength);
    }

    [Fact]
    public void Links_replace_the_current_fragment_and_keep_the_page_address_so_a_base_href_cannot_redirect_them()
    {
        // Arrange
        Services.GetRequiredService<NavigationManager>().NavigateTo("/orders/new?draft=1#old");

        // Act
        var cut = Render<ErrorSummary>(parameters => parameters
            .Add(component => component.Title, "There is a problem")
            .Add(component => component.Items, Items(("email", "Email is required."))));

        // Assert
        var link = cut.Find("li a");
        link.GetAttribute("href").ShouldBe("http://localhost/orders/new?draft=1#email");
        link.GetAttribute("target").ShouldBe("_top");
    }

    [Fact]
    public void Missing_title_items_or_target_ids_are_rejected()
    {
        // Arrange
        var noTitle = () => this.RenderSummary(null!, Items(("a", "b")));
        var noItems = () => this.RenderSummary("Title", null!);
        var noTarget = () => this.RenderSummary("Title", [new ErrorSummaryItem(null!, "message")]);

        // Act & Assert
        noTitle.ShouldThrow<ArgumentNullException>();
        noItems.ShouldThrow<ArgumentNullException>();
        noTarget.ShouldThrow<ArgumentNullException>();
    }
}
