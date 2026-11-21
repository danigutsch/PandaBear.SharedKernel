namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the text-beside-media layout without a heading.</summary>
public sealed class SplitTests : BunitContext
{
    [Fact]
    public void A_split_is_a_plain_division_of_the_text_then_the_media()
    {
        // Arrange & Act
        var cut = Render<Split>(parameters => parameters
            .AddChildContent("<p id=\"tagline\">Travel with feeling.</p>")
            .Add(component => component.Media, "<img id=\"photo\" src=\"/p.jpg\" alt=\"\">"));

        // Assert
        var root = cut.Find(".site-split");
        root.TagName.ShouldBe("DIV");
        root.HasAttribute("role").ShouldBeFalse();
        root.HasAttribute("aria-label").ShouldBeFalse();
        root.Children.Select(child => child.ClassName).ShouldBe(["site-split__main", "site-split__media"]);
        root.Children.Select(child => child.TagName).ShouldBe(["DIV", "DIV"]);
        cut.Find(".site-split__main > #tagline").ShouldNotBeNull();
        cut.Find(".site-split__media > #photo").ShouldNotBeNull();
        cut.FindAll("section, aside").ShouldBeEmpty();
    }

    [Fact]
    public void The_media_is_on_the_end_side_by_default()
    {
        // Arrange & Act
        var cut = Render<Split>(parameters => parameters
            .AddChildContent("<p>Text</p>")
            .Add(component => component.Media, "<img src=\"/p.jpg\" alt=\"\">"));

        // Assert
        cut.Find(".site-split").ClassName.ShouldBe("site-split");
    }

    [Theory]
    [InlineData(SplitMediaSide.End, "site-split")]
    [InlineData(SplitMediaSide.Start, "site-split site-split--media-start")]
    public void The_media_side_sets_a_class_and_keeps_the_text_first_in_reading_order(SplitMediaSide side, string classes)
    {
        // Arrange & Act
        var cut = Render<Split>(parameters => parameters
            .Add(component => component.MediaSide, side)
            .AddChildContent("<p id=\"tagline\">Day 1</p>")
            .Add(component => component.Media, "<img id=\"photo\" src=\"/p.jpg\" alt=\"\">"));

        // Assert
        var root = cut.Find(".site-split");
        root.ClassName.ShouldBe(classes);
        root.Children.Select(child => child.ClassName).ShouldBe(["site-split__main", "site-split__media"]);
        cut.Find(".site-split__main > #tagline").ShouldNotBeNull();
        cut.Find(".site-split__media > #photo").ShouldNotBeNull();
    }

    [Fact]
    public void An_undefined_media_side_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<Split>(parameters => parameters
            .Add(component => component.MediaSide, (SplitMediaSide)7)
            .AddChildContent("<p>Text</p>")
            .Add(component => component.Media, "<img src=\"/p.jpg\" alt=\"\">"));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(Split.MediaSide));
    }

    [Fact]
    public void A_split_without_media_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<Split>(parameters => parameters.AddChildContent("<p>Text</p>"));

        // Assert
        render.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(Split.Media));
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_split()
    {
        // Arrange & Act
        var cut = Render<Split>(parameters => parameters
            .Add(component => component.Media, "<img src=\"/p.jpg\" alt=\"\">")
            .Add(component => component.Class, "extra")
            .AddUnmatched("lang", "pt-BR")
            .AddChildContent("x"));

        // Assert
        var root = cut.Find(".site-split");
        BunitElementAssertions.HasClass(root.ClassList, "extra");
        root.GetAttribute("lang").ShouldBe("pt-BR");
    }

    [Fact]
    public void The_extra_class_follows_the_media_side_class()
    {
        // Arrange & Act
        var cut = Render<Split>(parameters => parameters
            .Add(component => component.MediaSide, SplitMediaSide.Start)
            .Add(component => component.Media, "<img src=\"/p.jpg\" alt=\"\">")
            .Add(component => component.Class, "extra")
            .AddChildContent("x"));

        // Assert
        cut.Find(".site-split").ClassName.ShouldBe("site-split site-split--media-start extra");
    }
}
