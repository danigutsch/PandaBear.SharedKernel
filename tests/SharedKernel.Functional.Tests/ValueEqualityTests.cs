namespace SharedKernel.Functional.Tests;

[Trait(global::SharedKernel.Testing.SharedKernelTestTraitNames.CategoryName, TestTraits.EdgeCaseCategory)]
public sealed class ValueEqualityTests
{
    [Fact]
    public void Supports_Option_Operators_And_Object_Equality()
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
    public void Distinguishes_Different_Option_Values()
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
    public void Supports_Non_Generic_Result_Operators_And_Object_Equality()
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
    public void Distinguishes_Different_Non_Generic_Result_States()
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
    public void Supports_Generic_Result_Operators_And_Object_Equality()
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
    public void Distinguishes_Different_Generic_Result_Values()
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
