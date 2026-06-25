namespace SharedKernel.Functional.Tests;

[Trait(TestTraits.CapabilityName, TestTraits.ResultCapability)]
[Trait(TestTraits.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class ValidationErrorsTests
{
    [Fact]
    public void Adds_Invalid_Results_And_Reports_HasErrors()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        errors.Add(Result.Invalid("Validation failed", "Name", "Name is required"));

        // Assert
        Assert.True(errors.HasErrors);
    }

    [Fact]
    public void Adds_Generic_Invalid_Results_And_Reports_HasErrors()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        errors.Add(Result.Invalid<int>("Validation failed", "Age", "Age must be positive"));

        // Assert
        Assert.True(errors.HasErrors);
    }

    [Fact]
    public void Merges_Multiple_Errors_Into_A_Single_Result()
    {
        // Arrange
        var errors = new ValidationErrors();
        errors.Add(Result.Invalid("Validation failed", "Name", "Name is required"));
        errors.Add(Result.Invalid("Validation failed", "Email", "Email is invalid"));

        // Act
        var result = errors.ToResult();

        // Assert
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var error = result.ErrorDetails;
        Assert.NotNull(error);
        Assert.Equal(ResultErrorCodes.Invalid, error.Code);
        Assert.Equal("Multiple validation errors occurred.", error.Detail);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(["Name is required"], error.ValidationErrors["Name"]);
        Assert.Equal(["Email is invalid"], error.ValidationErrors["Email"]);
    }

    [Fact]
    public void Merges_Multiple_Errors_For_The_Same_Field()
    {
        // Arrange
        var errors = new ValidationErrors();
        errors.Add(Result.Invalid("Validation failed", "Name", "Name is required"));
        errors.Add(Result.Invalid("Validation failed", "Name", "Name must be at least 3 characters"));

        // Act
        var result = errors.ToResult();

        // Assert
        var error = result.ErrorDetails;
        Assert.NotNull(error);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(
            ["Name is required", "Name must be at least 3 characters"],
            error.ValidationErrors["Name"]);
    }

    [Fact]
    public void Converts_A_Single_Error_To_A_Generic_Result()
    {
        // Arrange
        var errors = new ValidationErrors();
        errors.Add(Result.Invalid("Validation failed", "Age", "Age must be positive"));

        // Act
        var result = errors.ToResult<int>();

        // Assert
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var error = result.ErrorDetails;
        Assert.NotNull(error);
        Assert.Equal(ResultErrorCodes.Invalid, error.Code);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(["Age must be positive"], error.ValidationErrors["Age"]);
    }

    [Fact]
    public void Throws_When_Converting_An_Empty_Collection()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => errors.ToResult());

        // Assert
        Assert.Contains("Cannot create result from empty error collection", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_Non_Invalid_Results()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => errors.Add(Result.Ok()));

        // Assert
        Assert.Contains("Only validation errors can be added", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_Non_Invalid_Generic_Results()
    {
        // Arrange
        var errors = new ValidationErrors();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => errors.Add(Result.Ok(42)));

        // Assert
        Assert.Contains("Only validation errors can be added", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Returns_A_Single_Non_Generic_Result_Unchanged()
    {
        // Arrange
        var errors = new ValidationErrors();
        var original = Result.Invalid("Validation failed", "Name", "Name is required");
        errors.Add(original);

        // Act
        var result = errors.ToResult();

        // Assert
        Assert.Equal(original, result);
    }

    [Fact]
    public void Throws_When_A_Single_Invalid_Result_Lacks_Error_Details()
    {
        // Arrange
        var errors = new ValidationErrors();
        errors.Add(ValidationErrorsTestsHelpers.CreateMalformedInvalidResult(error: null));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => errors.ToResult<int>());

        // Assert
        Assert.Equal("Validation errors must include error details.", exception.Message);
    }

    [Fact]
    public void Throws_When_A_Single_Invalid_Result_Lacks_Validation_Details()
    {
        // Arrange
        var errors = new ValidationErrors();
        errors.Add(ValidationErrorsTestsHelpers.CreateMalformedInvalidResult(new ResultError("Validation failed", ResultErrorCodes.Invalid)));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => errors.ToResult<int>());

        // Assert
        Assert.Equal("Validation errors must include field details.", exception.Message);
    }

    private static class ValidationErrorsTestsHelpers
    {
        public static Result CreateMalformedInvalidResult(ResultError? error)
        {
            var constructor = typeof(Result).GetConstructor(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                types: [typeof(ResultStatus), typeof(ResultError)],
                modifiers: null);

            Assert.NotNull(constructor);
            return (Result)constructor.Invoke([ResultStatus.Invalid, error]);
        }
    }
}
