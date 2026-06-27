namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class ResultErrorTests
{
    [Fact]
    public void Creates_An_Error_With_Detail_And_Default_Code()
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
    public void Creates_An_Error_With_A_Specific_Code()
    {
        // Arrange
        // Act
        var error = new ResultError("Tour not found", ResultErrorCodes.NotFound);

        // Assert
        Assert.Equal(ResultErrorCodes.NotFound, error.Code);
        Assert.Equal("Tour not found", error.Detail);
    }

    [Fact]
    public void Defensively_Copies_Validation_Errors()
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
    public void Exposes_Read_Only_Validation_Error_Collections()
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
    public void Uses_Code_And_Detail_In_ToString()
    {
        // Arrange
        var error = new ResultError("Tour not found", ResultErrorCodes.NotFound);

        // Act
        var text = error.ToString();

        // Assert
        Assert.Equal("not_found: Tour not found", text);
    }

    [Fact]
    public void Supports_Value_Equality_For_Identical_Errors()
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
    public void Detects_Different_Error_Details()
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
    public void Detects_Different_Validation_Payloads()
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
    public void Rejects_Null_Or_Whitespace_Detail(string? detail)
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
    public void Rejects_Null_Or_Whitespace_Code(string? code)
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
    public void Rejects_Validation_Dictionaries_With_Null_Message_Arrays()
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
