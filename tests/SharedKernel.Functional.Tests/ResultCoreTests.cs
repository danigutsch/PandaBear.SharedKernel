namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.ResultCapability)]
[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class ResultCoreTests
{
    [Fact]
    public void Creates_A_Successful_Result()
    {
        // Arrange
        // Act
        var result = Result.Ok();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Null(result.ErrorDetails);
    }

    [Fact]
    public void Creates_A_Failed_Result_With_Error_Details()
    {
        // Arrange
        // Act
        var result = Result.Invalid("Validation failed", "name", "Name is required");

        // Assert
        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.True(result.TryGetError(out var error));
        Assert.NotNull(error);
        Assert.Equal(ResultErrorCodes.Invalid, error.Code);
        Assert.Equal("Validation failed", error.Detail);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(["Name is required"], error.ValidationErrors["name"]);
    }

    [Fact]
    public void Creates_A_Failed_Result_With_Multiple_Validation_Errors()
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
        Assert.Equal(ResultStatus.Invalid, result.Status);
        var error = result.ErrorDetails;
        Assert.NotNull(error);
        Assert.Equal(ResultErrorCodes.Invalid, error.Code);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(["Name is required"], error.ValidationErrors["name"]);
        Assert.Equal(["Email is invalid"], error.ValidationErrors["email"]);
    }

    [Fact]
    public void Creates_A_Successful_Result_With_A_Value()
    {
        // Arrange
        // Act
        var result = Result.Ok("porto");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ResultStatus.Ok, result.Status);
        Assert.Equal("porto", result.Value);
        Assert.Null(result.ErrorDetails);
    }

    [Fact]
    public void Creates_A_Successful_Non_Generic_Result()
    {
        // Arrange
        // Act
        var result = Result.Created("porto").ToResult();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(ResultStatus.Created, result.Status);
        Assert.Null(result.ErrorDetails);
    }

    [Fact]
    public void Returns_True_For_Successful_Generic_Results()
    {
        // Arrange
        var result = Result.Ok("porto");

        // Act
        var hasValue = result.TryGetValue(out var value);

        // Assert
        Assert.True(hasValue);
        Assert.Equal("porto", value);
    }

    [Fact]
    public void Creates_A_Failed_Result_Without_A_Value()
    {
        // Arrange
        // Act
        var result = Result.Error<string>("Unexpected failure");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.True(result.TryGetError(out var error));
        Assert.NotNull(error);
        Assert.Equal(ResultErrorCodes.Error, error.Code);
        Assert.Equal("Unexpected failure", error.Detail);
    }

    [Fact]
    public void Returns_False_For_Successful_Results()
    {
        // Arrange
        var result = Result.Ok();

        // Act
        var hasError = result.TryGetError(out var error);

        // Assert
        Assert.False(hasError);
        Assert.Null(error);
    }

    [Fact]
    public void Returns_True_For_Failed_Generic_Results()
    {
        // Arrange
        var result = Result.Error<string>("Unexpected failure");

        // Act
        var hasError = result.TryGetError(out var error);

        // Assert
        Assert.True(hasError);
        Assert.NotNull(error);
        Assert.Equal(ResultErrorCodes.Error, error.Code);
        Assert.Equal("Unexpected failure", error.Detail);
    }

    [Fact]
    public void Returns_False_For_An_Uninitialized_Non_Generic_Result()
    {
        // Arrange
        var result = default(Result);

        // Act
        var hasError = result.TryGetError(out var error);

        // Assert
        Assert.False(hasError);
        Assert.Null(error);
        Assert.False(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Returns_False_For_An_Uninitialized_Generic_Result()
    {
        // Arrange
        var result = default(Result<string>);

        // Act
        var hasValue = result.TryGetValue(out var value);
        var hasError = result.TryGetError(out var error);

        // Assert
        Assert.False(hasValue);
        Assert.Null(value);
        Assert.False(hasError);
        Assert.Null(error);
        Assert.False(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Rejects_Empty_Validation_Error_Dictionaries()
    {
        // Arrange
        var validationErrors = new Dictionary<string, string[]>();

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Result.Invalid("Validation failed", validationErrors));

        // Assert
        Assert.Equal("validationErrors", exception.ParamName);
    }

    [Fact]
    public void Rejects_Validation_Dictionaries_With_Empty_Field_Names()
    {
        // Arrange
        var validationErrors = new Dictionary<string, string[]>
        {
            [string.Empty] = ["Name is required"],
        };

        // Act
        var exception = Assert.Throws<ArgumentException>(() => Result.Invalid("Validation failed", validationErrors));

        // Assert
        Assert.Equal("field", exception.ParamName);
    }

    [Fact]
    public void Rejects_Validation_Dictionaries_With_Empty_Message_Arrays()
    {
        // Arrange
        var validationErrors = new Dictionary<string, string[]>
        {
            ["Name"] = [],
        };

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Result.Invalid("Validation failed", validationErrors));

        // Assert
        Assert.Equal("validationErrors", exception.ParamName);
    }

    [Fact]
    public void Rejects_Validation_Dictionaries_With_Null_Message_Entries()
    {
        // Arrange
        var validationErrors = new Dictionary<string, string[]>
        {
            ["Name"] = NullArgumentData.StringArray(),
        };

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => Result.Invalid("Validation failed", validationErrors));

        // Assert
        Assert.Equal("messages", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_Null_Or_Whitespace_Error_Detail(string? detail)
    {
        // Arrange
        // Act
        var exception = Record.Exception(() => Result.Error(detail ?? NullArgumentData.String()));

        // Assert
        Assert.NotNull(exception);
        var argumentException = Assert.IsAssignableFrom<ArgumentException>(exception);
        Assert.Equal("detail", argumentException.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_Null_Or_Whitespace_Validation_Field(string? field)
    {
        // Arrange
        // Act
        var exception = Record.Exception(() => Result.Invalid("Validation failed", field ?? NullArgumentData.String(), "Name is required"));

        // Assert
        Assert.NotNull(exception);
        var argumentException = Assert.IsAssignableFrom<ArgumentException>(exception);
        Assert.Equal("field", argumentException.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_Null_Or_Whitespace_Validation_Message(string? message)
    {
        // Arrange
        // Act
        var exception = Record.Exception(() => Result.Invalid("Validation failed", "Name", message ?? NullArgumentData.String()));

        // Assert
        Assert.NotNull(exception);
        var argumentException = Assert.IsAssignableFrom<ArgumentException>(exception);
        Assert.Equal("message", argumentException.ParamName);
    }
}
