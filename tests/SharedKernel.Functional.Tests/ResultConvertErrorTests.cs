using System.Reflection;

namespace SharedKernel.Functional.Tests;

[Trait(TestTraits.CapabilityName, TestTraits.ResultCapability)]
[Trait(TestTraits.CategoryName, TestTraits.CompositionCategory)]
public sealed class ResultConvertErrorTests
{
    [Fact]
    public void Converts_A_Non_Generic_Failure_To_A_Generic_Failure()
    {
        // Arrange
        var source = Result.NotFound("Tour not found");

        // Act
        var converted = source.ConvertError<string>();

        // Assert
        Assert.Equal(ResultStatus.NotFound, converted.Status);
        var error = converted.ErrorDetails;
        Assert.NotNull(error);
        Assert.Equal(ResultErrorCodes.NotFound, error.Code);
        Assert.Equal("Tour not found", error.Detail);
    }

    [Fact]
    public void Converts_A_Generic_Failure_To_A_Non_Generic_Failure()
    {
        // Arrange
        var source = Result.Conflict<string>("Tour already exists");

        // Act
        var converted = source.ConvertError();

        // Assert
        Assert.Equal(ResultStatus.Conflict, converted.Status);
        var error = converted.ErrorDetails;
        Assert.NotNull(error);
        Assert.Equal(ResultErrorCodes.Conflict, error.Code);
        Assert.Equal("Tour already exists", error.Detail);
    }

    [Fact]
    public void Converts_A_Generic_Failure_To_Another_Generic_Failure()
    {
        // Arrange
        var source = Result.Error<string>("Unexpected failure");

        // Act
        var converted = source.ConvertError<string, int>();

        // Assert
        Assert.Equal(ResultStatus.Error, converted.Status);
        var error = converted.ErrorDetails;
        Assert.NotNull(error);
        Assert.Equal(ResultErrorCodes.Error, error.Code);
        Assert.Equal("Unexpected failure", error.Detail);
    }

    [Fact]
    public void Preserves_Validation_Errors_When_Converting_Invalid_Results()
    {
        // Arrange
        var source = Result.Invalid("Validation failed", "Name", "Name is required");

        // Act
        var converted = source.ConvertError<string>();

        // Assert
        Assert.Equal(ResultStatus.Invalid, converted.Status);
        var error = converted.ErrorDetails;
        Assert.NotNull(error);
        Assert.Equal(ResultErrorCodes.Invalid, error.Code);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(["Name is required"], error.ValidationErrors["Name"]);
    }

    [Fact]
    public void Throws_When_Converting_An_Invalid_Result_Without_Validation_Payload()
    {
        // Arrange
        var malformedResult = ResultConvertErrorTestsHelpers.CreateMalformedResult(
            ResultStatus.Invalid,
            new ResultError("Validation failed", ResultErrorCodes.Invalid));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => malformedResult.ConvertError<string>());

        // Assert
        Assert.Equal("Validation errors must include field details.", exception.Message);
    }

    [Theory]
    [InlineData((int)ResultStatus.Created, "Cannot convert a successful result. Only failed results can be converted.")]
    [InlineData((int)ResultStatus.Unknown, "Unsupported result status: Unknown")]
    [InlineData(999, "Unsupported result status: 999")]
    public void Throws_Expected_Exception_For_Malformed_Non_Generic_Status(int statusValue, string expectedMessage)
    {
        // Arrange
        var malformedResult = ResultConvertErrorTestsHelpers.CreateMalformedResult((ResultStatus)statusValue, new ResultError("Malformed result status."));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => malformedResult.ConvertError<string>());

        // Assert
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData((int)ResultStatus.NoContent, "Cannot convert a successful result. Only failed results can be converted.")]
    [InlineData((int)ResultStatus.Unknown, "Unsupported result status: Unknown")]
    [InlineData(999, "Unsupported result status: 999")]
    public void Throws_Expected_Exception_For_Malformed_Generic_Status(int statusValue, string expectedMessage)
    {
        // Arrange
        var malformedResult = CreateMalformedGenericResult((ResultStatus)statusValue, "payload", new ResultError("Malformed result status."));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => malformedResult.ConvertError<string, int>());

        // Assert
        Assert.Equal(expectedMessage, exception.Message);
    }

    internal static Result<T> CreateMalformedGenericResult<T>(ResultStatus status, T value, ResultError? error)
        where T : notnull
    {
        var constructor = typeof(Result<T>).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(ResultStatus), typeof(T), typeof(ResultError)],
            modifiers: null);

        Assert.NotNull(constructor);
        return (Result<T>)constructor.Invoke([status, value, error]);
    }

    internal static class ResultConvertErrorTestsHelpers
    {
        public static Result CreateMalformedResult(ResultStatus status, ResultError? error)
        {
            var constructor = typeof(Result).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(ResultStatus), typeof(ResultError)],
                modifiers: null);

            Assert.NotNull(constructor);
            return (Result)constructor.Invoke([status, error]);
        }
    }
}
