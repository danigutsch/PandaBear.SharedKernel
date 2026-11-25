namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the plain list of lines.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class TextListTests : BunitContext
{
    [Fact]
    public void A_text_list_is_an_unordered_list_of_its_lines_by_default()
    {
        // Arrange & Act
        var cut = Render<TextList>(parameters => parameters.Add(component => component.Items, ["Guide", "Breakfast", "<b>Transfers</b>"]));

        // Assert
        var list = cut.Find("ul");
        list.ClassName.ShouldBe("site-text-list");
        list.Children.Select(child => child.TagName).ShouldBe(["LI", "LI", "LI"]);
        list.Children.Select(child => child.TextContent).ShouldBe(["Guide", "Breakfast", "<b>Transfers</b>"]);
        cut.FindAll("b").ShouldBeEmpty();
    }

    [Fact]
    public void An_ordered_text_list_is_an_ordered_list()
    {
        // Arrange & Act
        var cut = Render<TextList>(parameters => parameters
            .Add(component => component.Items, ["Book", "Pay the deposit"])
            .Add(component => component.Ordered, true));

        // Assert
        cut.FindAll("ul").ShouldBeEmpty();
        var list = cut.Find("ol");
        list.ClassName.ShouldBe("site-text-list");
        list.Children.Select(child => child.TextContent).ShouldBe(["Book", "Pay the deposit"]);
    }

    [Fact]
    public void An_empty_text_list_renders_nothing()
    {
        // Arrange & Act
        var cut = Render<TextList>(parameters => parameters.Add(component => component.Items, []));

        // Assert
        cut.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void Class_and_attributes_reach_the_list()
    {
        // Arrange & Act
        var cut = Render<TextList>(parameters => parameters
            .Add(component => component.Items, ["Guide"])
            .Add(component => component.Class, "included")
            .AddUnmatched("aria-labelledby", "included-heading"));

        // Assert
        var list = cut.Find("ul");
        list.ClassName.ShouldBe("site-text-list included");
        list.GetAttribute("aria-labelledby").ShouldBe("included-heading");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_line_is_refused(string line)
    {
        // Arrange & Act
        var render = () => Render<TextList>(parameters => parameters.Add(component => component.Items, ["Guide", line]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(TextList.Items));
    }

    [Fact]
    public void A_missing_list_is_refused()
    {
        // Arrange & Act
        var render = () => Render<TextList>(parameters => parameters.Add(component => component.Items, null!));

        // Assert
        render.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(TextList.Items));
    }
}
