using SharedKernel.Blazor.Site.Theming;

namespace SharedKernel.Blazor.Site.Tests.Theming;

/// <summary>Tests the hex colour value object and the WCAG contrast math.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class CssColorTests
{
    [Theory]
    [InlineData("#1d4ed8", "#1d4ed8")]
    [InlineData("#1D4ED8", "#1d4ed8")]
    [InlineData("#fff", "#ffffff")]
    [InlineData("#AbC", "#aabbcc")]
    [InlineData(" #000000 ", "#000000")]
    public void Six_and_three_digit_hex_colours_are_accepted_and_normalized_to_lower_case_six_digits(string input, string expected)
    {
        // Arrange & Act
        var color = CssColor.Create(input).ShouldBeSuccess();

        // Assert
        color.Value.ShouldBe(expected);
        color.ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1d4ed8")]
    [InlineData("#1d4ed")]
    [InlineData("#1d4ed8ff")]
    [InlineData("#ggg")]
    [InlineData("red")]
    [InlineData("rgb(0,0,0)")]
    [InlineData("#000;}body{display:none")]
    [InlineData("#00 000")]
    public void Anything_but_a_hex_colour_is_invalid(string? input)
    {
        // Arrange & Act
        var result = CssColor.Create(input);

        // Assert
        result.ShouldBeFailure(Results.ResultStatus.Invalid, Results.ResultErrorCodes.Invalid);
    }

    [Fact]
    public void Validation_errors_do_not_echo_the_input()
    {
        // Arrange & Act
        var result = CssColor.Create("secret-value");

        // Assert
        result.ErrorDetails!.Detail!.ShouldNotContain("secret", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("#000000", "#ffffff", 21.0)]
    [InlineData("#ffffff", "#ffffff", 1.0)]
    [InlineData("#767676", "#ffffff", 4.54)]
    [InlineData("#1f4399", "#ffffff", 9.05)]
    [InlineData("#1d4ed8", "#ffffff", 6.70)]
    [InlineData("#1f2937", "#ffffff", 14.68)]
    public void Contrast_ratio_matches_known_wcag_pairs(string foreground, string background, double expected)
    {
        // Arrange
        var first = CssColor.FromString(foreground);
        var second = CssColor.FromString(background);

        // Act
        var ratio = first.ContrastRatio(second);
        var reversed = second.ContrastRatio(first);

        // Assert
        ratio.ShouldBe(expected, 2);
        reversed.ShouldBe(ratio, 10);
    }

    [Fact]
    public void Relative_luminance_ranges_from_black_to_white()
    {
        // Arrange & Act
        var black = CssColor.FromString("#000000").RelativeLuminance;
        var white = CssColor.FromString("#ffffff").RelativeLuminance;

        // Assert
        black.ShouldBe(0.0, 10);
        white.ShouldBe(1.0, 10);
    }

    [Fact]
    public void From_string_throws_for_an_invalid_colour()
    {
        // Arrange & Act
        var invalid = () => CssColor.FromString("blue");

        // Assert
        invalid.ShouldThrow<ArgumentException>();
    }
}
