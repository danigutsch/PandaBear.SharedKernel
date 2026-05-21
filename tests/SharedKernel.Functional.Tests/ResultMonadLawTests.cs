namespace SharedKernel.Functional.Tests;

[Trait(TestTraits.CapabilityName, TestTraits.ResultCapability)]
[Trait(TestTraits.CategoryName, TestTraits.CompositionCategory)]
[Trait(TestTraits.TheoryName, TestTraits.MonadLawsTheory)]
public sealed class ResultMonadLawTests
{
    [Fact]
    public void Satisfies_The_Monad_Left_Identity_Law()
    {
        // Arrange
        const string value = "porto";

        // Act
        var left = Result.Ok(value).Bind(static city => Result.Ok(city.ToUpperInvariant()));
        var right = Result.Ok(value.ToUpperInvariant());

        // Assert
        Assert.Equal(right, left);
    }

    [Fact]
    public void Satisfies_The_Monad_Right_Identity_Law()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var bound = result.Bind(Result.Ok);

        // Assert
        Assert.Equal(result, bound);
    }

    [Fact]
    public void Satisfies_The_Monad_Associativity_Law()
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
        Assert.Equal(left, right);
    }

    [Fact]
    public void Preserves_Failure_Through_Monad_Binding()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var bound = result.Bind(static value => Result.Ok(value.Length));

        // Assert
        Assert.Equal(Result.Error<int>("Unexpected failure"), bound);
    }
}
