namespace PandaBear.Testing.Tests;

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
        Action wrongParameter = () => action.ShouldThrow<ArgumentNullException>("other");

        // Act
        var actual = action.ShouldThrow<ArgumentNullException>(parameterName);

        // Assert
        actual.ShouldBeSameAs(expected);
        wrongType.ShouldThrow<Xunit.Sdk.ThrowsException>();
        wrongParameter.ShouldThrow<Xunit.Sdk.ThrowsException>();
    }

    [Theory]
    [InlineData("value")]
    [InlineData(null)]
    public void Property_delegate_preserves_identity_and_parameter_validation(string? parameterName)
    {
        // Arrange
        var expected = new ArgumentNullException(parameterName);
        Func<object?> action = () => throw expected;
        Action wrongType = () => action.ShouldThrow<ArgumentException>();
        Action wrongParameter = () => action.ShouldThrow<ArgumentNullException>("other");

        // Act
        var actual = action.ShouldThrow<ArgumentNullException>(parameterName);

        // Assert
        actual.ShouldBeSameAs(expected);
        wrongType.ShouldThrow<Xunit.Sdk.ThrowsException>();
        wrongParameter.ShouldThrow<Xunit.Sdk.ThrowsException>();
    }

    [Theory]
    [InlineData("value")]
    [InlineData(null)]
    public async Task Asynchronous_delegate_preserves_identity_exact_type_and_parameter_validation(string? parameterName)
    {
        // Arrange
        var expected = new ArgumentNullException(parameterName);
        Func<Task> action = async () =>
        {
            await Task.Yield();
            throw expected;
        };
        Func<Task> wrongType = () => action.ShouldThrow<ArgumentException>();
        Func<Task> wrongParameter = () => action.ShouldThrow<ArgumentNullException>("other");

        // Act
        var actual = await action.ShouldThrow<ArgumentNullException>(parameterName);

        // Assert
        actual.ShouldBeSameAs(expected);
        await wrongType.ShouldThrow<Xunit.Sdk.ThrowsException>();
        await wrongParameter.ShouldThrow<Xunit.Sdk.ThrowsException>();
        (await action.ShouldThrowAssignableTo<ArgumentException>()).ShouldBeSameAs(expected);
    }
}
