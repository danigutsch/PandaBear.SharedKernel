using static SharedKernel.Blazor.Site.Tests.SiteFixtures;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the byline of a person.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class BylineTests : BunitContext
{
    [Fact]
    public void A_byline_shows_the_photo_then_the_name_then_the_job_title()
    {
        // Arrange & Act
        var cut = Render<Byline>(parameters => parameters
            .Add(component => component.Name, "Carla Giurizatto")
            .Add(component => component.JobTitle, "Founder and Emotional Travel Designer")
            .Add(component => component.Photo, Image("/carla.jpg", ImageAlternative.Decorative())));

        // Assert
        var root = cut.Find(".site-byline");
        root.TagName.ShouldBe("DIV");
        root.Children.Select(child => child.ClassName).ShouldBe(["site-byline__photo", "site-byline__text"]);
        var photo = cut.Find(".site-byline__photo img");
        photo.GetAttribute("alt").ShouldBe(string.Empty);
        photo.GetAttribute("loading").ShouldBe("lazy");
        var lines = cut.FindAll(".site-byline__text > p");
        lines.Select(line => line.ClassName).ShouldBe(["site-byline__name", "site-byline__job-title"]);
        lines[0].TextContent.ShouldBe("Carla Giurizatto");
        lines[1].TextContent.ShouldBe("Founder and Emotional Travel Designer");
    }

    [Fact]
    public void A_described_photo_keeps_its_alternative_text()
    {
        // Arrange & Act
        var cut = Render<Byline>(parameters => parameters
            .Add(component => component.Name, "Carla")
            .Add(component => component.Photo, Image("/carla.jpg", ImageAlternative.Describe("Carla smiling in Lisbon"))));

        // Assert
        cut.Find("img").GetAttribute("alt").ShouldBe("Carla smiling in Lisbon");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Without_a_photo_or_job_title_only_the_name_is_shown(string? jobTitle)
    {
        // Arrange & Act
        var cut = Render<Byline>(parameters => parameters
            .Add(component => component.Name, "Carla")
            .Add(component => component.JobTitle, jobTitle));

        // Assert
        cut.FindAll(".site-byline__photo").ShouldBeEmpty();
        cut.FindAll(".site-byline__job-title").ShouldBeEmpty();
        cut.Find(".site-byline__name").TextContent.ShouldBe("Carla");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_byline_without_a_name_is_rejected(string name)
    {
        // Arrange & Act
        var render = () => Render<Byline>(parameters => parameters.Add(component => component.Name, name));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(Byline.Name));
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_byline()
    {
        // Arrange & Act
        var cut = Render<Byline>(parameters => parameters
            .Add(component => component.Name, "Carla")
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x"));

        // Assert
        var root = cut.Find(".site-byline");
        BunitElementAssertions.HasClass(root.ClassList, "extra");
        root.GetAttribute("data-test").ShouldBe("x");
    }
}
