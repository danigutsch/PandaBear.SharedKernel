namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests that a misspelled or removed parameter fails loudly instead of falling into the pass-through attributes.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class SiteComponentAttributeTests : BunitContext
{
    [Theory]
    [InlineData("ImageSrc")]
    [InlineData("imagesrc")]
    [InlineData("IMAGEALT")]
    [InlineData("ImageWidth")]
    [InlineData("imageHeight")]
    public void A_removed_image_parameter_throws_and_names_the_attribute_and_its_replacement(string name)
    {
        // Arrange & Act
        var render = () => Render<Hero>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .AddUnmatched(name, "/img/hero.jpg"));

        // Assert
        var exception = render.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain(name, StringComparison.Ordinal);
        exception.Message.ShouldContain("Hero", StringComparison.Ordinal);
        exception.Message.ShouldContain("Image", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Titel")]
    [InlineData("HeadingLevl")]
    [InlineData("Href2")]
    public void An_attribute_that_starts_with_an_upper_case_letter_is_taken_for_a_misspelled_parameter(string name)
    {
        // Arrange & Act
        var render = () => Render<Card>(parameters => parameters
            .Add(component => component.Heading, "Atacama")
            .Add(component => component.Href, LinkTarget.FromString("/tours/atacama"))
            .AddUnmatched(name, "x"));

        // Assert
        render.ShouldThrow<InvalidOperationException>().Message.ShouldContain(name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("data-tour")]
    [InlineData("aria-describedby")]
    [InlineData("id")]
    [InlineData("dir")]
    [InlineData("lang")]
    [InlineData("tabindex")]
    [InlineData("title")]
    public void HTML_attributes_still_pass_through_to_the_root_element(string name)
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .AddUnmatched(name, "value"));

        // Assert
        cut.Find("section").GetAttribute(name).ShouldBe("value");
        cut.Find("h2").TextContent.ShouldBe("Tours");
    }

    [Theory]
    [InlineData("a b")]
    [InlineData("a\"b")]
    [InlineData("a=b")]
    [InlineData("a>b")]
    public void An_attribute_name_that_HTML_cannot_hold_throws(string name)
    {
        // Arrange & Act
        var render = () => Render<Section>(parameters => parameters
            .Add(component => component.Heading, "Tours")
            .AddUnmatched(name, "x"));

        // Assert
        render.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void The_check_runs_again_when_parameters_change()
    {
        // Arrange
        var cut = Render<SiteFooter>();

        // Act
        var rerender = () => cut.Render(parameters => parameters.AddUnmatched("Copyright", "(c) 2026"));

        // Assert
        rerender.ShouldThrow<InvalidOperationException>().Message.ShouldContain("Copyright", StringComparison.Ordinal);
    }
}
