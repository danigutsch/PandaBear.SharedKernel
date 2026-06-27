namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.TheoryName, TestTraits.FunctorLawsTheory)]
public sealed class ResultFunctorLawTests
{
    [Fact]
    public void Satisfies_The_Functor_Identity_Law_For_Success()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var mapped = result.Map(static value => value);

        // Assert
        Assert.Equal(result, mapped);
    }

    [Fact]
    public void Satisfies_The_Functor_Composition_Law_For_Success()
    {
        // Arrange
        var result = Result.Ok(" porto ");
        static string first(string value) => value.Trim();
        static string second(string value) => value.ToUpperInvariant();

        // Act
        var composed = result.Map(value => second(first(value)));
        var chained = result.Map(first).Map(second);

        // Assert
        Assert.Equal(composed, chained);
    }

    [Fact]
    public void Preserves_Failure_Through_Functor_Mapping()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var mapped = result.Map(static value => value.Length);

        // Assert
        Assert.Equal(Result.Error<int>("Unexpected failure"), mapped);
    }
}
