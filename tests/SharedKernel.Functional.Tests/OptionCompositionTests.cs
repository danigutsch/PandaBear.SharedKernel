namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.TheoryName, TestTraits.MatchSemanticsTheory)]
public sealed class OptionCompositionTests
{
    [Fact]
    public void Returns_the_some_branch_value_when_a_value_is_present()
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
    public void Returns_the_none_branch_value_when_no_value_is_present()
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
    public void Returns_none_when_the_option_is_empty()
    {
        // Arrange
        var option = Option.None<string>();

        // Act
        var mapped = option.Map(static value => value.ToUpperInvariant());

        // Assert
        Assert.Equal(Option.None<string>(), mapped);
    }

    [Fact]
    public void Returns_none_when_the_option_is_empty_after_binding()
    {
        // Arrange
        var option = Option.None<string>();

        // Act
        var bound = option.Bind(static value => Option.Some(value.ToUpperInvariant()));

        // Assert
        Assert.Equal(Option.None<string>(), bound);
    }

    [Fact]
    public void Can_project_reference_options_into_value_type_options()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var mapped = option.Map(static value => value.Length);

        // Assert
        Assert.Equal(Option.Some(5), mapped);
    }

    [Fact]
    public void Can_project_into_value_type_options()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var bound = option.Bind(static value => Option.Some(value.Length));

        // Assert
        Assert.Equal(Option.Some(5), bound);
    }
}
