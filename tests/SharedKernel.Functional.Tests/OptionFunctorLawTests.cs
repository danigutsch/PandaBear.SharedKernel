namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.OptionCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.TheoryName, TestTraits.FunctorLawsTheory)]
public sealed class OptionFunctorLawTests
{
    [Fact]
    public void Satisfies_The_Functor_Identity_Law()
    {
        // Arrange
        var option = Option.Some("porto");

        // Act
        var mapped = option.Map(static value => value);

        // Assert
        Assert.Equal(option, mapped);
    }

    [Fact]
    public void Satisfies_The_Functor_Composition_Law()
    {
        // Arrange
        var option = Option.Some("porto");
        static string first(string value) => value.Trim();
        static string second(string value) => value.ToUpperInvariant();

        // Act
        var composed = option.Map(value => second(first(value)));
        var chained = option.Map(first).Map(second);

        // Assert
        Assert.Equal(composed, chained);
    }
}
