
namespace SharedKernel.InputNormalization.Tests;

public sealed class NumericSanitizerTests
{
    [Fact]
    public void Sanitize_price_rounds_very_small_values()
    {
        var result = NumericSanitizer.SanitizePrice(0.001m);

        (result).ShouldBe(0.00m);
    }

    [Fact]
    public void Sanitize_price_rounds_very_small_values_up()
    {
        var result = NumericSanitizer.SanitizePrice(0.009m);

        (result).ShouldBe(0.01m);
    }

    [Fact]
    public void Sanitize_price_handles_banker_rounding_case_positive_even()
    {
        // Arrange
        // Act
        var result = NumericSanitizer.SanitizePrice(2.225m);

        (result).ShouldBe(2.23m);
    }

    [Fact]
    public void Sanitize_price_handles_banker_rounding_case_positive_odd()
    {
        // Arrange
        // Act
        var result = NumericSanitizer.SanitizePrice(2.215m);

        (result).ShouldBe(2.22m);
    }

    [Fact]
    public void Sanitize_price_handles_floating_point_precision_issue()
    {
        // Arrange
        // Act
        var result = NumericSanitizer.SanitizePrice(0.1m + 0.2m);

        (result).ShouldBe(0.30m);
    }

    [Theory]
    [InlineData(10.00, 10.00)]
    [InlineData(10.01, 10.01)]
    [InlineData(10.10, 10.10)]
    [InlineData(10.99, 10.99)]
    [InlineData(10.994, 10.99)]
    [InlineData(10.995, 11.00)]
    [InlineData(10.996, 11.00)]
    [InlineData(1.001, 1.00)]
    [InlineData(1.004, 1.00)]
    [InlineData(1.005, 1.01)]
    [InlineData(1.006, 1.01)]
    [InlineData(1.009, 1.01)]
    public void Sanitize_price_rounds_correctly_for_various_inputs(decimal input, decimal expected)
    {
        var result = NumericSanitizer.SanitizePrice(input);

        (result).ShouldBe(expected);
    }
}
