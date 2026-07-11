namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.TheoryName, TestTraits.MonadLawsTheory)]
public sealed class ResultMonadLawTests
{
    [Fact]
    public void Satisfies_the_monad_left_identity_law()
    {
        // Arrange
        const string value = "porto";

        // Act
        var left = Result.Ok(value).Bind(static city => Result.Ok(city.ToUpperInvariant()));
        var right = Result.Ok(value.ToUpperInvariant());

        // Assert
        (left).ShouldBe(right);
    }

    [Fact]
    public void Satisfies_the_monad_right_identity_law()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var bound = result.Bind(Result.Ok);

        // Assert
        (bound).ShouldBe(result);
    }

    [Fact]
    public void Satisfies_the_monad_associativity_law()
    {
        // Arrange
        var result = Result.Ok(" porto ");

        // Act
        var left = result
            .Bind(static city => Result.Ok(city.Trim()))
            .Bind(static city => Result.Ok(city.ToUpperInvariant()));
        var right = result.Bind(static city =>
            Result.Ok(city.Trim()).Bind(static trimmed => Result.Ok(trimmed.ToUpperInvariant())));

        // Assert
        (right).ShouldBe(left);
    }

    [Fact]
    public void Preserves_failure_through_monad_binding()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var bound = result.Bind(static value => Result.Ok(value.Length));

        // Assert
        (bound).ShouldBe(Result.Error<int>("Unexpected failure"));
    }
}
