namespace SharedKernel.Functional.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.EdgeCaseCategory)]
public sealed class ResultEdgeCaseTests
{
    [Fact]
    public void Throws_when_the_value_is_accessed_on_a_failed_result()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var exception = ((Action)(() =>
            {
                _ = result.Value;
            })).ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("failed result", StringComparison.Ordinal);
    }

    [Fact]
    public void Returns_false_for_failed_generic_results()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var hasValue = result.TryGetValue(out var value);

        // Assert
        hasValue.ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void Throws_when_ok_gets_a_null_reference()
    {
        // Arrange
        // Act
        var exception = ((Func<object?>)(() => Result.Ok(NullArgumentData.String()))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Throws_when_created_gets_a_null_reference()
    {
        // Arrange
        // Act
        var exception = ((Func<object?>)(() => Result.Created(NullArgumentData.String()))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Throws_when_accepted_gets_a_null_reference()
    {
        // Arrange
        // Act
        var exception = ((Func<object?>)(() => Result.Accepted(NullArgumentData.String()))).ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Returns_a_useful_string_for_a_successful_non_generic_result()
    {
        // Arrange
        var result = Result.Accepted();

        // Act
        var text = result.ToString();

        // Assert
        text.ShouldBe("Success: Accepted");
    }

    [Fact]
    public void Returns_a_useful_string_for_a_failed_non_generic_result()
    {
        // Arrange
        var result = Result.Error("Unexpected failure");

        // Act
        var text = result.ToString();

        // Assert
        text.ShouldBe("Failure: Error - Unexpected failure");
    }

    [Fact]
    public void Returns_a_useful_string_for_a_successful_generic_result()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var text = result.ToString();

        // Assert
        text.ShouldBe("Success: Ok - porto");
    }

    [Fact]
    public void Returns_a_useful_string_for_a_failed_generic_result()
    {
        // Arrange
        var result = Result.NotFound<string>("Tour not found");

        // Act
        var text = result.ToString();

        // Assert
        text.ShouldBe("Failure: NotFound - Tour not found");
    }

    [Fact]
    public void Returns_an_unknown_string_for_an_uninitialized_non_generic_result()
    {
        // Arrange
        var result = default(Result);

        // Act
        var text = result.ToString();

        // Assert
        text.ShouldBe("Unknown: Unknown");
    }

    [Fact]
    public void Returns_an_unknown_string_for_an_uninitialized_generic_result()
    {
        // Arrange
        var result = default(Result<string>);

        // Act
        var text = result.ToString();

        // Assert
        text.ShouldBe("Unknown: Unknown");
    }

    [Fact]
    public void Returns_a_useful_string_for_a_successful_generic_result_with_a_reference_type_value()
    {
        // Arrange
        var result = Result.Ok(new LoggedTourSummary("VT-42", "Porto river ride"));

        // Act
        var text = result.ToString();

        // Assert
        text.ShouldBe("Success: Ok - VT-42 | Porto river ride");
    }

    [Fact]
    public void Throws_when_a_malformed_successful_result_lacks_a_value()
    {
        // Arrange
        var result = ResultEdgeCaseTestsHelpers.CreateMalformedGenericResult(ResultStatus.Ok, value: null, error: null);

        // Act
        var exception = ((Func<object?>)(() => result.TryGetValue(out _))).ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Successful results must contain a value.");
    }

    [Fact]
    public void Throws_when_a_malformed_failed_result_lacks_error_details()
    {
        // Arrange
        var result = ResultEdgeCaseTestsHelpers.CreateMalformedNonGenericResult(ResultStatus.Error, error: null);

        // Act
        var exception = ((Func<object?>)(() => result.TryGetError(out _))).ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Failed results must contain error details.");
    }

    [Fact]
    public void Rejects_ensure_invalid_error_without_validation_details()
    {
        // Arrange
        var result = Result.Ok("porto");
        var error = new ResultError("Validation failed", ResultErrorCodes.Invalid);

        // Act
        var exception = ((Func<object?>)(() => result.Ensure(static _ => false, error))).ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldContain("Validation errors must include field details.", StringComparison.Ordinal);
        exception.ParamName.ShouldBe("error");
    }

    [Fact]
    public async Task Rejects_ensure_invalid_error_without_validation_details_for_Task_predicates()
    {
        // Arrange
        var result = Result.Ok("porto");
        var error = new ResultError("Validation failed", ResultErrorCodes.Invalid);

        // Act
        var exception = await ((Func<Task>)(() => result.Ensure(static _ => Task.FromResult(false), error))).ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldContain("Validation errors must include field details.", StringComparison.Ordinal);
        exception.ParamName.ShouldBe("error");
    }

    [Fact]
    public async Task Rejects_ensure_invalid_error_without_validation_details_for_ValueTask_predicates()
    {
        // Arrange
        var result = Result.Ok("porto");
        var error = new ResultError("Validation failed", ResultErrorCodes.Invalid);

        // Act
        var exception = await ((Func<Task>)(() => result.Ensure(static _ => ValueTask.FromResult(false), error).AsTask())).ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldContain("Validation errors must include field details.", StringComparison.Ordinal);
        exception.ParamName.ShouldBe("error");
    }

}
