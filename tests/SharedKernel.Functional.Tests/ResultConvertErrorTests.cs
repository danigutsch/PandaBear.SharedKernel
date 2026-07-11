namespace SharedKernel.Functional.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CompositionCategory)]
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
        converted.Status.ShouldBe(ResultStatus.NotFound);
        var error = converted.ErrorDetails;
        error.ShouldNotBeNull();
        error.Code.ShouldBe(ResultErrorCodes.NotFound);
        error.Detail.ShouldBe("Tour not found");
    }

    [Fact]
    public void Converts_a_generic_failure_to_a_non_generic_failure()
    {
        // Arrange
        var source = Result.Conflict<string>("Tour already exists");

        // Act
        var converted = source.ConvertError();

        // Assert
        converted.Status.ShouldBe(ResultStatus.Conflict);
        var error = converted.ErrorDetails;
        error.ShouldNotBeNull();
        error.Code.ShouldBe(ResultErrorCodes.Conflict);
        error.Detail.ShouldBe("Tour already exists");
    }

    [Fact]
    public void Converts_a_generic_failure_to_another_generic_failure()
    {
        // Arrange
        var source = Result.Error<string>("Unexpected failure");

        // Act
        var converted = source.ConvertError<string, int>();

        // Assert
        converted.Status.ShouldBe(ResultStatus.Error);
        var error = converted.ErrorDetails;
        error.ShouldNotBeNull();
        error.Code.ShouldBe(ResultErrorCodes.Error);
        error.Detail.ShouldBe("Unexpected failure");
    }

    [Fact]
    public void Preserves_validation_errors_when_converting_invalid_results()
    {
        // Arrange
        var source = Result.Invalid("Validation failed", "Name", "Name is required");

        // Act
        var converted = source.ConvertError<string>();

        // Assert
        converted.Status.ShouldBe(ResultStatus.Invalid);
        var error = converted.ErrorDetails;
        error.ShouldNotBeNull();
        error.Code.ShouldBe(ResultErrorCodes.Invalid);
        error.ValidationErrors.ShouldNotBeNull();
        TestAssert.Equal(["Name is required"], error.ValidationErrors["Name"]);
    }

    [Fact]
    public void Throws_when_converting_an_invalid_result_without_validation_payload()
    {
        // Arrange
        var malformedResult = ResultConvertErrorTestsHelpers.CreateMalformedResult(
            ResultStatus.Invalid,
            new ResultError("Validation failed", ResultErrorCodes.Invalid));

        // Act
        var exception = TestAssert.Throws<InvalidOperationException>(() => malformedResult.ConvertError<string>());

        // Assert
        exception.Message.ShouldBe("Validation errors must include field details.");
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
        var exception = TestAssert.Throws<InvalidOperationException>(() => malformedResult.ConvertError<string>());

        // Assert
        exception.Message.ShouldBe(expectedMessage);
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
        var exception = TestAssert.Throws<InvalidOperationException>(() => malformedResult.ConvertError<string, int>());

        // Assert
        exception.Message.ShouldBe(expectedMessage);
    }

}
