namespace SharedKernel.Functional.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
[Trait(Testing.SharedKernelTestTraitNames.TheoryName, TestTraits.FunctorLawsTheory)]
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
        (mapped).ShouldBe(result);
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
        (chained).ShouldBe(composed);
    }

    [Fact]
    public void Preserves_failure_through_functor_mapping()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var mapped = result.Map(static value => value.Length);

        // Assert
        (mapped).ShouldBe(Result.Error<int>("Unexpected failure"));
    }
}
