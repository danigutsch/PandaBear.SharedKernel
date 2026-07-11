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
        (key.Value).ShouldBe("message-123");
        (key.ToString()).ShouldBe("message-123");
    }

    [Fact]
    public void Key_from_rejects_null_value()
    {
        // Arrange
        string? value = null;

        // Act, Assert
        ((Func<object?>)(() => IdempotencyKey.From(value))).ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Key_default_instance_rejects_value_access()
    {
        // Arrange
        var key = default(IdempotencyKey);

        // Act, Assert
        ((Func<object?>)(() => key.Value)).ShouldThrow<InvalidOperationException>();
        ((Func<object?>)(() => key.ToString())).ShouldThrow<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Key_from_rejects_blank_values(string value)
    {
        // Arrange, Act, Assert
        ((Func<object?>)(() => IdempotencyKey.From(value))).ShouldThrow<ArgumentException>();
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
        (key.Value).ShouldBe(value);
    }

    [Theory]
    [InlineData("key with spaces")]
    [InlineData("key/with/slashes")]
    [InlineData("key@domain")]
    [InlineData("key#fragment")]
    public void Key_from_rejects_values_outside_token_format(string value)
    {
        // Arrange, Act, Assert
        ((Func<object?>)(() => IdempotencyKey.From(value))).ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Key_from_rejects_values_longer_than_255_characters()
    {
        // Arrange
        var value = new string('a', 256);

        // Act, Assert
        ((Func<object?>)(() => IdempotencyKey.From(value))).ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Scope_from_trims_value()
    {
        // Arrange
        const string value = " inbox:tour-created ";

        // Act
        var scope = IdempotencyScope.From(value);

        // Assert
        (scope.Value).ShouldBe("inbox:tour-created");
        (scope.ToString()).ShouldBe("inbox:tour-created");
    }

    [Fact]
    public void Scope_from_rejects_null_value()
    {
        // Arrange
        string? value = null;

        // Act, Assert
        ((Func<object?>)(() => IdempotencyScope.From(value))).ShouldThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Scope_from_rejects_blank_values(string value)
    {
        // Arrange, Act, Assert
        ((Func<object?>)(() => IdempotencyScope.From(value))).ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Scope_default_instance_rejects_value_access()
    {
        // Arrange
        var scope = default(IdempotencyScope);

        // Act, Assert
        ((Func<object?>)(() => scope.Value)).ShouldThrow<InvalidOperationException>();
        ((Func<object?>)(() => scope.ToString())).ShouldThrow<InvalidOperationException>();
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
        (operation.Scope).ShouldBe(scope);
        (operation.Key).ShouldBe(key);
    }
}
