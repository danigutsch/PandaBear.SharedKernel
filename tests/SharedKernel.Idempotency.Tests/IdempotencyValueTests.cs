namespace SharedKernel.Idempotency.Tests;

public sealed class IdempotencyValueTests
{
    [Fact]
    public void Key_from_trims_value()
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
    public void Key_from_rejects_null_value()
    {
        // Arrange
        string? value = null;

        // Act, Assert
        Assert.Throws<ArgumentNullException>(() => IdempotencyKey.From(value));
    }

    [Fact]
    public void Key_default_instance_rejects_value_access()
    {
        // Arrange
        var key = default(IdempotencyKey);

        // Act, Assert
        Assert.Throws<InvalidOperationException>(() => key.Value);
        Assert.Throws<InvalidOperationException>(() => key.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Key_from_rejects_blank_values(string value)
    {
        // Arrange, Act, Assert
        Assert.Throws<ArgumentException>(() => IdempotencyKey.From(value));
    }

    [Theory]
    [InlineData("8e03978e-40d5-43e8-bc93-6894a57f9324")]
    [InlineData("01JY8WMTF8SP5HW8XYCEV2Z2FR")]
    [InlineData("request_123:retry.1")]
    public void Key_from_accepts_opaque_token_values(string value)
    {
        // Arrange, Act
        var key = IdempotencyKey.From(value);

        // Assert
        Assert.Equal(value, key.Value);
    }

    [Theory]
    [InlineData("key with spaces")]
    [InlineData("key/with/slashes")]
    [InlineData("key@domain")]
    [InlineData("key#fragment")]
    public void Key_from_rejects_values_outside_token_format(string value)
    {
        // Arrange, Act, Assert
        Assert.Throws<ArgumentException>(() => IdempotencyKey.From(value));
    }

    [Fact]
    public void Key_from_rejects_values_longer_than_255_characters()
    {
        // Arrange
        var value = new string('a', 256);

        // Act, Assert
        Assert.Throws<ArgumentException>(() => IdempotencyKey.From(value));
    }

    [Fact]
    public void Scope_from_trims_value()
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
    public void Scope_from_rejects_null_value()
    {
        // Arrange
        string? value = null;

        // Act, Assert
        Assert.Throws<ArgumentNullException>(() => IdempotencyScope.From(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Scope_from_rejects_blank_values(string value)
    {
        // Arrange, Act, Assert
        Assert.Throws<ArgumentException>(() => IdempotencyScope.From(value));
    }

    [Fact]
    public void Scope_default_instance_rejects_value_access()
    {
        // Arrange
        var scope = default(IdempotencyScope);

        // Act, Assert
        Assert.Throws<InvalidOperationException>(() => scope.Value);
        Assert.Throws<InvalidOperationException>(() => scope.ToString());
    }

    [Fact]
    public void Operation_combines_scope_and_key()
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
