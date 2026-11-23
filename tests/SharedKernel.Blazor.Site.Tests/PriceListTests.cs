using System.Globalization;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the list of prices.</summary>
public sealed class PriceListTests : BunitContext
{
    private static readonly PriceItem[] Prices =
    [
        new("Double room", "1.234,50 €", 1234.50m, "per person"),
        new("Single supplement", "300 €", 300m),
    ];

    [Fact]
    public void A_price_list_is_a_description_list_of_rows_with_a_term_and_an_amount_in_a_data_element()
    {
        // Arrange & Act
        var cut = Render<PriceList>(parameters => parameters.Add(component => component.Items, Prices));

        // Assert
        var root = cut.Find(".site-price-list");
        root.TagName.ShouldBe("DIV");
        root.HasAttribute("role").ShouldBeFalse();
        var list = cut.Find(".site-price-list > dl.site-price-list__items");
        list.Children.Select(child => child.ClassName).ShouldBe(["site-price-list__row", "site-price-list__row"]);
        var first = list.Children[0];
        first.Children.Select(child => child.TagName).ShouldBe(["DT", "DD", "DD"]);
        first.Children.Select(child => child.ClassName).ShouldBe(["site-price-list__label", "site-price-list__amount", "site-price-list__note"]);
        first.Children.Select(child => child.TextContent).ShouldBe(["Double room", "1.234,50 €", "per person"]);
        var data = first.QuerySelector("dd.site-price-list__amount > data").ShouldNotBeNull();
        data.GetAttribute("value").ShouldBe("1234.50");
        list.Children[1].Children.Select(child => child.TagName).ShouldBe(["DT", "DD"]);
        list.Children[1].QuerySelector("data")!.GetAttribute("value").ShouldBe("300");
    }

    [Fact]
    public void The_machine_value_is_written_with_the_invariant_culture_whatever_the_current_culture()
    {
        // Arrange & Act
        var value = CultureOverride.Run(CultureInfo.GetCultureInfo("pt-BR"), () =>
            Render<PriceList>(parameters => parameters.Add(component => component.Items, [new PriceItem("Base", "R$ 1.234,56", 1234.56m)])).Find("data").GetAttribute("value"));

        // Assert
        value.ShouldBe("1234.56");
    }

    [Fact]
    public void An_amount_without_a_value_is_plain_text_without_a_data_element()
    {
        // Arrange & Act
        var cut = Render<PriceList>(parameters => parameters.Add(component => component.Items, [new PriceItem("Groups", "On request", null)]));

        // Assert
        var amount = cut.Find("dd.site-price-list__amount");
        amount.TextContent.ShouldBe("On request");
        amount.QuerySelector("data").ShouldBeNull();
    }

    [Fact]
    public void A_heading_ID_makes_the_list_and_its_conditions_a_group_named_by_that_heading()
    {
        // Arrange & Act
        var cut = Render<PriceList>(parameters => parameters
            .Add(component => component.Items, Prices)
            .Add(component => component.HeadingId, "prices")
            .Add(component => component.Conditions, "<p id=\"deposit\">A 30% deposit secures the place.</p>"));

        // Assert
        var root = cut.Find(".site-price-list");
        root.GetAttribute("role").ShouldBe("group");
        root.GetAttribute("aria-labelledby").ShouldBe("prices");
        root.Children.Select(child => child.ClassName).ShouldBe(["site-price-list__items", "site-price-list__conditions"]);
        cut.Find(".site-price-list__conditions > #deposit").ShouldNotBeNull();
    }

    [Fact]
    public void Without_items_only_the_conditions_render()
    {
        // Arrange & Act
        var cut = Render<PriceList>(parameters => parameters
            .Add(component => component.Items, [])
            .Add(component => component.Conditions, "<p>Prices follow.</p>"));

        // Assert
        cut.FindAll("dl").ShouldBeEmpty();
        cut.Find(".site-price-list__conditions").TextContent.ShouldBe("Prices follow.");
    }

    [Fact]
    public void Class_and_attributes_reach_the_root()
    {
        // Arrange & Act
        var cut = Render<PriceList>(parameters => parameters
            .Add(component => component.Items, Prices)
            .Add(component => component.Class, "tour-prices")
            .AddUnmatched("data-tour", "atacama"));

        // Assert
        var root = cut.Find("div");
        root.ClassName.ShouldBe("site-price-list tour-prices");
        root.GetAttribute("data-tour").ShouldBe("atacama");
    }

    [Theory]
    [InlineData("", "300 €")]
    [InlineData(" ", "300 €")]
    [InlineData("Base", "")]
    [InlineData("Base", " ")]
    public void An_item_with_a_blank_label_or_amount_is_refused(string label, string amount)
    {
        // Arrange & Act
        var render = () => Render<PriceList>(parameters => parameters.Add(component => component.Items, [new PriceItem(label, amount, 300m)]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(PriceList.Items));
    }

    [Fact]
    public void A_missing_item_is_refused()
    {
        // Arrange & Act
        var render = () => Render<PriceList>(parameters => parameters.Add(component => component.Items, [null!]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(PriceList.Items));
    }

    [Fact]
    public void A_missing_list_is_refused()
    {
        // Arrange & Act
        var render = () => Render<PriceList>(parameters => parameters.Add(component => component.Items, null!));

        // Assert
        render.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(PriceList.Items));
    }

    [Theory]
    [InlineData("")]
    [InlineData("two words")]
    public void A_heading_ID_that_is_empty_or_holds_white_space_is_refused(string id)
    {
        // Arrange & Act
        var render = () => Render<PriceList>(parameters => parameters
            .Add(component => component.Items, Prices)
            .Add(component => component.HeadingId, id));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(PriceList.HeadingId));
    }
}
