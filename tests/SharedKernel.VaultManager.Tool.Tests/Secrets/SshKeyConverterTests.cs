using System.Security.Cryptography;
using PandaBear.VaultManager.Tool.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Secrets;

public sealed class SshKeyConverterTests
{
    [Fact]
    public void Ssh_fingerprint_matches_ssh_keygen()
    {
        var conversion = SshKeyConverter.Convert(PemFixtures.Ed25519OpenSsh());

        var key = conversion.Key.ShouldNotBeNull();
        key.KeyFingerprint.ShouldBe(PemFixtures.Ed25519Fingerprint);
        key.PublicKey.ShouldBe(PemFixtures.Ed25519PublicKey);
        key.PrivateKey.ShouldBe(PemFixtures.Ed25519OpenSsh());
        conversion.Problem.ShouldBeNull();
    }

    [Fact]
    public void Ecdsa_pkcs8_key_derives_nistp256_public_key()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var point = ecdsa.ExportParameters(false).Q;

        var key = SshKeyConverter.Convert(PemFixtures.Pkcs8(ecdsa)).Key.ShouldNotBeNull();

        string[] parts = key.PublicKey.ShouldNotBeNull().Split(' ');
        parts[0].ShouldBe("ecdsa-sha2-nistp256");
        byte[] blob = Convert.FromBase64String(parts[1]);
        var strings = SshWireStrings.Read(blob);
        strings.Select(value => value.Length).ShouldBe([19, 8, 65]);
        strings[2].ShouldBe([0x04, .. point.X!, .. point.Y!]);
        key.KeyFingerprint.ShouldBe("SHA256:" + Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('='));
    }

    [Fact]
    public void Rsa_public_key_encodes_exponent_then_modulus_as_mpints()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(false);

        var key = SshKeyConverter.Convert(PemFixtures.Pkcs8(rsa)).Key.ShouldNotBeNull();

        var strings = SshWireStrings.Read(Convert.FromBase64String(key.PublicKey.ShouldNotBeNull().Split(' ')[1]));
        strings[1].ShouldBe(parameters.Exponent!);
        strings[2].ShouldBe([0x00, .. parameters.Modulus!]);
    }

    [Fact]
    public void Ed25519_pkcs8_key_needs_a_supplied_public_key()
    {
        byte[] seed = TestSecrets.Bytes(40, 32);
        string pem = PemFixtures.Wrap("PRIVATE KEY", Convert.ToBase64String([0x30, 0x2e, 0x02, 0x01, 0x00, 0x30, 0x05, 0x06, 0x03, 0x2b, 0x65, 0x70, 0x04, 0x22, 0x04, 0x20, .. seed]));

        var withoutPublic = SshKeyConverter.Convert(pem);
        var withPublic = SshKeyConverter.Convert(pem, PemFixtures.Ed25519PublicKey + " comment");

        withoutPublic.Key.ShouldBeNull();
        withoutPublic.Problem.ShouldNotBeNull().ShouldContain("Ed25519", StringComparison.Ordinal);
        var key = withPublic.Key.ShouldNotBeNull();
        key.PublicKey.ShouldBe(PemFixtures.Ed25519PublicKey);
        key.KeyFingerprint.ShouldBe(PemFixtures.Ed25519Fingerprint);
    }

    [Theory]
    [InlineData("ssh-dss", 64, "DSA")]
    [InlineData("sk-ssh-ed25519@openssh.com", 64, "security key")]
    [InlineData("ssh-ed25519", 6000, "budget")]
    public void Unsupported_or_oversized_keys_are_flagged_not_converted(string keyType, int filler, string reason)
    {
        string pem = OpenSshContainer.Unencrypted(keyType, filler);

        var inspection = SshKeyConverter.Inspect(pem);
        var conversion = SshKeyConverter.Convert(pem);

        inspection.CanConvert.ShouldBeFalse();
        conversion.Key.ShouldBeNull();
        conversion.Problem.ShouldNotBeNull().ShouldContain(reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("RSA PRIVATE KEY", false)]
    [InlineData("RSA PRIVATE KEY", true)]
    [InlineData("EC PRIVATE KEY", false)]
    [InlineData("EC PRIVATE KEY", true)]
    public void Pkcs1_and_sec1_keys_whose_body_is_not_base64_are_flagged_not_converted(string label, bool shortenLine)
    {
        string pem = PemFixtures.WithBrokenBody(PemFixtures.Unencrypted(label), shortenLine);

        var inspection = SshKeyConverter.Inspect(pem);
        var conversion = SshKeyConverter.Convert(pem);

        inspection.CanConvert.ShouldBeFalse();
        inspection.Problem.ShouldBe("key body is not base64");
        conversion.Key.ShouldBeNull();
        conversion.Problem.ShouldBe("key body is not base64");
    }

    [Fact]
    public void Sec1_key_with_nonzero_padding_bits_converts_like_its_canonical_form()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string pem = PemFixtures.Sec1WithNonzeroPaddingBits(ecdsa);

        var conversion = SshKeyConverter.Convert(pem);

        pem.ShouldNotBe(PemFixtures.Sec1(ecdsa));
        conversion.Key.ShouldNotBeNull().PrivateKey.ShouldBe(PemFixtures.Pkcs8(ecdsa));
    }

    [Fact]
    public void Ec_key_on_a_curve_the_platform_does_not_implement_is_flagged_not_converted()
    {
        var conversion = SshKeyConverter.Convert(PemFixtures.Sec1OnUnknownCurve());

        conversion.Key.ShouldBeNull();
        conversion.Problem.ShouldBe("unsupported ECDSA curve");
    }

    [Fact]
    public void Ec_keys_with_explicit_curve_parameters_are_flagged_not_converted()
    {
        using var named = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var explicitCurve = ECDsa.Create(named.ExportExplicitParameters(true));

        var sec1 = SshKeyConverter.Convert(PemFixtures.Sec1(explicitCurve));
        var pkcs8 = SshKeyConverter.Convert(PemFixtures.Pkcs8(explicitCurve));

        sec1.Key.ShouldBeNull();
        sec1.Problem.ShouldBe("unsupported ECDSA curve");
        pkcs8.Key.ShouldBeNull();
        pkcs8.Problem.ShouldBe("unsupported ECDSA curve");
    }

    [Fact]
    public void Encrypted_keys_are_flagged_by_inspection()
    {
        using var rsa = RSA.Create(2048);

        var pkcs8 = SshKeyConverter.Inspect(PemFixtures.EncryptedPkcs8(rsa));
        var openSsh = SshKeyConverter.Inspect(PemFixtures.EncryptedOpenSsh());

        pkcs8.IsEncrypted.ShouldBeTrue();
        openSsh.IsEncrypted.ShouldBeTrue();
        openSsh.CanConvert.ShouldBeFalse();
    }
}
