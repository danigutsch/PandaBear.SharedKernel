using System.Text.Json.Nodes;
using Konscious.Security.Cryptography;
using PandaBear.VaultManager.Tool.Crypto;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;

namespace PandaBear.VaultManager.Tool.Tests.Crypto;

public sealed class PasswordProtectedExportTests
{
    private const string FixturePassword = "fixture password";
    private const string EmptyExport = """{"encrypted":false,"items":[]}""";
    private static readonly KdfConfig FastPbkdf2 = new(KdfType.Pbkdf2Sha256, 5_000);
    private static readonly KdfConfig SmallArgon2id = new(KdfType.Argon2id, 2, 16, 1);

    [Fact]
    public void Encrypt_then_decrypt_with_pbkdf2_returns_original_json()
    {
        var envelope = PasswordProtectedExport.Encrypt(EmptyExport, "secret", FastPbkdf2);

        PasswordProtectedExport.Decrypt(envelope, "secret").ShouldBe(EmptyExport);
    }

    [Fact]
    public void Encrypt_then_decrypt_with_argon2id_returns_original_json()
    {
        var envelope = PasswordProtectedExport.Encrypt(EmptyExport, "secret", SmallArgon2id);

        PasswordProtectedExport.Decrypt(envelope, "secret").ShouldBe(EmptyExport);
    }

    [Fact]
    public void Encrypt_writes_bitwarden_envelope_fields()
    {
        var document = JsonNode.Parse(PasswordProtectedExport.Encrypt("{}", "secret", SmallArgon2id)).ShouldNotBeNull();

        document["encrypted"].ShouldNotBeNull().GetValue<bool>().ShouldBeTrue();
        document["passwordProtected"].ShouldNotBeNull().GetValue<bool>().ShouldBeTrue();
        document["kdfType"].ShouldNotBeNull().GetValue<int>().ShouldBe(1);
        document["kdfMemory"].ShouldNotBeNull().GetValue<int>().ShouldBe(16);
        document["encKeyValidation_DO_NOT_EDIT"].ShouldNotBeNull().GetValue<string>().ShouldStartWith("2.", StringComparison.Ordinal);
        document["data"].ShouldNotBeNull().GetValue<string>().ShouldStartWith("2.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("bitwarden/protected-pbkdf2.json")]
    [InlineData("bitwarden/protected-argon2id.json")]
    public void Decrypt_independently_generated_export_returns_plain_JSON(string fixture)
    {
        var decrypted = PasswordProtectedExport.Decrypt(Fixture.Read(fixture), FixturePassword);

        decrypted.ShouldBe(Fixture.Read("bitwarden/plain-small.json"));
    }

    [Fact]
    public void Decrypt_with_wrong_password_throws_invalid_data()
    {
        var envelope = Fixture.Read("bitwarden/protected-pbkdf2.json");

        var action = () => PasswordProtectedExport.Decrypt(envelope, "wrong password");

        var exception = action.ShouldThrow<InvalidDataException>();

        exception.Message.ShouldContain("password is wrong", StringComparison.Ordinal);
    }

    [Fact]
    public void Decrypt_with_tampered_data_throws_invalid_data()
    {
        var document = JsonNode.Parse(Fixture.Read("bitwarden/protected-pbkdf2.json")).ShouldNotBeNull();
        var parts = document["data"].ShouldNotBeNull().GetValue<string>()[2..].Split('|');
        var ciphertext = Convert.FromBase64String(parts[1]);
        ciphertext[0] ^= 0x01;
        document["data"] = $"2.{parts[0]}|{Convert.ToBase64String(ciphertext)}|{parts[2]}";
        var tampered = document.ToJsonString();

        var action = () => PasswordProtectedExport.Decrypt(tampered, FixturePassword);

        action.ShouldThrow<InvalidDataException>();
    }

    [Theory]
    [InlineData(1, 2, 1_000_000, 1)]
    [InlineData(1, 1, 16, 1)]
    [InlineData(1, 2, 16, 64)]
    [InlineData(0, 4_999, null, null)]
    [InlineData(0, 2_000_001, null, null)]
    public void Decrypt_rejects_out_of_range_kdf_settings_before_deriving(int kdfType, int iterations, int? memory, int? parallelism)
    {
        var document = JsonNode.Parse(Fixture.Read("bitwarden/protected-argon2id.json")).ShouldNotBeNull();
        document["kdfType"] = kdfType;
        document["kdfIterations"] = iterations;
        document["kdfMemory"] = memory;
        document["kdfParallelism"] = parallelism;
        var unsafeEnvelope = document.ToJsonString();

        var action = () => PasswordProtectedExport.Decrypt(unsafeEnvelope, FixturePassword);

        action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Decrypt_rejects_files_that_are_not_password_protected()
    {
        var plain = Fixture.Read("bitwarden/plain-small.json");

        var action = () => PasswordProtectedExport.Decrypt(plain, "x");

        action.ShouldThrow<InvalidDataException>();
    }

    [Fact]
    public void Argon2id_library_matches_rfc_9106_test_vector()
    {
        // RFC 9106 section 5.3.
        using var argon2 = new Argon2id(Enumerable.Repeat((byte)0x01, 32).ToArray())
        {
            Salt = Enumerable.Repeat((byte)0x02, 16).ToArray(),
            KnownSecret = Enumerable.Repeat((byte)0x03, 8).ToArray(),
            AssociatedData = Enumerable.Repeat((byte)0x04, 12).ToArray(),
            Iterations = 3,
            MemorySize = 32,
            DegreeOfParallelism = 4,
        };

        var tag = Convert.ToHexStringLower(argon2.GetBytes(32));

        tag.ShouldBe("0d640df58d78766c08c037a34a8b53c9d01ef0452d75b65eb52520e96b01e659");
    }
}
