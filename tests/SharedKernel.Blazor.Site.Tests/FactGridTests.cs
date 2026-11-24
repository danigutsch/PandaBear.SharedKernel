using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the grid of fact cards.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class FactGridTests : BunitContext
{
    private static readonly RenderFragment ClockIcon = builder => builder.AddMarkupContent(0, "<svg id=\"clock\" viewBox=\"0 0 24 24\"><circle cx=\"12\" cy=\"12\" r=\"10\" /></svg>");

    [Fact]
    public void A_fact_grid_is_a_description_list_of_cards_with_a_term_and_a_definition_per_value()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards,
        [
            new FactCard("Duration", [new("8 days"), new("7 nights")]),
            new FactCard("Group", [new("Up to 12")]),
        ]));

        // Assert
        var list = cut.Find("dl");
        list.ClassName.ShouldBe("site-fact-grid site-fact-grid--columns-2");
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
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [new FactCard("Duration", [new("8 days")], ClockIcon)]));

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
    public void A_linked_value_is_a_link_in_its_definition_that_opens_in_the_same_tab()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards,
        [
            new FactCard("E-mail", [new("info@example.org", LinkTarget.FromString("mailto:info@example.org"))]),
            new FactCard("Phone", [new("+55 (11) 99999-0000", LinkTarget.FromString("tel:+5511999990000"))]),
            new FactCard("WhatsApp", [new("Chat with us", LinkTarget.FromString("https://wa.me/5511999990000"))]),
        ]));

        // Assert
        var values = cut.FindAll("dd.site-fact-grid__value");
        values.Select(value => value.InnerHtml).ShouldBe(
        [
            "<a href=\"mailto:info@example.org\" aria-label=\"info@example.org\">info<wbr>@example<wbr>.org</a>",
            "<a href=\"tel:+5511999990000\">+55 (11) 99999-0000</a>",
            "<a href=\"https://wa.me/5511999990000\">Chat with us</a>",
        ]);
        cut.FindAll("a[target], a[rel]").ShouldBeEmpty();
    }

    [Fact]
    public void Plain_and_linked_values_can_share_a_card_and_a_plain_value_has_no_link()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards,
        [
            new FactCard("Office", [new("Mon to Fri, 9 a.m.–6 p.m."), new("Directions", LinkTarget.FromString("/contact#map"))]),
        ]));

        // Assert
        var values = cut.FindAll("dd.site-fact-grid__value");
        values.Select(value => value.InnerHtml).ShouldBe(["Mon to Fri, 9 a.m.–6 p.m.", "<a href=\"/contact#map\">Directions</a>"]);
    }

    [Fact]
    public void Value_text_is_encoded_with_or_without_a_link()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards,
        [
            new FactCard("Note", [new("<b>Free</b> & open"), new("<i>Map</i>", LinkTarget.FromString("/map"))]),
        ]));

        // Assert
        cut.FindAll("b, i").ShouldBeEmpty();
        cut.FindAll("dd").Select(value => value.TextContent).ShouldBe(["<b>Free</b> & open", "<i>Map</i>"]);
    }

    [Fact]
    public void A_card_without_an_icon_renders_no_icon_element()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [new FactCard("Group", [new("Up to 12")])]));

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
        var list = cut.Find("dl.site-fact-grid");
        list.ChildElementCount.ShouldBe(0);
        list.ClassName.ShouldBe("site-fact-grid");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    [InlineData(5, 3)]
    [InlineData(6, 3)]
    [InlineData(7, 4)]
    [InlineData(8, 4)]
    [InlineData(9, 3)]
    [InlineData(10, 4)]
    [InlineData(12, 4)]
    [InlineData(13, 4)]
    public void The_column_count_balances_the_rows_with_at_most_four_columns(int count, int columns)
    {
        // Arrange
        var cards = Enumerable.Range(1, count).Select(index => new FactCard($"Fact {index}", [new("Value")])).ToArray();

        // Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards, cards));

        // Assert
        cut.Find("dl").ClassName.ShouldBe($"site-fact-grid site-fact-grid--columns-{columns}");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_card_without_a_label_is_rejected(string label)
    {
        // Arrange & Act
        var render = () => Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [new FactCard(label, [new("8 days")])]));

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
        var render = () => Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [new FactCard("Duration", [new("8 days"), new(value)])]));

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
        var renderNullValue = () => Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [new FactCard("Duration", [new("8 days"), null!])]));
        var renderNullText = () => Render<FactGrid>(parameters => parameters.Add(component => component.Cards, [new FactCard("Duration", [new(null!)])]));

        // Assert
        renderNullCards.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(FactGrid.Cards));
        renderNullCard.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(FactGrid.Cards));
        renderNullValues.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(FactGrid.Cards));
        renderNullValue.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(FactGrid.Cards));
        renderNullText.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(FactGrid.Cards));
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_list()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters
            .Add(component => component.Cards, [new FactCard("Duration", [new("8 days")])])
            .Add(component => component.Class, "extra")
            .AddUnmatched("aria-label", "Basic information"));

        // Assert
        var list = cut.Find("dl");
        list.ClassName.ShouldBe("site-fact-grid site-fact-grid--columns-1 extra");
        list.GetAttribute("aria-label").ShouldBe("Basic information");
    }

    [Fact]
    public void An_e_mail_value_may_break_only_before_its_at_sign_and_its_dots()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards,
        [
            new FactCard("E-mail", [new("info.desk@example.org", LinkTarget.FromString("mailto:info.desk@example.org")), new("Write to a.b@c.org")]),
        ]));

        // Assert
        var values = cut.FindAll("dd.site-fact-grid__value");
        values.Select(value => value.InnerHtml).ShouldBe(
        [
            "<a href=\"mailto:info.desk@example.org\" aria-label=\"info.desk@example.org\">info<wbr>.desk<wbr>@example<wbr>.org</a>",
            "Write to a<wbr>.b<wbr>@c<wbr>.org",
        ]);
        values.Select(value => value.TextContent).ShouldBe(["info.desk@example.org", "Write to a.b@c.org"]);
    }

    [Fact]
    public void A_value_without_an_at_sign_keeps_its_dots_unbroken_and_its_text_encoded()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards,
        [
            new FactCard("Distance", [new("8.5 km a day. <b>Flat</b>"), new("<i>x</i>@y.z")]),
        ]));

        // Assert
        var values = cut.FindAll("dd.site-fact-grid__value");
        values[0].InnerHtml.ShouldBe("8.5 km a day. &lt;b&gt;Flat&lt;/b&gt;");
        values[1].InnerHtml.ShouldBe("&lt;i&gt;x&lt;/i&gt;<wbr>@y<wbr>.z");
    }

    [Fact]
    public void A_dot_that_ends_an_address_or_comes_before_closing_punctuation_gets_no_break()
    {
        // Arrange & Act
        var cut = Render<FactGrid>(parameters => parameters.Add(component => component.Cards,
        [
            new FactCard("E-mail", [new("Write to a.b@c.org."), new("(x@y.org.)"), new("x@y.org...")]),
        ]));

        // Assert
        cut.FindAll("dd.site-fact-grid__value").Select(value => value.InnerHtml).ShouldBe(
        [
            "Write to a<wbr>.b<wbr>@c<wbr>.org.",
            "(x<wbr>@y<wbr>.org.)",
            "x<wbr>@y<wbr>.org...",
        ]);
    }
}
