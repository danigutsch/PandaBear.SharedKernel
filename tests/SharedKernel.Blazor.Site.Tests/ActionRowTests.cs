namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the row of actions.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
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

    [Theory]
    [InlineData(ActionRowJustification.Start, "site-action-row")]
    [InlineData(ActionRowJustification.Center, "site-action-row site-action-row--center")]
    [InlineData(ActionRowJustification.End, "site-action-row site-action-row--end")]
    [InlineData(ActionRowJustification.Between, "site-action-row site-action-row--between")]
    public void The_justification_sets_a_class_and_keeps_the_actions_in_order(ActionRowJustification justification, string classes)
    {
        // Arrange & Act
        var cut = Render<ActionRow>(parameters => parameters
            .Add(component => component.Justification, justification)
            .Add(component => component.Class, "extra")
            .AddChildContent("<a id=\"one\" href=\"/one\">One</a><a id=\"two\" href=\"/two\">Two</a>"));

        // Assert
        var root = cut.Find(".site-action-row");
        root.ClassName.ShouldBe(classes + " extra");
        root.Children.Select(child => child.Id).ShouldBe(["one", "two"]);
    }

    [Fact]
    public void An_undefined_justification_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<ActionRow>(parameters => parameters
            .Add(component => component.Justification, (ActionRowJustification)9)
            .AddChildContent("<a href=\"/\">Home</a>"));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(ActionRow.Justification));
    }
}
