namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class ValidationErrorsTests
{
    [Fact]
    public void Adds_invalid_results_and_reports_haserrors()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        errors.Add(Result.Invalid("Validation failed", "Name", "Name is required"));

        // Assert
        errors.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void Adds_generic_invalid_results_and_reports_haserrors()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        errors.Add(Result.Invalid<int>("Validation failed", "Age", "Age must be positive"));

        // Assert
        errors.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void Merges_multiple_errors_into_a_single_result()
    {
        // Arrange
        var errors = new ValidationErrors();
        errors.Add(Result.Invalid("Validation failed", "Name", "Name is required"));
        errors.Add(Result.Invalid("Validation failed", "Email", "Email is invalid"));

        // Act
        var result = errors.ToResult();

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var error = result.ErrorDetails;
        error.ShouldNotBeNull();
        error.Code.ShouldBe(ResultErrorCodes.Invalid);
        error.Detail.ShouldBe("Multiple validation errors occurred.");
        error.ValidationErrors.ShouldNotBeNull();
        TestAssert.Equal(["Name is required"], error.ValidationErrors["Name"]);
        TestAssert.Equal(["Email is invalid"], error.ValidationErrors["Email"]);
    }

    [Fact]
    public void Merges_multiple_errors_for_the_same_field()
    {
        // Arrange
        var errors = new ValidationErrors();
        errors.Add(Result.Invalid("Validation failed", "Name", "Name is required"));
        errors.Add(Result.Invalid("Validation failed", "Name", "Name must be at least 3 characters"));

        // Act
        var result = errors.ToResult();

        // Assert
        var error = result.ErrorDetails;
        error.ShouldNotBeNull();
        error.ValidationErrors.ShouldNotBeNull();
        TestAssert.Equal(
            ["Name is required", "Name must be at least 3 characters"],
            error.ValidationErrors["Name"]);
    }

    [Fact]
    public void Converts_a_single_error_to_a_generic_result()
    {
        // Arrange
        var errors = new ValidationErrors();
        errors.Add(Result.Invalid("Validation failed", "Age", "Age must be positive"));

        // Act
        var result = errors.ToResult<int>();

        // Assert
        result.Status.ShouldBe(ResultStatus.Invalid);
        var error = result.ErrorDetails;
        error.ShouldNotBeNull();
        error.Code.ShouldBe(ResultErrorCodes.Invalid);
        error.ValidationErrors.ShouldNotBeNull();
        TestAssert.Equal(["Age must be positive"], error.ValidationErrors["Age"]);
    }

    [Fact]
    public void Throws_when_converting_an_empty_collection()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var exception = TestAssert.Throws<InvalidOperationException>(() => errors.ToResult());

        // Assert
        exception.Message.ShouldContain("Cannot create result from empty error collection", StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_non_invalid_results()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var exception = ((Action)(() => errors.Add(Result.Ok()))).ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("Only validation errors can be added", StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_non_invalid_generic_results()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var exception = ((Action)(() => errors.Add(Result.Ok(42)))).ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("Only validation errors can be added", StringComparison.Ordinal);
    }

    [Fact]
    public void Returns_a_single_non_generic_result_unchanged()
    {
        // Arrange
        var errors = new ValidationErrors();
        var original = Result.Invalid("Validation failed", "Name", "Name is required");
        errors.Add(original);

        // Act
        var result = errors.ToResult();

        // Assert
        result.ShouldBe(original);
    }

    [Fact]
    public void Throws_when_a_single_invalid_result_lacks_error_details()
    {
        // Arrange
        var errors = new ValidationErrors();
        errors.Add(ValidationErrorsTestsHelpers.CreateMalformedInvalidResult(error: null));

        // Act
        var exception = TestAssert.Throws<InvalidOperationException>(() => errors.ToResult<int>());

        // Assert
        exception.Message.ShouldBe("Validation errors must include error details.");
    }

    [Fact]
    public void Throws_when_a_single_invalid_result_lacks_validation_details()
    {
        // Arrange
        var errors = new ValidationErrors();
        errors.Add(ValidationErrorsTestsHelpers.CreateMalformedInvalidResult(new ResultError("Validation failed", ResultErrorCodes.Invalid)));

        // Act
        var exception = TestAssert.Throws<InvalidOperationException>(() => errors.ToResult<int>());

        // Assert
        exception.Message.ShouldBe("Validation errors must include field details.");
    }

}
