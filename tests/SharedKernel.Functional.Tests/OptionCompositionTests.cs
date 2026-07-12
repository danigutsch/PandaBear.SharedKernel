namespace SharedKernel.Functional.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
[Trait(Testing.SharedKernelTestTraitNames.TheoryName, TestTraits.MatchSemanticsTheory)]
public static class OptionCompositionTests
{
    [Fact]
    public static void Returns_the_some_branch_value_when_a_value_is_present()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var result = option.Match(
            static value => value.ToUpperInvariant(),
            static () => "EMPTY");

        // Assert
        result.ShouldBe("PORTO");
    }

    [Fact]
    public static void Returns_the_none_branch_value_when_no_value_is_present()
    {
        // Arrange
        var option = Option.None<string>();

        // Act
        var result = option.Match(
            static value => value.ToUpperInvariant(),
            static () => "EMPTY");

        // Assert
        result.ShouldBe("EMPTY");
    }

    [Fact]
    public static void Returns_none_when_the_option_is_empty()
    {
        // Arrange
        var option = Option.None<string>();

        // Act
        var mapped = option.Map(static value => value.ToUpperInvariant());

        // Assert
        mapped.ShouldBe(Option.None<string>());
    }

    [Fact]
    public static void Returns_none_when_the_option_is_empty_after_binding()
    {
        // Arrange
        var option = Option.None<string>();

        // Act
        var bound = option.Bind(static value => Option.Some(value.ToUpperInvariant()));

        // Assert
        bound.ShouldBe(Option.None<string>());
    }

    [Fact]
    public static void Can_project_reference_options_into_value_type_options()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var mapped = option.Map(static value => value.Length);

        // Assert
        mapped.ShouldBe(Option.Some(5));
    }

    [Fact]
    public static void Can_project_into_value_type_options()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var bound = option.Bind(static value => Option.Some(value.Length));

        // Assert
        bound.ShouldBe(Option.Some(5));
    }

}
