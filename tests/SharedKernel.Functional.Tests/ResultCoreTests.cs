namespace SharedKernel.Functional.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class ResultCoreTests
{
    [Fact]
    public void Creates_a_successful_result()
    {
        // Arrange
        // Act
        var result = Result.Ok();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Status.ShouldBe(ResultStatus.Ok);
        result.ErrorDetails.ShouldBeNull();
    }

    [Fact]
    public void Creates_a_failed_result_with_error_details()
    {
        // Arrange
        // Act
        var result = Result.Invalid("Validation failed", "name", "Name is required");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.IsSuccess.ShouldBeFalse();
        result.Status.ShouldBe(ResultStatus.Invalid);
        result.TryGetError(out var error).ShouldBeTrue();
        error.ShouldNotBeNull();
        error.Code.ShouldBe(ResultErrorCodes.Invalid);
        error.Detail.ShouldBe("Validation failed");
        error.ValidationErrors.ShouldNotBeNull();
        TestAssert.Equal(["Name is required"], error.ValidationErrors["name"]);
    }

    [Fact]
    public void Creates_a_failed_result_with_multiple_validation_errors()
    {
        // Arrange
        var validationErrors = new Dictionary<string, string[]>
        {
            ["name"] = ["Name is required"],
            ["email"] = ["Email is invalid"],
        };

        // Act
        var result = Result.Invalid("Validation failed", validationErrors);

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var error = result.ErrorDetails;
        error.ShouldNotBeNull();
        error.Code.ShouldBe(ResultErrorCodes.Invalid);
        error.ValidationErrors.ShouldNotBeNull();
        TestAssert.Equal(["Name is required"], error.ValidationErrors["name"]);
        TestAssert.Equal(["Email is invalid"], error.ValidationErrors["email"]);
    }

    [Fact]
    public void Creates_a_successful_result_with_a_value()
    {
        // Arrange
        // Act
        var result = Result.Ok("porto");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Status.ShouldBe(ResultStatus.Ok);
        result.Value.ShouldBe("porto");
        result.ErrorDetails.ShouldBeNull();
    }

    [Fact]
    public void Creates_a_successful_non_generic_result()
    {
        // Arrange
        // Act
        var result = Result.Created("porto").ToResult();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Status.ShouldBe(ResultStatus.Created);
        result.ErrorDetails.ShouldBeNull();
    }

    [Fact]
    public void Returns_true_for_successful_generic_results()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var hasValue = result.TryGetValue(out var value);

        // Assert
        hasValue.ShouldBeTrue();
        value.ShouldBe("porto");
    }

    [Fact]
    public void Creates_a_failed_result_without_a_value()
    {
        // Arrange
        // Act
        var result = Result.Error<string>("Unexpected failure");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Status.ShouldBe(ResultStatus.Error);
        result.TryGetError(out var error).ShouldBeTrue();
        error.ShouldNotBeNull();
        error.Code.ShouldBe(ResultErrorCodes.Error);
        error.Detail.ShouldBe("Unexpected failure");
    }

    [Fact]
    public void Returns_false_for_successful_results()
    {
        // Arrange
        var result = Result.Ok();

        // Act
        var hasError = result.TryGetError(out var error);

        // Assert
        hasError.ShouldBeFalse();
        error.ShouldBeNull();
    }

    [Fact]
    public void Returns_true_for_failed_generic_results()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var hasError = result.TryGetError(out var error);

        // Assert
        hasError.ShouldBeTrue();
        error.ShouldNotBeNull();
        error.Code.ShouldBe(ResultErrorCodes.Error);
        error.Detail.ShouldBe("Unexpected failure");
    }

    [Fact]
    public void Returns_false_for_an_uninitialized_non_generic_result()
    {
        // Arrange
        var result = default(Result);

        // Act
        var hasError = result.TryGetError(out var error);

        // Assert
        hasError.ShouldBeFalse();
        error.ShouldBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeFalse();
    }

    [Fact]
    public void Returns_false_for_an_uninitialized_generic_result()
    {
        // Arrange
        var result = default(Result<string>);

        // Act
        var hasValue = result.TryGetValue(out var value);
        var hasError = result.TryGetError(out var error);

        // Assert
        hasValue.ShouldBeFalse();
        value.ShouldBeNull();
        hasError.ShouldBeFalse();
        error.ShouldBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_empty_validation_error_dictionaries()
    {
        // Arrange
        var validationErrors = new Dictionary<string, string[]>();

        // Act
        var exception = TestAssert.Throws<ArgumentOutOfRangeException>(() => Result.Invalid("Validation failed", validationErrors));

        // Assert
        exception.ParamName.ShouldBe("validationErrors");
    }

    [Fact]
    public void Rejects_validation_dictionaries_with_empty_field_names()
    {
        // Arrange
        var validationErrors = new Dictionary<string, string[]>
        {
            [string.Empty] = ["Name is required"],
        };

        // Act
        var exception = TestAssert.Throws<ArgumentException>(() => Result.Invalid("Validation failed", validationErrors));

        // Assert
        exception.ParamName.ShouldBe("field");
    }

    [Fact]
    public void Rejects_validation_dictionaries_with_empty_message_arrays()
    {
        // Arrange
        var validationErrors = new Dictionary<string, string[]>
        {
            ["Name"] = [],
        };

        // Act
        var exception = TestAssert.Throws<ArgumentOutOfRangeException>(() => Result.Invalid("Validation failed", validationErrors));

        // Assert
        exception.ParamName.ShouldBe("validationErrors");
    }

    [Fact]
    public void Rejects_validation_dictionaries_with_null_message_entries()
    {
        // Arrange
        var validationErrors = new Dictionary<string, string[]>
        {
            ["Name"] = NullArgumentData.StringArray(),
        };

        // Act
        var exception = TestAssert.Throws<ArgumentNullException>(() => Result.Invalid("Validation failed", validationErrors));

        // Assert
        exception.ParamName.ShouldBe("messages");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_null_or_whitespace_error_detail(string? detail)
    {
        // Arrange
        // Act
        var exception = Record.Exception(() => Result.Error(detail ?? NullArgumentData.String()));

        // Assert
        exception.ShouldNotBeNull();
        var argumentException = exception.ShouldBeAssignableTo<ArgumentException>();
        argumentException.ParamName.ShouldBe("detail");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_null_or_whitespace_validation_field(string? field)
    {
        // Arrange
        // Act
        var exception = Record.Exception(() => Result.Invalid("Validation failed", field ?? NullArgumentData.String(), "Name is required"));

        // Assert
        exception.ShouldNotBeNull();
        var argumentException = exception.ShouldBeAssignableTo<ArgumentException>();
        argumentException.ParamName.ShouldBe("field");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_null_or_whitespace_validation_message(string? message)
    {
        // Arrange
        // Act
        var exception = Record.Exception(() => Result.Invalid("Validation failed", "Name", message ?? NullArgumentData.String()));

        // Assert
        exception.ShouldNotBeNull();
        var argumentException = exception.ShouldBeAssignableTo<ArgumentException>();
        argumentException.ParamName.ShouldBe("message");
    }
}
