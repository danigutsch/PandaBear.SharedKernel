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

    [Fact]
    public void Preserves_A_Success_When_Ensure_Predicate_Passes()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var ensured = result.Ensure(static value => value.Length == 5, new ResultError("Length mismatch"));

        // Assert
        Assert.True(ensured.IsSuccess);
        Assert.Equal("porto", ensured.Value);
    }

    [Fact]
    public void Returns_The_Provided_Error_When_Ensure_Predicate_Fails()
    {
        // Arrange
        var failure = new ResultError("Length mismatch", ResultErrorCodes.Error);
        var result = Result.Ok("porto");

        // Act
        var ensured = result.Ensure(static value => value.Length == 4, failure);

        // Assert
        Assert.True(ensured.IsFailure);
        Assert.True(ensured.TryGetError(out var error));
        Assert.NotNull(error);
        Assert.Equal(failure, error);
    }

    [Fact]
    public void Preserves_Invalid_Status_And_Validation_Payload_When_Ensure_Fails_With_A_Validation_Error()
    {
        // Arrange
        var failure = new ResultError(
            "Validation failed",
            ResultErrorCodes.Invalid,
            new Dictionary<string, string[]>
            {
                ["Name"] = ["Name is required"],
            });
        var result = Result.Ok("porto");

        // Act
        var ensured = result.Ensure(static value => value.Length == 4, failure);

        // Assert
        Assert.True(ensured.IsFailure);
        Assert.Equal(ResultStatus.Invalid, ensured.Status);
        Assert.True(ensured.TryGetError(out var error));
        Assert.NotNull(error);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(["Name is required"], error.ValidationErrors["Name"]);
    }

    [Fact]
    public void Short_Circuits_Ensure_For_Existing_Failures()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var ensured = result.Ensure(static _ => true, new ResultError("Should not be used"));

        // Assert
        Assert.True(ensured.IsFailure);
        Assert.True(ensured.TryGetError(out var error));
        Assert.NotNull(error);
        Assert.Equal("Unexpected failure", error.Detail);
    }
}
