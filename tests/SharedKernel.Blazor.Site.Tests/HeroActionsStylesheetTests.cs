namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the spacing of a hero's actions in the stylesheet.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class HeroActionsStylesheetTests
{
    [Fact]
    public void The_actions_keep_a_step_of_the_spacing_scale_from_the_heading_above_them()
    {
        // Arrange
        var css = File.ReadAllText(SiteFiles.StylesheetPath());

        // Act
        var rule = SiteFiles.Rule(css, ".site-hero__actions");

        // Assert
        rule.ShouldContain("margin-block-start: var(--site-space-4);", StringComparison.Ordinal);
    }
}
