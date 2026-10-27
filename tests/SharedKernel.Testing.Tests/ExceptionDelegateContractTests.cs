namespace PandaBear.Testing.Tests;

/// <summary>Tests that ShouldThrow returns the very exception thrown by a delegate and requires its exact type.</summary>
public sealed class ExceptionDelegateContractTests
{
    [Theory]
    [InlineData("value")]
    [InlineData(null)]
    public void Synchronous_delegate_preserves_identity_and_exact_exception_type(string? parameterName)
    {
        // Arrange
        var expected = new ArgumentNullException(parameterName);
        Action action = () => throw expected;
        Action wrongType = () => action.ShouldThrow<ArgumentException>();

        // Act
        var actual = action.ShouldThrow<ArgumentNullException>();

        // Assert
        actual.ShouldBeSameAs(expected);
        actual.ParamName.ShouldBe(parameterName);
        wrongType.ShouldThrow<Xunit.Sdk.ThrowsException>();
    }

    [Theory]
    [InlineData("value")]
    [InlineData(null)]
    public void Property_delegate_preserves_identity_and_exact_exception_type(string? parameterName)
    {
        // Arrange
        var expected = new ArgumentNullException(parameterName);
        Func<object?> action = () => throw expected;
        Action wrongType = () => action.ShouldThrow<ArgumentException>();

        // Act
        var actual = action.ShouldThrow<ArgumentNullException>();

        // Assert
        actual.ShouldBeSameAs(expected);
        actual.ParamName.ShouldBe(parameterName);
        wrongType.ShouldThrow<Xunit.Sdk.ThrowsException>();
    }

    [Theory]
    [InlineData("value")]
    [InlineData(null)]
    public async Task Asynchronous_delegate_preserves_identity_and_exact_exception_type(string? parameterName)
    {
        // Arrange
        var expected = new ArgumentNullException(parameterName);
        Func<Task> action = async () =>
        {
            await Task.Yield();
            throw expected;
        };
        Func<Task> wrongType = () => action.ShouldThrow<ArgumentException>();

        // Act
        var actual = await action.ShouldThrow<ArgumentNullException>();

        // Assert
        actual.ShouldBeSameAs(expected);
        actual.ParamName.ShouldBe(parameterName);
        await wrongType.ShouldThrow<Xunit.Sdk.ThrowsException>();
        var actualResult = await action.ShouldThrowAssignableTo<ArgumentException>();
        actualResult.ShouldBeSameAs(expected);
    }
}
