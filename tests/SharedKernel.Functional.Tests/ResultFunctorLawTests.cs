namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.TheoryName, TestTraits.FunctorLawsTheory)]
public sealed class ResultFunctorLawTests
{
    [Fact]
    public void Satisfies_the_functor_identity_law_for_success()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var mapped = result.Map(static value => value);

        // Assert
        TestAssert.Equal(result, mapped);
    }

    [Fact]
    public void Satisfies_the_functor_composition_law_for_success()
    {
        // Arrange
        var result = Result.Ok(" porto ");

        // Act
        var composed = result.Map(static value => value.Trim().ToUpperInvariant());
        var chained = result.Map(static value => value.Trim()).Map(static value => value.ToUpperInvariant());

        // Assert
        TestAssert.Equal(composed, chained);
    }

    [Fact]
    public void Preserves_failure_through_functor_mapping()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var mapped = result.Map(static value => value.Length);

        // Assert
        TestAssert.Equal(Result.Error<int>("Unexpected failure"), mapped);
    }
}
