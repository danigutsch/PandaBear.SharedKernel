namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class ResultErrorTests
{
    [Fact]
    public void Creates_an_error_with_detail_and_default_code()
    {
        // Arrange
        // Act
        var error = new ResultError("Something went wrong");

        // Assert
        Assert.Equal(ResultErrorCodes.Error, error.Code);
        Assert.Equal("Something went wrong", error.Detail);
        Assert.Null(error.ValidationErrors);
    }

    [Fact]
    public void Creates_an_error_with_a_specific_code()
    {
        // Arrange
        // Act
        var error = new ResultError("Tour not found", ResultErrorCodes.NotFound);

        // Assert
        Assert.Equal(ResultErrorCodes.NotFound, error.Code);
        Assert.Equal("Tour not found", error.Detail);
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
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(["Name is required"], error.ValidationErrors["Name"]);
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

        Assert.NotNull(error.ValidationErrors);

        // Act
        var messages = error.ValidationErrors["Name"];

        // Assert
        Assert.IsNotType<List<string>>(messages);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)messages).Add("Changed after construction"));
        Assert.Equal(["Name is required"], error.ValidationErrors["Name"]);
    }

    [Fact]
    public void Uses_code_and_detail_in_tostring()
    {
        // Arrange
        var error = new ResultError("Tour not found", ResultErrorCodes.NotFound);

        // Act
        var text = error.ToString();

        // Assert
        Assert.Equal("not_found: Tour not found", text);
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
        Assert.True(equalAsTyped);
        Assert.True(equalAsObject);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
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
        Assert.False(equal);
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
        Assert.False(equal);
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
        Assert.NotNull(exception);
        var argumentException = Assert.IsAssignableFrom<ArgumentException>(exception);
        Assert.Equal("detail", argumentException.ParamName);
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
        Assert.NotNull(exception);
        var argumentException = Assert.IsAssignableFrom<ArgumentException>(exception);
        Assert.Equal("code", argumentException.ParamName);
    }

    [Fact]
    public void Rejects_validation_dictionaries_with_null_message_arrays()
    {
        // Arrange
        var validationErrors = ResultErrorTestsHelpers.CreateValidationErrorsWithNullMessageArray();

        // Act
        var exception = Record.Exception(() => new ResultError("Validation failed", ResultErrorCodes.Invalid, validationErrors));

        // Assert
        Assert.NotNull(exception);
        var argumentException = Assert.IsAssignableFrom<ArgumentException>(exception);
        Assert.Equal("messages", argumentException.ParamName);
    }

}
