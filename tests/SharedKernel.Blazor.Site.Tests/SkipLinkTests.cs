namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the skip link.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class SkipLinkTests : BunitContext
{
    [Fact]
    public void Link_points_at_the_default_main_target_with_the_supplied_label()
    {
        // Arrange & Act
        var cut = Render<SkipLink>(parameters => parameters.AddChildContent("Skip to content"));

        // Assert
        var link = cut.Find("a");
        link.GetAttribute("href").ShouldBe("#main-content");
        link.TextContent.ShouldBe("Skip to content");
        BunitElementAssertions.HasClass(link.ClassList, "site-skip-link");
    }

    [Fact]
    public void Target_ID_and_extra_attributes_are_applied()
    {
        // Arrange & Act
        var cut = Render<SkipLink>(parameters => parameters
            .Add(component => component.TargetId, "content")
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x")
            .AddChildContent("Skip"));

        // Assert
        var link = cut.Find("a");
        link.GetAttribute("href").ShouldBe("#content");
        BunitElementAssertions.HasClass(link.ClassList, "site-skip-link");
        BunitElementAssertions.HasClass(link.ClassList, "extra");
        link.GetAttribute("data-test").ShouldBe("x");
    }

    [Fact]
    public void Extra_attributes_cannot_replace_the_target_or_the_class()
    {
        // Arrange & Act
        var cut = Render<SkipLink>(parameters => parameters
            .AddUnmatched("href", "https://evil.example")
            .AddUnmatched("class", "replaced")
            .AddChildContent("Skip"));

        // Assert
        var link = cut.Find("a");
        link.GetAttribute("href").ShouldBe("#main-content");
        BunitElementAssertions.HasClass(link.ClassList, "site-skip-link");
    }

    [Fact]
    public void Blank_target_ID_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<SkipLink>(parameters => parameters
            .Add(component => component.TargetId, " ")
            .AddChildContent("Skip"));

        // Assert
        render.ShouldThrow<ArgumentException>();
    }
}
