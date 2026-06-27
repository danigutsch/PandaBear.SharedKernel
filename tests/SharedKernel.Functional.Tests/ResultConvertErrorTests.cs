namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
public sealed class ResultConvertErrorTests
{
    [Fact]
    public void Converts_a_non_generic_failure_to_a_generic_failure()
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
    public void Converts_a_generic_failure_to_a_non_generic_failure()
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
    public void Converts_a_generic_failure_to_another_generic_failure()
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
    public void Preserves_validation_errors_when_converting_invalid_results()
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
    public void Throws_when_converting_an_invalid_result_without_validation_payload()
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
    public void Throws_expected_exception_for_malformed_non_generic_status(int statusValue, string expectedMessage)
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
    public void Throws_expected_exception_for_malformed_generic_status(int statusValue, string expectedMessage)
    {
        // Arrange
        var malformedResult = ResultConvertErrorTestsHelpers.CreateMalformedGenericResult((ResultStatus)statusValue, "payload", new ResultError("Malformed result status."));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => malformedResult.ConvertError<string, int>());

        // Assert
        Assert.Equal(expectedMessage, exception.Message);
    }

}
