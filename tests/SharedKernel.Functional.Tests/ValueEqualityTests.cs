namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.EdgeCaseCategory)]
public sealed class ValueEqualityTests
{
    [Fact]
    public void Supports_option_operators_and_object_equality()
    {
        // Arrange
        var left = Option.Some("porto");
        var right = Option.Some("porto");

        // Act
        var equalOperator = left == right;
        var notEqualOperator = left != right;
        var equalAsObject = left.Equals((object)right);

        // Assert
        Assert.True(equalOperator);
        Assert.False(notEqualOperator);
        Assert.True(equalAsObject);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Distinguishes_different_option_values()
    {
        // Arrange
        var left = Option.Some("porto");
        var right = Option.Some("lisbon");

        // Act
        var equal = left == right;

        // Assert
        Assert.False(equal);
    }

    [Fact]
    public void Supports_non_generic_result_operators_and_object_equality()
    {
        // Arrange
        var left = Result.Error("Unexpected failure");
        var right = Result.Error("Unexpected failure");

        // Act
        var equalOperator = left == right;
        var notEqualOperator = left != right;
        var equalAsObject = left.Equals((object)right);

        // Assert
        Assert.True(equalOperator);
        Assert.False(notEqualOperator);
        Assert.True(equalAsObject);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Distinguishes_different_non_generic_result_states()
    {
        // Arrange
        var left = Result.Ok();
        var right = Result.Error("Unexpected failure");

        // Act
        var equal = left == right;

        // Assert
        Assert.False(equal);
    }

    [Fact]
    public void Supports_generic_result_operators_and_object_equality()
    {
        // Arrange
        var left = Result.Ok("porto");
        var right = Result.Ok("porto");

        // Act
        var equalOperator = left == right;
        var notEqualOperator = left != right;
        var equalAsObject = left.Equals((object)right);

        // Assert
        Assert.True(equalOperator);
        Assert.False(notEqualOperator);
        Assert.True(equalAsObject);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Distinguishes_different_generic_result_values()
    {
        // Arrange
        var left = Result.Ok("porto");
        var right = Result.Ok("lisbon");

        // Act
        var equal = left == right;

        // Assert
        Assert.False(equal);
    }
}
