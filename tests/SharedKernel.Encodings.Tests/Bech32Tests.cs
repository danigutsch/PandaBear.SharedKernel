namespace PandaBear.Encodings.Tests;

/// <summary>Tests Bech32 against the BIP-173 vectors. age uses Bech32, not Bech32m, and applies no length limit.</summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.EncodingsCapability)]
public sealed class Bech32Tests
{
    [Theory]
    [InlineData("A12UEL5L", "a")]
    [InlineData("a12uel5l", "a")]
    [InlineData("an83characterlonghumanreadablepartthatcontainsthenumber1andtheexcludedcharactersbio1tt5tgs", "an83characterlonghumanreadablepartthatcontainsthenumber1andtheexcludedcharactersbio")]
    [InlineData("abcdef1qpzry9x8gf2tvdw0s3jn54khce6mua7lmqqqxw", "abcdef")]
    [InlineData("split1checkupstagehandshakeupstreamerranterredcaperred2y9e3w", "split")]
    [InlineData("?1ezyfcl", "?")]
    public void A_bip173_valid_vector_decodes(string text, string humanReadablePart)
    {
        // Act
        var valid = Bech32.TryDecode(text, Bech32.Bip173MaxLength, out var hrp, out _);

        // Assert
        valid.ShouldBeTrue();
        hrp.ShouldBe(humanReadablePart);
    }

    [Fact]
    public void The_bip173_vector_with_a_one_character_human_readable_part_and_82_data_characters_decodes()
    {
        // Arrange
        var text = "11" + new string('q', 82) + "c8247j";

        // Act
        var valid = Bech32.TryDecode(text, Bech32.Bip173MaxLength, out var hrp, out _);

        // Assert
        valid.ShouldBeTrue();
        hrp.ShouldBe("1");
    }

    [Theory]
    [InlineData("pzry9x0s0muk")]
    [InlineData("1pzry9x0s0muk")]
    [InlineData("x1b4n0q5v")]
    [InlineData("li1dgmt3")]
    [InlineData("A1G7SGD8")]
    [InlineData("10a06t8")]
    [InlineData("1qzzfhee")]
    [InlineData("A12uEL5L")]
    [InlineData(" 1nwlqzd")]
    [InlineData("\u007f1axkwrx")]
    [InlineData("\u00801eym55h")]
    [InlineData("")]
    public void A_bip173_invalid_vector_is_rejected(string text)
    {
        // Act
        var valid = Bech32.TryDecode(text, Bech32.Bip173MaxLength, out _, out _);

        // Assert
        valid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("A1LQFN3A")]
    [InlineData("a1lqfn3a")]
    [InlineData("abcdef1l7aum6echk45nj3s0wdvt2fg8x9yrzpqzd3ryx")]
    public void A_bech32m_string_is_not_valid_bech32(string text)
    {
        // Act
        var valid = Bech32.TryDecode(text, maxLength: null, out _, out _);

        // Assert
        valid.ShouldBeFalse();
    }

    [Fact]
    public void The_bip173_length_limit_is_a_parameter_and_age_applies_none()
    {
        // Arrange
        const string text = "an84characterslonghumanreadablepartthatcontainsthenumber1andtheexcludedcharactersbio1569pvx";

        // Act
        var withLimit = Bech32.TryDecode(text, Bech32.Bip173MaxLength, out _, out _);
        var withoutLimit = Bech32.TryDecode(text, maxLength: null, out _, out _);

        // Assert
        withLimit.ShouldBeFalse();
        withoutLimit.ShouldBeTrue();
    }

    [Fact]
    public void An_age_x25519_recipient_decodes_to_a_32_byte_key()
    {
        // Act
        var valid = Bech32.TryDecode("age1s6ljywnmxkxndew9qztcjgfa3lsqfwe0rec66a9jjugzuxaagg3sgumkyf", maxLength: null, out var hrp, out var data);
        var bytes = Bech32.TryConvertToBytes(data, out var key);

        // Assert
        valid.ShouldBeTrue();
        hrp.ShouldBe("age");
        bytes.ShouldBeTrue();
        key.Length.ShouldBe(32);
    }

    [Fact]
    public void Padding_bits_that_are_not_zero_are_rejected()
    {
        // Act
        var valid = Bech32.TryConvertToBytes([31], out _);

        // Assert
        valid.ShouldBeFalse();
    }
}
