namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the "from" price line.</summary>
public sealed class FromPriceTests : BunitContext
{
    [Fact]
    public void The_format_is_split_at_its_placeholder_around_the_amount_in_a_data_element()
    {
        // Arrange & Act
        var cut = Render<FromPrice>(parameters => parameters
            .Add(component => component.Format, "a partir de {0} por pessoa")
            .Add(component => component.AmountText, "1.234,50 €")
            .Add(component => component.Value, 1234.50m));

        // Assert
        var line = cut.Find("p");
        line.ClassName.ShouldBe("site-from-price");
        line.TextContent.ShouldBe("a partir de 1.234,50 € por pessoa");
        var data = line.QuerySelector("data.site-from-price__amount").ShouldNotBeNull();
        data.GetAttribute("value").ShouldBe("1234.50");
        data.TextContent.ShouldBe("1.234,50 €");
    }

    [Theory]
    [InlineData("{0}", "300 €")]
    [InlineData("from {0}", "from 300 €")]
    [InlineData("{0} and up", "300 € and up")]
    public void The_placeholder_may_open_close_or_be_the_whole_format(string format, string text)
    {
        // Arrange & Act
        var cut = Render<FromPrice>(parameters => parameters
            .Add(component => component.Format, format)
            .Add(component => component.AmountText, "300 €")
            .Add(component => component.Value, 300m));

        // Assert
        cut.Find("p").TextContent.ShouldBe(text);
    }

    [Fact]
    public void The_text_around_the_amount_is_encoded_and_not_formatted()
    {
        // Arrange & Act
        var cut = Render<FromPrice>(parameters => parameters
            .Add(component => component.Format, "<b>{1}</b> {0}")
            .Add(component => component.AmountText, "300 €")
            .Add(component => component.Value, 300m));

        // Assert
        cut.Find("p").TextContent.ShouldBe("<b>{1}</b> 300 €");
        cut.FindAll("b").ShouldBeEmpty();
    }

    [Fact]
    public void Class_and_attributes_reach_the_paragraph()
    {
        // Arrange & Act
        var cut = Render<FromPrice>(parameters => parameters
            .Add(component => component.Format, "from {0}")
            .Add(component => component.AmountText, "300 €")
            .Add(component => component.Value, 300m)
            .Add(component => component.Class, "lead")
            .AddUnmatched("id", "price"));

        // Assert
        var line = cut.Find("p");
        line.ClassName.ShouldBe("site-from-price lead");
        line.Id.ShouldBe("price");
    }

    [Theory]
    [InlineData("")]
    [InlineData("from")]
    [InlineData("from {0} to {0}")]
    [InlineData("from {{0}}")]
    [InlineData("from {{0}")]
    [InlineData("from {0}}")]
    public void A_format_without_exactly_one_placeholder_is_refused(string format)
    {
        // Arrange & Act
        var render = () => Render<FromPrice>(parameters => parameters
            .Add(component => component.Format, format)
            .Add(component => component.AmountText, "300 €")
            .Add(component => component.Value, 300m));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(FromPrice.Format));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_amount_is_refused(string amount)
    {
        // Arrange & Act
        var render = () => Render<FromPrice>(parameters => parameters
            .Add(component => component.Format, "from {0}")
            .Add(component => component.AmountText, amount)
            .Add(component => component.Value, 300m));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(FromPrice.AmountText));
    }
}
