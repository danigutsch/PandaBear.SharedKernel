namespace SharedKernel.Functional.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
[Trait(Testing.SharedKernelTestTraitNames.TheoryName, TestTraits.FunctorLawsTheory)]
public sealed class OptionFunctorLawTests
{
    [Fact]
    public void Satisfies_the_functor_identity_law()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var mapped = option.Map(static value => value);

        // Assert
        mapped.ShouldBe(option);
    }

    [Fact]
    public void Satisfies_the_functor_composition_law()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var composed = option.Map(static value => value.Trim().ToUpperInvariant());
        var chained = option.Map(static value => value.Trim()).Map(static value => value.ToUpperInvariant());

        // Assert
        chained.ShouldBe(composed);
    }
}
