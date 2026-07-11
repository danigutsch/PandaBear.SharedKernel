namespace SharedKernel.Functional.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public static class OptionCoreTests
{
    [Fact]
    public static void Creates_an_option_with_a_value()
    {
        // Arrange
        const string expectedValue = "porto";

        // Act
        var option = Option.Some(expectedValue);

        // Assert
        option.HasValue.ShouldBeTrue();
        option.IsEmpty.ShouldBeFalse();
        option.Value.ShouldBe(expectedValue);
    }

    [Fact]
    public static void Creates_an_empty_option()
    {
        // Arrange
        // Act
        var option = Option.None<string>();

        // Assert
        option.HasValue.ShouldBeFalse();
        option.IsEmpty.ShouldBeTrue();
        option.Value.ShouldBeNull();
    }

    [Fact]
    public static void Returns_none_for_null_values()
    {
        // Arrange
        string? value = null;

        // Act
        var option = Option.FromNullable(value);

        // Assert
        option.HasValue.ShouldBeFalse();
        option.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public static void Supports_value_types_when_the_value_is_not_null()
    {
        // Arrange
        const int expectedValue = 42;

        // Act
        var option = Option.Some(expectedValue);

        // Assert
        option.HasValue.ShouldBeTrue();
        option.Value.ShouldBe(expectedValue);
    }
}
