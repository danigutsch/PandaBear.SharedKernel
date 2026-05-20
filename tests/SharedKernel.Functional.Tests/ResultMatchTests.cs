namespace SharedKernel.Functional.Tests;

[Trait(TestTraits.CapabilityName, TestTraits.ResultCapability)]
[Trait(TestTraits.CategoryName, TestTraits.CompositionCategory)]
[Trait(TestTraits.TheoryName, TestTraits.MatchSemanticsTheory)]
public sealed class ResultMatchTests
{
    [Fact]
    public void Returns_The_Success_Branch_Value_For_Generic_Results()
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
    public void Returns_The_Failure_Branch_Value_For_Generic_Results()
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
    public void Returns_The_Success_Branch_Value_For_Non_Generic_Results()
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
    public void Returns_The_Failure_Branch_Value_For_Non_Generic_Results()
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
    public void Throws_For_An_Uninitialized_Non_Generic_Result()
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
    public void Throws_For_An_Uninitialized_Generic_Result()
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
