namespace SharedKernel.Functional.Tests;

[Trait(TestTraits.CapabilityName, TestTraits.ResultCapability)]
[Trait(TestTraits.CategoryName, TestTraits.EdgeCaseCategory)]
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
        var exception = ReflectionTestHelpers.InvokeSingleParameterStaticGenericAndUnwrapArgumentNull(
            typeof(Result),
            nameof(Result.Ok),
            typeof(string),
            null);

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Throws_When_Created_Gets_A_Null_Reference()
    {
        // Arrange
        // Act
        var exception = ReflectionTestHelpers.InvokeSingleParameterStaticGenericAndUnwrapArgumentNull(
            typeof(Result),
            nameof(Result.Created),
            typeof(string),
            null);

        // Assert
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Throws_When_Accepted_Gets_A_Null_Reference()
    {
        // Arrange
        // Act
        var exception = ReflectionTestHelpers.InvokeSingleParameterStaticGenericAndUnwrapArgumentNull(
            typeof(Result),
            nameof(Result.Accepted),
            typeof(string),
            null);

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
        var result = CreateMalformedGenericResult(ResultStatus.Ok, value: null, error: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => result.TryGetValue(out _));

        // Assert
        Assert.Equal("Successful results must contain a value.", exception.Message);
    }

    [Fact]
    public void Throws_When_A_Malformed_Failed_Result_Lacks_Error_Details()
    {
        // Arrange
        var result = CreateMalformedNonGenericResult(ResultStatus.Error, error: null);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => result.TryGetError(out _));

        // Assert
        Assert.Equal("Failed results must contain error details.", exception.Message);
    }

    private sealed class LoggedTourSummary(string code, string title)
    {
        public override string ToString() => $"{code} | {title}";
    }

    private static Result<string> CreateMalformedGenericResult(ResultStatus status, string? value, ResultError? error)
    {
        var constructor = typeof(Result<string>).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ResultStatus), typeof(string), typeof(ResultError)],
            modifiers: null);

        Assert.NotNull(constructor);
        return (Result<string>)constructor.Invoke([status, value, error]);
    }

    private static Result CreateMalformedNonGenericResult(ResultStatus status, ResultError? error)
    {
        var constructor = typeof(Result).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ResultStatus), typeof(ResultError)],
            modifiers: null);

        Assert.NotNull(constructor);
        return (Result)constructor.Invoke([status, error]);
    }
}
