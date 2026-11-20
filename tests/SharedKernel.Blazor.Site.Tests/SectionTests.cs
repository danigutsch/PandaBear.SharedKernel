namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the section with its heading, intro and divider.</summary>
public sealed class SectionTests : BunitContext
{
    [Fact]
    public void Section_has_a_level_two_heading_intro_and_body_and_no_generated_ID()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Title, "Tours")
            .Add(component => component.Intro, "Pick one.")
            .AddChildContent("<p id=\"body\">Body</p>"));

        // Assert
        var heading = cut.Find("section > .site-section__header > h2");
        heading.TextContent.ShouldBe("Tours");
        heading.HasAttribute("id").ShouldBeFalse();
        cut.Find("section").HasAttribute("aria-labelledby").ShouldBeFalse();
        cut.Find(".site-section__intro").TextContent.ShouldBe("Pick one.");
        cut.Find(".site-section__body #body").ShouldNotBeNull();
    }

    [Fact]
    public void Section_markup_is_the_same_on_every_render_so_prerendering_and_the_circuit_agree()
    {
        // Arrange & Act
        var first = Render<Section>(parameters => parameters.Add(component => component.Title, "Tours"));
        var second = Render<Section>(parameters => parameters.Add(component => component.Title, "Tours"));

        // Assert
        second.Markup.ShouldBe(first.Markup);
    }

    [Fact]
    public void A_heading_ID_labels_the_section_by_its_heading()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Title, "Tours")
            .Add(component => component.HeadingId, "tours-heading"));

        // Assert
        cut.Find("h2").Id.ShouldBe("tours-heading");
        cut.Find("section").GetAttribute("aria-labelledby").ShouldBe("tours-heading");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("two words")]
    [InlineData("two\u00A0words")]
    [InlineData("two\u2003words")]
    public void A_blank_or_spaced_heading_ID_is_rejected(string id)
    {
        // Arrange & Act
        var render = () => Render<Section>(parameters => parameters
            .Add(component => component.Title, "Tours")
            .Add(component => component.HeadingId, id));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(Section.HeadingId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public void Heading_level_is_configurable(int level)
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Title, "Tours")
            .Add(component => component.Level, level));

        // Assert
        cut.Find($"h{level}").TextContent.ShouldBe("Tours");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Heading_level_outside_one_to_six_is_rejected(int level)
    {
        // Arrange & Act
        var render = () => Render<Section>(parameters => parameters
            .Add(component => component.Title, "Tours")
            .Add(component => component.Level, level));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Divider_is_decorative_and_can_be_switched_off()
    {
        // Arrange & Act
        var with = Render<Section>(parameters => parameters.Add(component => component.Title, "Tours"));
        var without = Render<Section>(parameters => parameters
            .Add(component => component.Title, "Tours")
            .Add(component => component.Divider, false));

        // Assert
        with.Find(".site-divider").GetAttribute("aria-hidden").ShouldBe("true");
        without.FindAll(".site-divider").ShouldBeEmpty();
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_section()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Title, "Tours")
            .Add(component => component.Class, "extra")
            .AddUnmatched("id", "tours"));

        // Assert
        var section = cut.Find("section");
        BunitElementAssertions.HasClass(section.ClassList, "site-section");
        BunitElementAssertions.HasClass(section.ClassList, "extra");
        section.Id.ShouldBe("tours");
    }

    [Fact]
    public void A_section_without_media_keeps_its_header_and_body_as_children()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters.Add(component => component.Title, "Trips"));

        // Assert
        var section = cut.Find("section");
        section.ClassName.ShouldBe("site-section");
        section.Children.Select(child => child.ClassName).ShouldBe(["site-section__header", "site-section__body"]);
    }

    [Fact]
    public void Media_splits_the_section_into_the_text_then_the_media_in_reading_order()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Title, "Past trips")
            .Add(component => component.Media, "<img id=\"photo\" src=\"/p.jpg\" alt=\"\">")
            .AddChildContent("<a id=\"more\" href=\"/trips\">More</a>"));

        // Assert
        var section = cut.Find("section");
        BunitElementAssertions.HasClass(section.ClassList, "site-section--split");
        section.Children.Select(child => child.ClassName).ShouldBe(["site-section__main", "site-section__media"]);
        section.Children[0].Children.Select(child => child.ClassName).ShouldBe(["site-section__header", "site-section__body"]);
        cut.Find(".site-section__main #more").ShouldNotBeNull();
        cut.Find(".site-section__media #photo").ShouldNotBeNull();
        cut.FindAll("aside").ShouldBeEmpty();
    }
}
