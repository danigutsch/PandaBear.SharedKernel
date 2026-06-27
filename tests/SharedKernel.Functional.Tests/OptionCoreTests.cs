namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class OptionCoreTests
{
    [Fact]
    public void Creates_an_option_with_a_value()
    {
        // Arrange
        const string expectedValue = "porto";

        // Act
        var option = Option.Some(expectedValue);

        // Assert
        Assert.True(option.HasValue);
        Assert.False(option.IsEmpty);
        Assert.Equal(expectedValue, option.Value);
    }

    [Fact]
    public void Creates_an_empty_option()
    {
        // Arrange
        // Act
        var option = Option.None<string>();

        // Assert
        Assert.False(option.HasValue);
        Assert.True(option.IsEmpty);
        Assert.Null(option.Value);
    }

    [Fact]
    public void Returns_none_for_null_values()
    {
        // Arrange
        string? value = null;

        // Act
        var option = Option.FromNullable(value);

        // Assert
        Assert.False(option.HasValue);
        Assert.True(option.IsEmpty);
    }

    [Fact]
    public void Supports_value_types_when_the_value_is_not_null()
    {
        // Arrange
        const int expectedValue = 42;

        // Act
        var option = Option.Some(expectedValue);

        // Assert
        Assert.True(option.HasValue);
        Assert.Equal(expectedValue, option.Value);
    }
}
