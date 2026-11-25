namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the hero's decorative rule between its title and its eyebrow and subtitle.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class HeroDividerTests : BunitContext
{
    [Fact]
    public void A_divided_hero_marks_its_root_and_adds_no_element_to_the_heading_group()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Eyebrow, "Since 2004")
            .Add(component => component.Title, "Discover")
            .Add(component => component.Subtitle, "Small groups")
            .Add(component => component.Layout, HeroLayout.Cover)
            .Add(component => component.Divider, true)
            .Add(component => component.Class, "extra"));

        // Assert
        var root = cut.Find("section");
        BunitElementAssertions.HasClass(root.ClassList, "site-hero--divided");
        BunitElementAssertions.HasClass(root.ClassList, "site-hero--cover");
        BunitElementAssertions.HasClass(root.ClassList, "extra");
        cut.Find("hgroup").Children.Select(child => child.TagName).ShouldBe(["P", "H1", "P"]);
    }

    [Fact]
    public void A_hero_has_no_rule_by_default()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters.Add(component => component.Title, "Discover"));

        // Assert
        cut.Find("section").ClassList.ShouldNotContain("site-hero--divided");
    }
}
