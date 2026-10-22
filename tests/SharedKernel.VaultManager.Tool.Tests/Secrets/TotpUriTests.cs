using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

public sealed class TotpUriTests
{
    [Fact]
    public void Plain_base32_is_uppercased_stripped_and_wrapped_in_otpauth()
    {
        string secret = TestSecrets.Base32Encode(TestSecrets.Bytes(1, 20));
        string spaced = string.Join(' ', TestSecrets.Lower(secret).Chunk(4).Select(chunk => new string(chunk)));

        var normalized = TotpUri.TryNormalize(spaced, "My Bank", "alice", out string? uri);

        normalized.ShouldBeTrue();
        uri.ShouldBe("otpauth://totp/My%20Bank:alice?secret=" + secret + "&issuer=My%20Bank");
    }

    [Fact]
    public void Default_parameters_are_dropped_and_non_default_parameters_are_kept()
    {
        string secret = TestSecrets.Base32Encode(TestSecrets.Bytes(2, 20));

        var defaults = TotpUri.TryNormalize($"otpauth://totp/Acme:bob?secret={secret}&algorithm=SHA1&digits=6&period=30&issuer=Acme", null, null, out string? plain);
        var custom = TotpUri.TryNormalize($"otpauth://totp/bob?secret={secret}&algorithm=sha512&period=45", "Fallback", null, out string? kept);

        defaults.ShouldBeTrue();
        plain.ShouldBe($"otpauth://totp/Acme:bob?secret={secret}&issuer=Acme");
        custom.ShouldBeTrue();
        kept.ShouldBe($"otpauth://totp/Fallback:bob?secret={secret}&issuer=Fallback&algorithm=SHA512&period=45");
    }

    [Fact]
    public void Steam_uri_keeps_native_steam_form()
    {
        string secret = TestSecrets.Base32Encode(TestSecrets.Bytes(3, 10));

        var normalized = TotpUri.TryNormalize("steam://" + TestSecrets.Lower(secret), null, null, out string? uri);

        normalized.ShouldBeTrue();
        uri.ShouldBe("steam://" + secret);
    }

    [Fact]
    public void Hotp_and_short_secrets_are_not_normalized()
    {
        string secret = TestSecrets.Base32Encode(TestSecrets.Bytes(4, 20));

        var hotp = TotpUri.TryNormalize($"otpauth://hotp/x?secret={secret}&counter=3", null, null, out _);
        var shortSecret = TotpUri.TryNormalize("otpauth://totp/x?secret=ABCDEFGH", null, null, out _);

        hotp.ShouldBeFalse();
        TotpUri.IsHotp($"otpauth://hotp/x?secret={secret}").ShouldBeTrue();
        shortSecret.ShouldBeFalse();
    }

    [Fact]
    public void Migration_payload_marks_md5_and_hotp_entries_unsupported()
    {
        string uri = MigrationPayloadBuilder.Uri(
            (TestSecrets.Bytes(5, 20), "a", "A", 4, 1, 2),
            (TestSecrets.Bytes(6, 20), "b", "B", 1, 1, 1),
            (TestSecrets.Bytes(7, 20), "c", "C", 3, 1, 2));

        var entries = TotpUri.DecodeMigration(uri);

        entries.Select(entry => entry.IsSupported).ShouldBe([false, false, true]);
        entries[2].Uri.ShouldBe("otpauth://totp/C:c?secret=" + TestSecrets.Base32Encode(TestSecrets.Bytes(7, 20)) + "&issuer=C&algorithm=SHA512");
        entries[0].ToString().ShouldNotContain(TestSecrets.Base32Encode(TestSecrets.Bytes(5, 20)), StringComparison.Ordinal);
    }

    [Fact]
    public void Malformed_migration_payload_decodes_to_nothing()
    {
        var entries = TotpUri.DecodeMigration("otpauth-migration://offline?data=%%%");
        var truncated = TotpUri.DecodeMigration("otpauth-migration://offline?data=CgQK");

        entries.ShouldBeEmpty();
        truncated.ShouldBeEmpty();
    }
}
