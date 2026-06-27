namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.TheoryName, TestTraits.MatchSemanticsTheory)]
public sealed class ResultMatchTests
{
    [Fact]
    public void Returns_the_success_branch_value_for_generic_results()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var matched = result.Match(
            static value => value.Length,
            static error => error.Detail.Length);

        // Assert
        Assert.Equal(5, matched);
    }

    [Fact]
    public void Returns_the_failure_branch_value_for_generic_results()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var matched = result.Match(
            static value => value.Length,
            static error => error.Detail.Length);

        // Assert
        Assert.Equal("Unexpected failure".Length, matched);
    }

    [Fact]
    public void Returns_the_success_branch_value_for_non_generic_results()
    {
        // Arrange
        var result = Result.Ok();

        // Act
        var matched = result.Match(
            static () => "success",
            static error => error.Detail);

        // Assert
        Assert.Equal("success", matched);
    }

    [Fact]
    public void Returns_the_failure_branch_value_for_non_generic_results()
    {
        // Arrange
        var result = Result.Error("Unexpected failure");

        // Act
        var matched = result.Match(
            static () => "success",
            static error => error.Detail);

        // Assert
        Assert.Equal("Unexpected failure", matched);
    }

    [Fact]
    public void Throws_for_an_uninitialized_non_generic_result()
    {
        // Arrange
        var result = default(Result);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => result.Match(static () => "success", static error => error.Detail));

        // Assert
        Assert.Equal("Result status is not initialized.", exception.Message);
    }

    [Fact]
    public void Throws_for_an_uninitialized_generic_result()
    {
        // Arrange
        var result = default(Result<string>);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => result.Match(static value => value.Length, static error => error.Detail.Length));

        // Assert
        Assert.Equal("Result status is not initialized.", exception.Message);
    }
}
