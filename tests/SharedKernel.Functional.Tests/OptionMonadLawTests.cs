namespace SharedKernel.Functional.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
[Trait(Testing.SharedKernelTestTraitNames.TheoryName, TestTraits.MonadLawsTheory)]
public sealed class OptionMonadLawTests
{
    [Fact]
    public void Satisfies_the_monad_left_identity_law()
    {
        // Arrange
        const string value = "porto";

        // Act
        var left = Option.Some(value).Bind(static city => Option.Some(city.ToUpperInvariant()));
        var right = Option.Some(value.ToUpperInvariant());

        // Assert
        left.ShouldBe(right);
    }

    [Fact]
    public void Satisfies_the_monad_right_identity_law()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var bound = option.Bind(Option.Some);

        // Assert
        bound.ShouldBe(option);
    }

    [Fact]
    public void Satisfies_the_monad_associativity_law()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var left = option
            .Bind(static city => Option.Some(city.Trim()))
            .Bind(static city => Option.Some(city.ToUpperInvariant()));
        var right = option.Bind(static city =>
            Option.Some(city.Trim()).Bind(static trimmed => Option.Some(trimmed.ToUpperInvariant())));

        // Assert
        right.ShouldBe(left);
    }
}
