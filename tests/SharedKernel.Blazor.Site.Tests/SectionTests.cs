namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the section with its heading, intro and divider.</summary>
public sealed class SectionTests : BunitContext
{
    [Fact]
    public void Section_is_labelled_by_its_heading_which_defaults_to_level_two()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Title, "Tours")
            .Add(component => component.Intro, "Pick one.")
            .AddChildContent("<p id=\"body\">Body</p>"));

        // Assert
        var heading = cut.Find("section > .site-section__header > h2");
        heading.TextContent.ShouldBe("Tours");
        cut.Find("section").GetAttribute("aria-labelledby").ShouldBe(heading.Id);
        cut.Find(".site-section__intro").TextContent.ShouldBe("Pick one.");
        cut.Find(".site-section__body #body").ShouldNotBeNull();
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
}
