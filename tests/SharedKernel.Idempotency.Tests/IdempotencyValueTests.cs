namespace SharedKernel.Idempotency.Tests;

public sealed class IdempotencyValueTests
{
    [Fact]
    public void Key_From_Trims_Value()
    {
        // Arrange
        const string value = " message-123 ";

        // Act
        var key = IdempotencyKey.From(value);

        // Assert
        Assert.Equal("message-123", key.Value);
        Assert.Equal("message-123", key.ToString());
    }

    [Fact]
    public void Key_From_Rejects_Null_Value()
    {
        // Arrange
        string? value = null;

        // Act, Assert
        Assert.Throws<ArgumentNullException>(() => IdempotencyKey.From(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Key_From_Rejects_Blank_Values(string value)
    {
        // Arrange, Act, Assert
        Assert.Throws<ArgumentException>(() => IdempotencyKey.From(value));
    }

    [Fact]
    public void Scope_From_Trims_Value()
    {
        // Arrange
        const string value = " inbox:tour-created ";

        // Act
        var scope = IdempotencyScope.From(value);

        // Assert
        Assert.Equal("inbox:tour-created", scope.Value);
        Assert.Equal("inbox:tour-created", scope.ToString());
    }

    [Fact]
    public void Operation_Combines_Scope_And_Key()
    {
        // Arrange
        var scope = IdempotencyScope.From("projection:catalog-tour");
        var key = IdempotencyKey.From("event-42");

        // Act
        var operation = new IdempotencyOperation(scope, key);

        // Assert
        Assert.Equal(scope, operation.Scope);
        Assert.Equal(key, operation.Key);
    }
}
