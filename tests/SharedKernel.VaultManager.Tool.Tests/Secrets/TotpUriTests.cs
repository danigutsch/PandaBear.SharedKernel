using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

/// <summary>Tests normalizing TOTP secrets and URIs and decoding migration payloads.</summary>
public sealed class TotpUriTests
{
    /// <summary>A plain Base32 secret is uppercased, stripped of spaces, and wrapped in an otpauth URI with the issuer.</summary>
    [Fact]
    public void Plain_base32_is_uppercased_stripped_and_wrapped_in_otpauth()
    {
        // Arrange
        string secret = TestSecrets.Base32Encode(TestSecrets.Bytes(1, 20));
        string spaced = string.Join(' ', TestSecrets.Lower(secret).Chunk(4).Select(chunk => new string(chunk)));

        // Act
        var normalized = TotpUri.TryNormalize(spaced, "My Bank", "alice", out string? uri);

        // Assert
        normalized.ShouldBeTrue();
        uri.ShouldBe("otpauth://totp/My%20Bank:alice?secret=" + secret + "&issuer=My%20Bank");
    }

    /// <summary>Default parameters are dropped and non-default parameters are kept.</summary>
    [Fact]
    public void Default_parameters_are_dropped_and_non_default_parameters_are_kept()
    {
        // Arrange
        string secret = TestSecrets.Base32Encode(TestSecrets.Bytes(2, 20));

        // Act
        var defaults = TotpUri.TryNormalize($"otpauth://totp/Acme:bob?secret={secret}&algorithm=SHA1&digits=6&period=30&issuer=Acme", null, null, out string? plain);
        var custom = TotpUri.TryNormalize($"otpauth://totp/bob?secret={secret}&algorithm=sha512&period=45", "Fallback", null, out string? kept);

        // Assert
        defaults.ShouldBeTrue();
        plain.ShouldBe($"otpauth://totp/Acme:bob?secret={secret}&issuer=Acme");
        custom.ShouldBeTrue();
        kept.ShouldBe($"otpauth://totp/Fallback:bob?secret={secret}&issuer=Fallback&algorithm=SHA512&period=45");
    }

    /// <summary>A Steam URI keeps its native steam form.</summary>
    [Fact]
    public void Steam_uri_keeps_native_steam_form()
    {
        // Arrange
        string secret = TestSecrets.Base32Encode(TestSecrets.Bytes(3, 10));

        // Act
        var normalized = TotpUri.TryNormalize("steam://" + TestSecrets.Lower(secret), null, null, out string? uri);

        // Assert
        normalized.ShouldBeTrue();
        uri.ShouldBe("steam://" + secret);
    }

    /// <summary>HOTP URIs and short secrets are not normalized.</summary>
    [Fact]
    public void Hotp_and_short_secrets_are_not_normalized()
    {
        // Arrange
        string secret = TestSecrets.Base32Encode(TestSecrets.Bytes(4, 20));

        // Act
        var hotp = TotpUri.TryNormalize($"otpauth://hotp/x?secret={secret}&counter=3", null, null, out _);
        var shortSecret = TotpUri.TryNormalize("otpauth://totp/x?secret=ABCDEFGH", null, null, out _);
        var isHotp = TotpUri.IsHotp($"otpauth://hotp/x?secret={secret}");

        // Assert
        hotp.ShouldBeFalse();
        isHotp.ShouldBeTrue();
        shortSecret.ShouldBeFalse();
    }

    /// <summary>A migration payload marks MD5 and HOTP entries unsupported and never prints their secrets.</summary>
    [Fact]
    public void Migration_payload_marks_md5_and_hotp_entries_unsupported()
    {
        // Arrange
        string uri = MigrationPayloadBuilder.Uri(
            (TestSecrets.Bytes(5, 20), "a", "A", 4, 1, 2),
            (TestSecrets.Bytes(6, 20), "b", "B", 1, 1, 1),
            (TestSecrets.Bytes(7, 20), "c", "C", 3, 1, 2));

        // Act
        var entries = TotpUri.DecodeMigration(uri);

        // Assert
        entries.Select(entry => entry.IsSupported).ShouldBe([false, false, true]);
        entries[2].Uri.ShouldBe("otpauth://totp/C:c?secret=" + TestSecrets.Base32Encode(TestSecrets.Bytes(7, 20)) + "&issuer=C&algorithm=SHA512");
        entries[0].ToString().ShouldNotContain(TestSecrets.Base32Encode(TestSecrets.Bytes(5, 20)), StringComparison.Ordinal);
    }

    /// <summary>A malformed or truncated migration payload decodes to nothing.</summary>
    [Fact]
    public void Malformed_migration_payload_decodes_to_nothing()
    {
        // Act
        var entries = TotpUri.DecodeMigration("otpauth-migration://offline?data=%%%");
        var truncated = TotpUri.DecodeMigration("otpauth-migration://offline?data=CgQK");

        // Assert
        entries.ShouldBeEmpty();
        truncated.ShouldBeEmpty();
    }
}
