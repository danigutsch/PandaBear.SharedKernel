namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the row of actions.</summary>
public sealed class ActionRowTests : BunitContext
{
    [Fact]
    public void An_action_row_is_a_plain_division_around_its_actions_in_order()
    {
        // Arrange & Act
        var cut = Render<ActionRow>(parameters => parameters
            .AddChildContent("<a id=\"sign-up\" class=\"site-button site-button--primary\" href=\"/sign-up\">Sign up</a><a id=\"top\" href=\"#top\">Back to top</a>"));

        // Assert
        var root = cut.Find(".site-action-row");
        root.TagName.ShouldBe("DIV");
        root.ClassName.ShouldBe("site-action-row");
        root.HasAttribute("role").ShouldBeFalse();
        root.Children.Select(child => child.Id).ShouldBe(["sign-up", "top"]);
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_row()
    {
        // Arrange & Act
        var cut = Render<ActionRow>(parameters => parameters
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x")
            .AddChildContent("<a href=\"/\">Home</a>"));

        // Assert
        var root = cut.Find("div");
        root.ClassName.ShouldBe("site-action-row extra");
        root.GetAttribute("data-test").ShouldBe("x");
    }
}
