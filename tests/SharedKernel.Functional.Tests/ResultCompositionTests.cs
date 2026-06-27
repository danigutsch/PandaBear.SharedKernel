namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
public sealed class ResultCompositionTests
{
    [Fact]
    public void Discards_the_value_and_preserves_success_status()
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
    public void Preserves_created_status_as_success()
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
    public void Discards_the_value_and_preserves_failure_error()
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
    public void Transforms_the_success_value()
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
    public void Preserves_failure_details()
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
    public void Flattens_successful_results()
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
    public void Short_circuits_failures()
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
    public void Preserves_a_success_when_ensure_predicate_passes()
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
    public void Returns_the_provided_error_when_ensure_predicate_fails()
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
    public void Preserves_invalid_status_and_validation_payload_when_ensure_fails_with_a_validation_error()
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

    [Theory]
    [InlineData(ResultErrorCodes.NotFound, ResultStatus.NotFound)]
    [InlineData(ResultErrorCodes.Unauthorized, ResultStatus.Unauthorized)]
    [InlineData(ResultErrorCodes.Forbidden, ResultStatus.Forbidden)]
    [InlineData(ResultErrorCodes.Conflict, ResultStatus.Conflict)]
    [InlineData(ResultErrorCodes.CriticalError, ResultStatus.CriticalError)]
    [InlineData(ResultErrorCodes.Unavailable, ResultStatus.Unavailable)]
    [InlineData(ResultErrorCodes.Error, ResultStatus.Error)]
    [InlineData("custom_error", ResultStatus.Error)]
    public void Maps_ensure_error_codes_to_the_expected_failure_status(string errorCode, ResultStatus expectedStatus)
    {
        // Arrange
        var result = Result.Ok("porto");
        var failure = new ResultError("Failure", errorCode);

        // Act
        var ensured = result.Ensure(static _ => false, failure);

        // Assert
        Assert.True(ensured.IsFailure);
        Assert.Equal(expectedStatus, ensured.Status);
    }

    [Fact]
    public void Short_circuits_ensure_for_existing_failures()
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
