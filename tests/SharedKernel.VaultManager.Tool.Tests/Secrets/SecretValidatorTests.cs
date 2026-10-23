using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

public sealed class SecretValidatorTests
{
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
