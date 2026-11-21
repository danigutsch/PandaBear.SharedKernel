using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the grid of fact cards.</summary>
public sealed class FactGridTests : BunitContext
{
    private static readonly RenderFragment ClockIcon = builder => builder.AddMarkupContent(0, "<svg id=\"clock\" viewBox=\"0 0 24 24\"><circle cx=\"12\" cy=\"12\" r=\"10\" /></svg>");

    [Fact]
    public void A_fact_grid_is_a_description_list_of_cards_with_a_term_and_a_definition_per_value()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards,
        [
            new FactCard("Duration", ["8 days", "7 nights"]),
            new FactCard("Group", ["Up to 12"]),
        ]));

        // Assert
        var list = cut.Find("dl");
        list.ClassName.ShouldBe("site-fact-grid");
        list.Children.Select(child => child.TagName).ShouldBe(["DIV", "DIV"]);
        list.Children.Select(child => child.ClassName).ShouldBe(["site-fact-grid__item", "site-fact-grid__item"]);
        var first = list.Children[0];
        first.Children.Select(child => child.TagName).ShouldBe(["DT", "DD", "DD"]);
        first.Children.Select(child => child.ClassName).ShouldBe(["site-fact-grid__label", "site-fact-grid__value", "site-fact-grid__value"]);
        first.Children.Select(child => child.TextContent).ShouldBe(["Duration", "8 days", "7 nights"]);
        list.Children[1].Children.Select(child => child.TextContent).ShouldBe(["Group", "Up to 12"]);
    }

    [Fact]
    public void An_icon_is_hidden_from_assistive_technology_inside_the_term_before_the_label()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [new FactCard("Duration", ["8 days"], ClockIcon)]));

        // Assert
        var term = cut.Find(".site-fact-grid__item > dt");
        var icon = term.FirstElementChild.ShouldNotBeNull();
        icon.TagName.ShouldBe("SPAN");
        icon.ClassName.ShouldBe("site-fact-grid__icon");
        icon.GetAttribute("aria-hidden").ShouldBe("true");
        icon.QuerySelector("#clock").ShouldNotBeNull();
        term.TextContent.ShouldBe("Duration");
        cut.Find(".site-fact-grid__item").Children.Select(child => child.TagName).ShouldBe(["DT", "DD"]);
    }

    [Fact]
    public void A_card_without_an_icon_renders_no_icon_element()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [new FactCard("Group", ["Up to 12"])]));

        // Assert
        cut.FindAll(".site-fact-grid__icon").ShouldBeEmpty();
        cut.FindAll("[aria-hidden]").ShouldBeEmpty();
    }

    [Fact]
    public void A_grid_without_cards_is_an_empty_list()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards, []));

        // Assert
        cut.Find("dl.site-fact-grid").ChildElementCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_card_without_a_label_is_rejected(string label)
    {
        // Arrange & Act
        var render = () => Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [new FactCard(label, ["8 days"])]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(FactGrid.Cards));
    }

    [Fact]
    public void A_card_without_values_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [new FactCard("Duration", [])]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(FactGrid.Cards));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_card_with_a_blank_value_is_rejected_instead_of_rendering_an_empty_definition(string value)
    {
        // Arrange & Act
        var render = () => Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [new FactCard("Duration", ["8 days", value])]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(FactGrid.Cards));
    }

    [Fact]
    public void A_missing_card_or_value_list_is_rejected()
    {
        // Arrange & Act
        var renderNullCards = () => Render<FactGrid>(parameters => parameters.Add(component => component.Cards, null!));
        var renderNullCard = () => Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [null!]));
        var renderNullValues = () => Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [new FactCard("Duration", null!)]));

        // Assert
        renderNullCards.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(FactGrid.Cards));
        renderNullCard.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(FactGrid.Cards));
        renderNullValues.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(FactGrid.Cards));
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_list()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters
            .Add(component => component.Cards, [new FactCard("Duration", ["8 days"])])
            .Add(component => component.Class, "extra")
            .AddUnmatched("aria-label", "Basic information"));

        // Assert
        var list = cut.Find("dl");
        list.ClassName.ShouldBe("site-fact-grid extra");
        list.GetAttribute("aria-label").ShouldBe("Basic information");
    }
}
