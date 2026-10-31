namespace PandaBear.Npgsql.Tests;

/// <summary>Protects the lock identity contract while carrying its numeric value as a distinct type.</summary>
[PandaBear.Testing.TestScope(PandaBear.Testing.TestScope.Unit)]
[PandaBear.Testing.TestHost(PandaBear.Testing.TestHost.InProcess)]
public sealed class PostgreSqlLockKeyTests
{
    [Fact]
    public async Task Existing_default_literal_calls_remain_unambiguous_and_keep_the_zero_key()
    {
        // Act
        Func<Task> acquireSession = () => PostgreSqlSessionAdvisoryLock.Acquire(null!, default, TestContext.Current.CancellationToken).AsTask();
        Func<Task> acquireTransaction = () => PostgreSqlTransactionAdvisoryLock.Acquire(null!, null!, default, TestContext.Current.CancellationToken).AsTask();

        // Assert
        await acquireSession.ShouldThrow<ArgumentNullException>();
        await acquireTransaction.ShouldThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData("", -2039914840885289964L)]
    [InlineData("abc", -5010229573455851542L)]
    [InlineData("inventory:123", -6502278405708100228L)]
    [InlineData("caf\u00e9", -8858723660289998967L)]
    [InlineData("cafe\u0301", -9084035275619258937L)]
    public void Identity_derivation_preserves_the_persisted_lock_protocol(string identity, long expected)
    {
        // Act
        var key = PostgreSqlLockKey.FromIdentity(identity);

        // Assert
        key.Value.ShouldBe(expected);
        key.ShouldBe(new PostgreSqlLockKey(expected));
    }

    [Fact]
    public void Ordering_uses_signed_numeric_values_and_zero_is_valid()
    {
        // Arrange
        var negative = new PostgreSqlLockKey(-1);
        var zero = default(PostgreSqlLockKey);

        // Act
        var order = negative.CompareTo(zero);

        // Assert
        order.ShouldBeLessThan(0);
        zero.Value.ShouldBe(0);
    }
}
