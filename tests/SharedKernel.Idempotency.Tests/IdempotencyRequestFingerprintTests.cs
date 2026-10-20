namespace PandaBear.Idempotency.Tests;

public sealed class IdempotencyRequestFingerprintTests
{
    [Theory]
    [InlineData("abc", "1", "E00F2689192B0CB46E6AEF0203A77E3C8CE549DB5640C011D3D864ED0CAF6799")]
    [InlineData("🚌", "é", "0E25966EA49FD26AC1275DEFD6D3870F09DE6BEAE3EA7C93955B968DA09747B9")]
    [InlineData("a:b", "c", "8A37856518DACC57691927D22E62FEB58519471AD25E0FC44C6EA9D0DC17BDA7")]
    [InlineData("a", "b:c", "B8D47370810D318BC6F70A5C93E34B860BA61E7638B47E071F298E97F20FFE5A")]
    public void Ordered_components_preserve_the_length_framed_hash_contract(string first, string second, string expected)
    {
        // Act
        var actual = IdempotencyRequestFingerprint.Create(first, second);

        // Assert
        actual.ShouldBe(expected);
    }

    [Fact]
    public void Empty_component_differs_from_no_components()
    {
        // Act
        var emptyList = IdempotencyRequestFingerprint.Create();
        var emptyComponent = IdempotencyRequestFingerprint.Create(string.Empty);

        // Assert
        emptyList.ShouldBe("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855");
        emptyComponent.ShouldBe("BA768B331FD86CEC803BE04E56AB2B3D4C0E98EF4EE4FCD4E72AD7CCE61A1D1F");
    }

    [Fact]
    public void Component_order_and_whitespace_are_significant()
    {
        // Act
        var ordered = IdempotencyRequestFingerprint.Create("first", "second");
        var reversed = IdempotencyRequestFingerprint.Create("second", "first");
        var padded = IdempotencyRequestFingerprint.Create(" first", "second");

        // Assert
        ordered.ShouldNotBe(reversed);
        ordered.ShouldNotBe(padded);
    }

    [Fact]
    public void Null_component_list_is_rejected()
    {
        // Arrange
        Action action = () => IdempotencyRequestFingerprint.Create(null!);

        // Act
        var exception = action.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("components");
    }

    [Fact]
    public void Null_component_is_rejected()
    {
        // Arrange
        Action action = () => IdempotencyRequestFingerprint.Create("value", null!);

        // Act
        var exception = action.ShouldThrow<ArgumentNullException>();

        // Assert
        exception.ParamName.ShouldBe("component");
    }
}
