namespace SharedKernel.Functional.Tests;

[Trait(TestTraits.CapabilityName, TestTraits.ResultCapability)]
[Trait(TestTraits.CategoryName, TestTraits.CompositionCategory)]
public sealed class ResultCompositionTests
{
    [Fact]
    public void Discards_The_Value_And_Preserves_Success_Status()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var converted = result.ToResult();

        // Assert
        Assert.True(converted.IsSuccess);
        Assert.Equal(ResultStatus.Ok, converted.Status);
        Assert.Null(converted.ErrorDetails);
    }

    [Fact]
    public void Preserves_Created_Status_As_Success()
    {
        // Arrange
        var result = Result.Created("porto");

        // Act
        var converted = result.ToResult();

        // Assert
        Assert.True(converted.IsSuccess);
        Assert.Equal(ResultStatus.Created, converted.Status);
        Assert.Null(converted.ErrorDetails);
    }

    [Fact]
    public void Discards_The_Value_And_Preserves_Failure_Error()
    {
        // Arrange
        var result = Result.Conflict<string>("Tour is already published");

        // Act
        var converted = result.ToResult();

        // Assert
        Assert.True(converted.IsFailure);
        Assert.Equal(ResultStatus.Conflict, converted.Status);
        Assert.True(converted.TryGetError(out var error));
        Assert.Equal("Tour is already published", error.Detail);
    }

    [Fact]
    public void Transforms_The_Success_Value()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var mapped = result.Map(static value => value.Length);

        // Assert
        Assert.True(mapped.IsSuccess);
        Assert.Equal(5, mapped.Value);
    }

    [Fact]
    public void Preserves_Failure_Details()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var mapped = result.Map(static value => value.Length);

        // Assert
        Assert.True(mapped.IsFailure);
        Assert.True(mapped.TryGetError(out var error));
        Assert.Equal("Unexpected failure", error.Detail);
    }

    [Fact]
    public void Flattens_Successful_Results()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var bound = result.Bind(static value => Result.Ok(value.Length));

        // Assert
        Assert.True(bound.IsSuccess);
        Assert.Equal(5, bound.Value);
    }

    [Fact]
    public void Short_Circuits_Failures()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var bound = result.Bind(static value => Result.Ok(value.Length));

        // Assert
        Assert.True(bound.IsFailure);
        Assert.True(bound.TryGetError(out var error));
        Assert.Equal("Unexpected failure", error.Detail);
    }
}
