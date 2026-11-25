using SharedKernel.Blazor.Site.Theming;

namespace SharedKernel.Blazor.Site.Tests.Theming;

/// <summary>Tests the lists of allowed values an editor offers: they are exactly the values the theme's rules accept.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ThemeOptionListsTests
{
    [Fact]
    public void A_heading_colour_is_accepted_exactly_when_it_is_one_of_the_listed_colours()
    {
        // Arrange
        var roles = Enum.GetValues<ThemeColorRole>();

        // Act
        var accepted = roles.Where(role => TypographyStyle.Create(null, null, null, null, null, null, null, null, role).IsSuccess).ToArray();

        // Assert
        accepted.ShouldBe([.. TypographyStyle.Colors.Order()]);
        TypographyStyle.Colors.ShouldBe([ThemeColorRole.Text, ThemeColorRole.Primary, ThemeColorRole.Muted]);
    }

    [Fact]
    public void Only_headings_card_titles_and_the_brand_take_a_colour()
    {
        // Arrange
        var roles = Enum.GetValues<TypographyRole>();

        // Act
        var taking = roles.Where(TypographyStyle.TakesColor).ToArray();

        // Assert
        taking.ShouldBe([TypographyRole.H1, TypographyRole.H2, TypographyRole.H3, TypographyRole.H4, TypographyRole.H5, TypographyRole.H6, TypographyRole.CardTitle, TypographyRole.Brand]);
    }

    [Fact]
    public void A_divider_colour_is_accepted_exactly_when_it_is_one_of_the_listed_colours()
    {
        // Arrange
        var roles = Enum.GetValues<ThemeColorRole>();

        // Act
        var accepted = roles.Where(role => SiteShape.Create(ControlShape.Rounded, 4m, 0.25m, role).IsSuccess).ToArray();

        // Assert
        accepted.Order().ToArray().ShouldBe([.. SiteShape.DividerColors.Order()]);
        SiteShape.DividerColors.Count.ShouldBe(5);
    }

    [Fact]
    public void The_listed_weights_are_the_weights_a_face_accepts()
    {
        // Arrange
        var candidates = Enumerable.Range(0, 1001);

        // Act
        var accepted = candidates.Where(weight => ThemeFontFace.Create(weight, ThemeFontStyle.Normal, LinkTarget.FromString("/fonts/a.woff2")).IsSuccess).ToArray();

        // Assert
        accepted.ShouldBe([.. ThemeFont.Weights]);
    }
}
