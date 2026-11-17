namespace PandaBear.Encodings.Tests;

/// <summary>Tests RFC 4648 Base32.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.EncodingsCapability)]
public sealed class Base32Tests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Bytes_encode_as_the_rfc_4648_test_vectors_without_padding(string text, string expected)
    {
        // Act
        var encoded = Base32.Encode(System.Text.Encoding.ASCII.GetBytes(text));

        // Assert
        encoded.ShouldBe(expected);
    }

    [Theory]
    [InlineData("mzxw 6-ytb==", "MZXW6YTB")]
    [InlineData("JBSW Y3DP EHPK 3PXP", "JBSWY3DPEHPK3PXP")]
    [InlineData("0189", "")]
    public void Normalizing_uppercases_and_drops_everything_outside_the_alphabet(string text, string expected)
    {
        // Act
        var normalized = Base32.Normalize(text);

        // Assert
        normalized.ShouldBe(expected);
    }

    [Fact]
    public void Normalizing_refuses_null()
    {
        // Act
        var normalize = () => Base32.Normalize(null!);

        // Assert
        normalize.ShouldThrow<ArgumentNullException>();
    }
}
