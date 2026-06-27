namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.EdgeCaseCategory)]
public sealed class ResultEdgeCaseTests
{
    [Fact]
    public void Throws_when_the_value_is_accessed_on_a_failed_result()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () =>
            {
                _ = result.Value;
            });

        // Assert
        Assert.Contains("failed result", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Returns_false_for_failed_generic_results()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var hasValue = result.TryGetValue(out var value);

        // Assert
        Assert.False(hasValue);
        Assert.Null(value);
    }

    [Fact]
    public void Throws_when_ok_gets_a_null_reference()
    {
        // Arrange
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => Result.Ok(NullArgumentData.String()));

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Throws_when_created_gets_a_null_reference()
    {
        // Arrange
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => Result.Created(NullArgumentData.String()));

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Throws_when_accepted_gets_a_null_reference()
    {
        // Arrange
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => Result.Accepted(NullArgumentData.String()));

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Returns_a_useful_string_for_a_successful_non_generic_result()
    {
        // Arrange
        var result = Result.Accepted();

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Success: Accepted", text);
    }

    [Fact]
    public void Returns_a_useful_string_for_a_failed_non_generic_result()
    {
        // Arrange
        var result = Result.Error("Unexpected failure");

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Failure: Error - Unexpected failure", text);
    }

    [Fact]
    public void Returns_a_useful_string_for_a_successful_generic_result()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Success: Ok - porto", text);
    }

    [Fact]
    public void Returns_a_useful_string_for_a_failed_generic_result()
    {
        // Arrange
        var result = Result.NotFound<string>("Tour not found");

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Failure: NotFound - Tour not found", text);
    }

    [Fact]
    public void Returns_an_unknown_string_for_an_uninitialized_non_generic_result()
    {
        // Arrange
        var result = default(Result);

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Unknown: Unknown", text);
    }

    [Fact]
    public void Returns_an_unknown_string_for_an_uninitialized_generic_result()
    {
        // Arrange
        var result = default(Result<string>);

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Unknown: Unknown", text);
    }

    [Fact]
    public void Returns_a_useful_string_for_a_successful_generic_result_with_a_reference_type_value()
    {
        // Arrange
        var result = Result.Ok(new LoggedTourSummary("VT-42", "Porto river ride"));

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Success: Ok - VT-42 | Porto river ride", text);
    }

    [Fact]
    public void Throws_when_a_malformed_successful_result_lacks_a_value()
    {
        // Arrange
        var result = ResultEdgeCaseTestsHelpers.CreateMalformedGenericResult(ResultStatus.Ok, value: null, error: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => result.TryGetValue(out _));

        // Assert
        Assert.Equal("Successful results must contain a value.", exception.Message);
    }

    [Fact]
    public void Throws_when_a_malformed_failed_result_lacks_error_details()
    {
        // Arrange
        var result = ResultEdgeCaseTestsHelpers.CreateMalformedNonGenericResult(ResultStatus.Error, error: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => result.TryGetError(out _));

        // Assert
        Assert.Equal("Failed results must contain error details.", exception.Message);
    }

    [Fact]
    public void Rejects_ensure_invalid_error_without_validation_details()
    {
        // Arrange
        var result = Result.Ok("porto");
        var error = new ResultError("Validation failed", ResultErrorCodes.Invalid);

        // Act
        var exception = Assert.Throws<ArgumentException>(() => result.Ensure(static _ => false, error));

        // Assert
        Assert.Contains("Validation errors must include field details.", exception.Message, StringComparison.Ordinal);
        Assert.Equal("error", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_ensure_invalid_error_without_validation_details_for_task_predicates()
    {
        // Arrange
        var result = Result.Ok("porto");
        var error = new ResultError("Validation failed", ResultErrorCodes.Invalid);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => result.Ensure(static _ => Task.FromResult(false), error));

        // Assert
        Assert.Contains("Validation errors must include field details.", exception.Message, StringComparison.Ordinal);
        Assert.Equal("error", exception.ParamName);
    }

    [Fact]
    public async Task Rejects_ensure_invalid_error_without_validation_details_for_valueTask_predicates()
    {
        // Arrange
        var result = Result.Ok("porto");
        var error = new ResultError("Validation failed", ResultErrorCodes.Invalid);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => result.Ensure(static _ => ValueTask.FromResult(false), error).AsTask());

        // Assert
        Assert.Contains("Validation errors must include field details.", exception.Message, StringComparison.Ordinal);
        Assert.Equal("error", exception.ParamName);
    }

}
