namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.TheoryName, TestTraits.MatchSemanticsTheory)]
public sealed class OptionCompositionTests
{
    [Fact]
    public void Returns_The_Some_Branch_Value_When_A_Value_Is_Present()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var result = option.Match(
            static value => value.ToUpperInvariant(),
            static () => "EMPTY");

        // Assert
        Assert.Equal("PORTO", result);
    }

    [Fact]
    public void Returns_The_None_Branch_Value_When_No_Value_Is_Present()
    {
        // Arrange
        var option = Option.None<string>();

        // Act
        var result = option.Match(
            static value => value.ToUpperInvariant(),
            static () => "EMPTY");

        // Assert
        Assert.Equal("EMPTY", result);
    }

    [Fact]
    public void Returns_None_When_The_Option_Is_Empty()
    {
        // Arrange
        var option = Option.None<string>();

        // Act
        var mapped = option.Map(static value => value.ToUpperInvariant());

        // Assert
        Assert.Equal(Option.None<string>(), mapped);
    }

    [Fact]
    public void Returns_None_When_The_Option_Is_Empty_After_Binding()
    {
        // Arrange
        var option = Option.None<string>();

        // Act
        var bound = option.Bind(static value => Option.Some(value.ToUpperInvariant()));

        // Assert
        Assert.Equal(Option.None<string>(), bound);
    }

    [Fact]
    public void Can_Project_Reference_Options_Into_Value_Type_Options()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var mapped = option.Map(static value => value.Length);

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public void Can_Project_Into_Value_Type_Options()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var bound = option.Bind(static value => Option.Some(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), bound);
    }
}
