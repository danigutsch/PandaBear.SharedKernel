namespace SharedKernel.Functional.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
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
        converted.IsSuccess.ShouldBeTrue();
        converted.Status.ShouldBe(ResultStatus.Ok);
        converted.ErrorDetails.ShouldBeNull();
    }

    [Fact]
    public void Preserves_created_status_as_success()
    {
        // Arrange
        var result = Result.Created("porto");

        // Act
        var converted = result.ToResult();

        // Assert
        converted.IsSuccess.ShouldBeTrue();
        converted.Status.ShouldBe(ResultStatus.Created);
        converted.ErrorDetails.ShouldBeNull();
    }

    [Fact]
    public void Discards_the_value_and_preserves_failure_error()
    {
        // Arrange
        var result = Result.Conflict<string>("Tour is already published");

        // Act
        var converted = result.ToResult();

        // Assert
        converted.IsFailure.ShouldBeTrue();
        converted.Status.ShouldBe(ResultStatus.Conflict);
        converted.TryGetError(out var error).ShouldBeTrue();
        var nonNullError = TestAssert.NotNull(error);
        nonNullError.Detail.ShouldBe("Tour is already published");
    }

    [Fact]
    public void Transforms_the_success_value()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var mapped = result.Map(static value => value.Length);

        // Assert
        mapped.IsSuccess.ShouldBeTrue();
        mapped.Value.ShouldBe(5);
    }

    [Fact]
    public void Preserves_failure_details()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var mapped = result.Map(static value => value.Length);

        // Assert
        mapped.IsFailure.ShouldBeTrue();
        mapped.TryGetError(out var error).ShouldBeTrue();
        var nonNullError = TestAssert.NotNull(error);
        nonNullError.Detail.ShouldBe("Unexpected failure");
    }

    [Fact]
    public void Flattens_successful_results()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var bound = result.Bind(static value => Result.Ok(value.Length));

        // Assert
        bound.IsSuccess.ShouldBeTrue();
        bound.Value.ShouldBe(5);
    }

    [Fact]
    public void Short_circuits_failures()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var bound = result.Bind(static value => Result.Ok(value.Length));

        // Assert
        bound.IsFailure.ShouldBeTrue();
        bound.TryGetError(out var error).ShouldBeTrue();
        var nonNullError = TestAssert.NotNull(error);
        nonNullError.Detail.ShouldBe("Unexpected failure");
    }

    [Fact]
    public void Preserves_a_success_when_ensure_predicate_passes()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var ensured = result.Ensure(static value => value.Length == 5, new ResultError("Length mismatch"));

        // Assert
        ensured.IsSuccess.ShouldBeTrue();
        ensured.Value.ShouldBe("porto");
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
        ensured.IsFailure.ShouldBeTrue();
        ensured.TryGetError(out var error).ShouldBeTrue();
        error.ShouldNotBeNull();
        error.ShouldBe(failure);
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
        ensured.IsFailure.ShouldBeTrue();
        ensured.Status.ShouldBe(ResultStatus.Invalid);
        ensured.TryGetError(out var error).ShouldBeTrue();
        error.ShouldNotBeNull();
        error.ValidationErrors.ShouldNotBeNull();
        TestAssert.Equal(["Name is required"], error.ValidationErrors["Name"]);
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
        ensured.IsFailure.ShouldBeTrue();
        ensured.Status.ShouldBe(expectedStatus);
    }

    [Fact]
    public void Short_circuits_ensure_for_existing_failures()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var ensured = result.Ensure(static _ => true, new ResultError("Should not be used"));

        // Assert
        ensured.IsFailure.ShouldBeTrue();
        ensured.TryGetError(out var error).ShouldBeTrue();
        error.ShouldNotBeNull();
        error.Detail.ShouldBe("Unexpected failure");
    }
}
