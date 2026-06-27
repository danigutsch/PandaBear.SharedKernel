namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.TheoryName, TestTraits.FunctorLawsTheory)]
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
        Assert.Equal(option, mapped);
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
        Assert.Equal(composed, chained);
    }
}
