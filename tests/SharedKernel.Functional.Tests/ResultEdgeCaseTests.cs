namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.EdgeCaseCategory)]
public sealed class ResultEdgeCaseTests
{
    [Fact]
    public void Throws_When_The_Value_Is_Accessed_On_A_Failed_Result()
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
    public void Returns_False_For_Failed_Generic_Results()
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
    public void Throws_When_Ok_Gets_A_Null_Reference()
    {
        // Arrange
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => Result.Ok(NullArgumentData.String()));

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Throws_When_Created_Gets_A_Null_Reference()
    {
        // Arrange
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => Result.Created(NullArgumentData.String()));

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Throws_When_Accepted_Gets_A_Null_Reference()
    {
        // Arrange
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => Result.Accepted(NullArgumentData.String()));

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Returns_A_Useful_String_For_A_Successful_Non_Generic_Result()
    {
        // Arrange
        var result = Result.Accepted();

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Success: Accepted", text);
    }

    [Fact]
    public void Returns_A_Useful_String_For_A_Failed_Non_Generic_Result()
    {
        // Arrange
        var result = Result.Error("Unexpected failure");

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Failure: Error - Unexpected failure", text);
    }

    [Fact]
    public void Returns_A_Useful_String_For_A_Successful_Generic_Result()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Success: Ok - porto", text);
    }

    [Fact]
    public void Returns_A_Useful_String_For_A_Failed_Generic_Result()
    {
        // Arrange
        var result = Result.NotFound<string>("Tour not found");

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Failure: NotFound - Tour not found", text);
    }

    [Fact]
    public void Returns_An_Unknown_String_For_An_Uninitialized_Non_Generic_Result()
    {
        // Arrange
        var result = default(Result);

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Unknown: Unknown", text);
    }

    [Fact]
    public void Returns_An_Unknown_String_For_An_Uninitialized_Generic_Result()
    {
        // Arrange
        var result = default(Result<string>);

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Unknown: Unknown", text);
    }

    [Fact]
    public void Returns_A_Useful_String_For_A_Successful_Generic_Result_With_A_Reference_Type_Value()
    {
        // Arrange
        var result = Result.Ok(new LoggedTourSummary("VT-42", "Porto river ride"));

        // Act
        var text = result.ToString();

        // Assert
        Assert.Equal("Success: Ok - VT-42 | Porto river ride", text);
    }

    [Fact]
    public void Throws_When_A_Malformed_Successful_Result_Lacks_A_Value()
    {
        // Arrange
        var result = ResultEdgeCaseTestsHelpers.CreateMalformedGenericResult(ResultStatus.Ok, value: null, error: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => result.TryGetValue(out _));

        // Assert
        Assert.Equal("Successful results must contain a value.", exception.Message);
    }

    [Fact]
    public void Throws_When_A_Malformed_Failed_Result_Lacks_Error_Details()
    {
        // Arrange
        var result = ResultEdgeCaseTestsHelpers.CreateMalformedNonGenericResult(ResultStatus.Error, error: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => result.TryGetError(out _));

        // Assert
        Assert.Equal("Failed results must contain error details.", exception.Message);
    }

    [Fact]
    public void Rejects_Ensure_Invalid_Error_Without_Validation_Details()
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
    public async Task Rejects_Ensure_Invalid_Error_Without_Validation_Details_For_Task_Predicates()
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
    public async Task Rejects_Ensure_Invalid_Error_Without_Validation_Details_For_ValueTask_Predicates()
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
