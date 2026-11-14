using PandaBear.Blazor.Site.Theming;
using PandaBear.Results;

namespace PandaBear.Blazor.Site.Tests.Theming;

/// <summary>Tests the shape rules of font sizes and typography styles.</summary>
public sealed class TypographyValueTests
{
    [Fact]
    public void A_fixed_size_has_equal_bounds_and_a_fluid_size_keeps_both()
    {
        // Arrange & Act
        var fixedSize = FontSize.Fixed(1.25m).ShouldBeSuccess();
        var fluid = FontSize.Fluid(1m, 1.5m).ShouldBeSuccess();

        // Assert
        fixedSize.Min.ShouldBe(1.25m);
        fixedSize.Max.ShouldBe(1.25m);
        fixedSize.IsFluid.ShouldBeFalse();
        fluid.Min.ShouldBe(1m);
        fluid.Max.ShouldBe(1.5m);
        fluid.IsFluid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(0.4, 1)]
    [InlineData(1, 10.5)]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    [InlineData(1.00001, 2)]
    public void Fluid_sizes_need_a_minimum_below_the_maximum_within_the_allowed_range(double min, double max)
    {
        // Arrange & Act
        var result = FontSize.Fluid((decimal)min, (decimal)max);

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        result.FieldErrors().Keys.ShouldContain("size");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.49)]
    [InlineData(10.01)]
    public void Fixed_sizes_outside_the_range_are_invalid(double rem)
    {
        // Arrange & Act
        var result = FontSize.Fixed((decimal)rem);

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
    }

    [Fact]
    public void An_empty_style_inherits_everything()
    {
        // Arrange & Act
        var style = TypographyStyle.Create().ShouldBeSuccess();

        // Assert
        style.ShouldBe(TypographyStyle.Inherit);
        style.FontKey.ShouldBeNull();
        style.Fallback.ShouldBeNull();
        style.Size.ShouldBeNull();
        style.Weight.ShouldBeNull();
        style.LineHeight.ShouldBeNull();
        style.LetterSpacing.ShouldBeNull();
        style.Transform.ShouldBeNull();
        style.Italic.ShouldBeNull();
    }

    [Fact]
    public void A_complete_style_keeps_every_value()
    {
        // Arrange
        var size = FontSize.Fluid(2m, 3m).ShouldBeSuccess();

        // Act
        var style = TypographyStyle.Create("display", GenericFontFamily.Serif, size, 800, 1.1m, -0.02m, ThemeTextTransform.Uppercase, true).ShouldBeSuccess();

        // Assert
        style.FontKey.ShouldBe("display");
        style.Fallback.ShouldBe(GenericFontFamily.Serif);
        style.Size.ShouldBe(size);
        style.Weight.ShouldBe(800);
        style.LineHeight.ShouldBe(1.1m);
        style.LetterSpacing.ShouldBe(-0.02m);
        style.Transform.ShouldBe(ThemeTextTransform.Uppercase);
        style.Italic.ShouldBe(true);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(450)]
    [InlineData(1000)]
    public void Weights_must_be_a_multiple_of_100_from_100_to_900(int weight)
    {
        // Arrange & Act
        var result = TypographyStyle.Create(weight: weight);

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        result.FieldErrors().Keys.ShouldContain("weight");
    }

    [Theory]
    [InlineData(0.7)]
    [InlineData(3.1)]
    public void Line_heights_outside_0_8_to_3_are_invalid(double lineHeight)
    {
        // Arrange & Act
        var result = TypographyStyle.Create(lineHeight: (decimal)lineHeight);

        // Assert
        result.FieldErrors().Keys.ShouldContain("lineHeight");
    }

    [Theory]
    [InlineData(-0.11)]
    [InlineData(0.51)]
    public void Letter_spacing_outside_minus_0_1_to_0_5_em_is_invalid(double spacing)
    {
        // Arrange & Act
        var result = TypographyStyle.Create(letterSpacing: (decimal)spacing);

        // Assert
        result.FieldErrors().Keys.ShouldContain("letterSpacing");
    }

    [Theory]
    [InlineData("Inter")]
    [InlineData("inter\"")]
    [InlineData("-x")]
    [InlineData("a b")]
    [InlineData("")]
    public void Font_keys_must_be_lower_case_slugs(string key)
    {
        // Arrange & Act
        var result = TypographyStyle.Create(fontKey: key);

        // Assert
        result.FieldErrors().Keys.ShouldContain("fontKey");
    }

    [Fact]
    public void Undefined_enum_values_are_invalid()
    {
        // Arrange & Act
        var result = TypographyStyle.Create(fallback: (GenericFontFamily)42, transform: (ThemeTextTransform)42);

        // Assert
        result.FieldErrors().Keys.ShouldContain("fallback");
        result.FieldErrors().Keys.ShouldContain("transform");
    }
}
