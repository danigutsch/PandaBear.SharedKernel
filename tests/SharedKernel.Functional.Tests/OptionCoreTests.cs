namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class OptionCoreTests
{
    [Fact]
    public void Creates_An_Option_With_A_Value()
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
    public void Creates_An_Empty_Option()
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
    public void Returns_None_For_Null_Values()
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
    public void Supports_Value_Types_When_The_Value_Is_Not_Null()
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
