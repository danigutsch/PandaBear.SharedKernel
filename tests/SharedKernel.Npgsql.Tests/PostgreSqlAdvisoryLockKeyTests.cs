namespace PandaBear.Npgsql.Tests;

public sealed class PostgreSqlAdvisoryLockKeyTests
{
    [Theory]
    [InlineData("", -2039914840885289964L)]
    [InlineData("abc", -5010229573455851542L)]
    [InlineData("inventory:123", -6502278405708100228L)]
    [InlineData("caf\u00e9", -8858723660289998967L)]
    [InlineData("cafe\u0301", -9084035275619258937L)]
    [InlineData(" abc ", 4516856718243218399L)]
    [InlineData("ABC", -5344642063536853079L)]
    public void Derives_the_signed_big_endian_key_from_exact_utf8_text(string identity, long expected)
    {
        // Act
        var actual = PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(identity);

        // Assert
        actual.ShouldBe(expected);
    }

    [Fact]
    public void Rejects_a_null_identity()
    {
        // Arrange
        Func<object?> action = () => PostgreSqlAdvisoryLockKey.FromUtf8Sha256BigEndian(null!);

        // Act
        var exception = action.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("identity");
    }
}
