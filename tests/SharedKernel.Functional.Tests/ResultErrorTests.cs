namespace SharedKernel.Functional.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class ResultErrorTests
{
    [Fact]
    public void Creates_an_error_with_detail_and_default_code()
    {
        // Arrange
        // Act
        var error = new ResultError("Something went wrong");

        // Assert
        error.Code.ShouldBe(ResultErrorCodes.Error);
        error.Detail.ShouldBe("Something went wrong");
        error.ValidationErrors.ShouldBeNull();
    }

    [Fact]
    public void Creates_an_error_with_a_specific_code()
    {
        // Arrange
        // Act
        var error = new ResultError("Tour not found", ResultErrorCodes.NotFound);

        // Assert
        error.Code.ShouldBe(ResultErrorCodes.NotFound);
        error.Detail.ShouldBe("Tour not found");
    }

    [Fact]
    public void Defensively_copies_validation_errors()
    {
        // Arrange
        var source = new Dictionary<string, string[]>
        {
            ["Name"] = ["Name is required"],
        };

        // Act
        var error = new ResultError("Validation failed", ResultErrorCodes.Invalid, source);
        source["Name"] = ["Changed after construction"];

        // Assert
        error.ValidationErrors.ShouldNotBeNull();
        (error.ValidationErrors["Name"]).ShouldBe(["Name is required"]);
    }

    [Fact]
    public void Exposes_read_only_validation_error_collections()
    {
        // Arrange
        var error = new ResultError(
            "Validation failed",
            ResultErrorCodes.Invalid,
            new Dictionary<string, string[]>
            {
                ["Name"] = ["Name is required"],
            });

        error.ValidationErrors.ShouldNotBeNull();

        // Act
        var messages = error.ValidationErrors["Name"];

        // Assert
        (messages).ShouldNotBeOfType<List<string>>();
        ((Action)(() => ((IList<string>)messages).Add("Changed after construction"))).ShouldThrow<NotSupportedException>();
        (error.ValidationErrors["Name"]).ShouldBe(["Name is required"]);
    }

    [Fact]
    public void Uses_code_and_detail_in_tostring()
    {
        // Arrange
        var error = new ResultError("Tour not found", ResultErrorCodes.NotFound);

        // Act
        var text = error.ToString();

        // Assert
        text.ShouldBe("not_found: Tour not found");
    }

    [Fact]
    public void Supports_value_equality_for_identical_errors()
    {
        // Arrange
        var left = new ResultError(
            "Validation failed",
            ResultErrorCodes.Invalid,
            new Dictionary<string, string[]>
            {
                ["Name"] = ["Name is required"],
            });
        var right = new ResultError(
            "Validation failed",
            ResultErrorCodes.Invalid,
            new Dictionary<string, string[]>
            {
                ["Name"] = ["Name is required"],
            });

        // Act
        var equalAsTyped = left.Equals(right);
        var equalAsObject = left.Equals((object)right);

        // Assert
        equalAsTyped.ShouldBeTrue();
        equalAsObject.ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Detects_different_error_details()
    {
        // Arrange
        var left = new ResultError("Validation failed", ResultErrorCodes.Invalid);
        var right = new ResultError("Something else failed", ResultErrorCodes.Invalid);

        // Act
        var equal = left.Equals(right);

        // Assert
        equal.ShouldBeFalse();
    }

    [Fact]
    public void Detects_different_validation_payloads()
    {
        // Arrange
        var left = new ResultError(
            "Validation failed",
            ResultErrorCodes.Invalid,
            new Dictionary<string, string[]>
            {
                ["Name"] = ["Name is required"],
            });
        var right = new ResultError(
            "Validation failed",
            ResultErrorCodes.Invalid,
            new Dictionary<string, string[]>
            {
                ["Name"] = ["Name must be at least 3 characters"],
            });

        // Act
        var equal = left.Equals(right);

        // Assert
        equal.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_null_or_whitespace_detail(string? detail)
    {
        // Arrange
        // Act
        var exception = Record.Exception(() => new ResultError(detail ?? NullArgumentData.String(), ResultErrorCodes.Error));

        // Assert
        exception.ShouldNotBeNull();
        var argumentException = exception.ShouldBeAssignableTo<ArgumentException>();
        argumentException.ParamName.ShouldBe("detail");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_null_or_whitespace_code(string? code)
    {
        // Arrange
        // Act
        var exception = Record.Exception(() => new ResultError("Something went wrong", code ?? NullArgumentData.String()));

        // Assert
        exception.ShouldNotBeNull();
        var argumentException = exception.ShouldBeAssignableTo<ArgumentException>();
        argumentException.ParamName.ShouldBe("code");
    }

    [Fact]
    public void Rejects_validation_dictionaries_with_null_message_arrays()
    {
        // Arrange
        var validationErrors = ResultErrorTestsHelpers.CreateValidationErrorsWithNullMessageArray();

        // Act
        var exception = Record.Exception(() => new ResultError("Validation failed", ResultErrorCodes.Invalid, validationErrors));

        // Assert
        exception.ShouldNotBeNull();
        var argumentException = exception.ShouldBeAssignableTo<ArgumentException>();
        argumentException.ParamName.ShouldBe("messages");
    }

}
