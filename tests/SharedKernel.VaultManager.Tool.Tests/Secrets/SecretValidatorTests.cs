using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Tests the validators that confirm a candidate secret: card numbers, Base32 seeds, BIP39 phrases, JWTs, token checksums, and age keys.</summary>
public sealed class SecretValidatorTests
{
    /// <summary>The card number validator accepts each supported brand prefix with a valid length and names the brand.</summary>
    [Theory]
    [InlineData("4", 13, "Visa")]
    [InlineData("4", 16, "Visa")]
    [InlineData("4", 19, "Visa")]
    [InlineData("51", 16, "Mastercard")]
    [InlineData("55", 16, "Mastercard")]
    [InlineData("2221", 16, "Mastercard")]
    [InlineData("2720", 16, "Mastercard")]
    [InlineData("34", 15, "Amex")]
    [InlineData("37", 15, "Amex")]
    [InlineData("6011", 16, "Discover")]
    [InlineData("644", 16, "Discover")]
    [InlineData("65", 19, "Discover")]
    [InlineData("3528", 16, "JCB")]
    [InlineData("3589", 19, "JCB")]
    [InlineData("36", 14, "Diners Club")]
    [InlineData("39", 19, "Diners Club")]
    [InlineData("62", 16, "UnionPay")]
    [InlineData("6759", 12, "Maestro")]
    [InlineData("5018", 19, "Maestro")]
    public void Card_number_validator_accepts_brand_prefix_and_length(string prefix, int length, string brand)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(prefix);
        string number = TestSecrets.CardNumber(prefix, length);

        // Act
        var valid = CardNumberValidator.TryGetBrand(number, out string? actual);

        // Assert
        valid.ShouldBeTrue();
        actual.ShouldBe(brand);
    }

    /// <summary>The card number validator rejects a prefix with the wrong length for its brand or an unknown prefix.</summary>
    [Theory]
    [InlineData("34", 16)]
    [InlineData("4", 15)]
    [InlineData("2721", 16)]
    [InlineData("99", 16)]
    public void Card_number_validator_rejects_wrong_brand_length_or_prefix(string prefix, int length)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(prefix);
        string number = TestSecrets.CardNumber(prefix, length);

        // Act
        var valid = CardNumberValidator.TryGetBrand(number, out _);

        // Assert
        valid.ShouldBeFalse();
    }

    /// <summary>A Base32 secret needs at least eighty bits and only Base32 characters.</summary>
    [Fact]
    public void Base32_secret_needs_at_least_eighty_bits()
    {
        // Act
        var enough = Base32.IsTotpSecret(TestSecrets.Base32Encode(TestSecrets.Bytes(1, 10)));
        var tooShort = Base32.IsTotpSecret(TestSecrets.Base32Encode(TestSecrets.Bytes(1, 9)));
        var invalid = Base32.IsTotpSecret("ABCDEFGHIJKLMNO1");

        // Assert
        enough.ShouldBeTrue();
        tooShort.ShouldBeFalse();
        invalid.ShouldBeFalse();
    }

    /// <summary>The BIP39 checksum accepts every standard entropy length.</summary>
    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(28)]
    [InlineData(32)]
    public void Bip39_checksum_accepts_every_standard_length(int entropyBytes)
    {
        // Arrange
        string mnemonic = Bip39Reference.Mnemonic(TestSecrets.Bytes(entropyBytes, entropyBytes));

        // Act
        var valid = Bip39.IsValidMnemonic(mnemonic.Split(' '));

        // Assert
        valid.ShouldBeTrue();
    }

    /// <summary>A phrase that fails the BIP39 checksum but carries the Electrum seed version prefix is found as a high-confidence Electrum seed.</summary>
    [Fact]
    public void Electrum_seed_version_prefix_is_recognized_when_bip39_checksum_fails()
    {
        // Arrange
        string phrase = Bip39Reference.ElectrumPhrase();

        // Act
        var findings = SecretScanner.Default.Scan(SecretTestItems.Note(phrase));

        // Assert
        var finding = findings.ShouldHaveSingleItem();
        finding.RuleId.ShouldBe("electrum-seed");
        finding.Confidence.ShouldBe(Confidence.High);
    }

    /// <summary>A JWT header must decode to JSON that has an algorithm.</summary>
    [Fact]
    public void Jwt_header_must_decode_to_json_with_alg()
    {
        // Act
        var valid = JwtValidator.HasAlgorithmHeader(TestSecrets.Jwt(1));
        var missing = JwtValidator.HasAlgorithmHeader(TestSecrets.Base64UrlEncode("{\"typ\":\"JWT\"}") + ".eyJ4IjoxfQ.sig");
        var garbage = JwtValidator.HasAlgorithmHeader("eyJ!!!.eyJ.x");

        // Assert
        valid.ShouldBeTrue();
        missing.ShouldBeFalse();
        garbage.ShouldBeFalse();
    }

    /// <summary>GitHub and npm token checksums accept tokens from an independent generator and reject a tampered one, and CRC-32 matches the standard check value.</summary>
    [Fact]
    public void Github_and_npm_checksums_round_trip_with_independent_generator()
    {
        // Arrange
        string github = TestSecrets.GitHubToken("gho", 2);
        string npm = TestSecrets.NpmToken(3);

        // Act
        var githubValid = TokenChecksums.IsValidGitHubToken(github);
        var npmValid = TokenChecksums.IsValidNpmToken(npm);
        var tampered = TokenChecksums.IsValidGitHubToken(github[..10] + (github[10] == 'a' ? 'b' : 'a') + github[11..]);
        var checksum = TokenChecksums.Crc32("123456789"u8);

        // Assert
        githubValid.ShouldBeTrue();
        npmValid.ShouldBeTrue();
        tampered.ShouldBeFalse();
        checksum.ShouldBe(0xCBF43926u);
    }

    /// <summary>An age identity needs a valid bech32 checksum.</summary>
    [Fact]
    public void Age_identity_requires_valid_bech32_checksum()
    {
        // Arrange
        string key = Bech32Reference.Encode("age-secret-key-", TestSecrets.Bytes(4, 32)).ToUpperInvariant();
        string broken = key[..20] + (key[20] == 'Q' ? 'P' : 'Q') + key[21..];

        // Act
        var valid = Bech32.IsValidAgeSecretKey(key);
        var invalid = Bech32.IsValidAgeSecretKey(broken);

        // Assert
        valid.ShouldBeTrue();
        invalid.ShouldBeFalse();
    }
}
