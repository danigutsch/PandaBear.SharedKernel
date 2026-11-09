namespace PandaBear.Secrets.Tests;

/// <summary>Tests that decrypted bytes never print themselves and are cleared on disposal.</summary>
[Trait("Capability", "Secrets")]
public sealed class SecretBytesTests
{
    [Fact]
    public void ToString_and_the_debugger_text_never_show_the_content()
    {
        // Arrange
        using var secret = SecretBytes.CopyOf("hunter2"u8);

        // Act
        var text = secret.ToString();
        var interpolated = $"{secret}";

        // Assert
        text.ShouldBe("SecretBytes(redacted)");
        interpolated.ShouldNotContain("hunter2", StringComparison.Ordinal);
    }

    [Fact]
    public void The_bytes_are_readable_until_disposal()
    {
        // Arrange
        using var secret = SecretBytes.CopyOf("hunter2"u8);

        // Assert
        secret.Length.ShouldBe(7);
        secret.IsDisposed.ShouldBeFalse();
        secret.AsSpan().SequenceEqual("hunter2"u8).ShouldBeTrue();
        secret.RevealUtf8().ShouldBe("hunter2");
    }

    [Fact]
    public void Dispose_overwrites_the_bytes_and_later_reads_fail()
    {
        // Arrange
        var secret = SecretBytes.CopyOf("hunter2"u8);
        var view = secret.AsSpan();

        // Act
        secret.Dispose();
        secret.Dispose();

        // Assert
        view.ToArray().ShouldBe(new byte[7]);
        secret.IsDisposed.ShouldBeTrue();
        secret.Length.ShouldBe(0);
        Func<object?> read = () => secret.RevealUtf8();
        read.ShouldThrow<ObjectDisposedException>();
    }

    [Fact]
    public void The_copy_is_independent_of_the_source()
    {
        // Arrange
        var source = "abc"u8.ToArray();

        // Act
        using var secret = SecretBytes.CopyOf(source);
        source[0] = (byte)'z';

        // Assert
        secret.RevealUtf8().ShouldBe("abc");
    }

    [Fact]
    public void WriteTo_copies_the_bytes_to_a_stream_and_rejects_null()
    {
        // Arrange
        using var secret = SecretBytes.CopyOf("abc"u8);
        using var stream = new MemoryStream();

        // Act
        secret.WriteTo(stream);
        Action act = () => secret.WriteTo(null!);

        // Assert
        stream.ToArray().ShouldBe("abc"u8.ToArray());
        act.ShouldThrow<ArgumentNullException>();
    }
}
