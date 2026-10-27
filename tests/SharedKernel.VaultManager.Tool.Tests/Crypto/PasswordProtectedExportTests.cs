using System.Text.Json.Nodes;
using Konscious.Security.Cryptography;
using PandaBear.VaultManager.Tool.Crypto;
using PandaBear.VaultManager.Tool.Formats.Bitwarden;

namespace PandaBear.VaultManager.Tool.Tests.Crypto;

/// <summary>Tests the encryption and decryption of Bitwarden password-protected exports.</summary>
public sealed class PasswordProtectedExportTests
{
    private const string FixturePassword = "fixture password";
    private const string EmptyExport = """{"encrypted":false,"items":[]}""";
    private static readonly KdfConfig FastPbkdf2 = new(KdfType.Pbkdf2Sha256, 5_000);
    private static readonly KdfConfig SmallArgon2id = new(KdfType.Argon2id, 2, 16, 1);

    /// <summary>Encrypting an empty export with a 5,000-iteration PBKDF2 setting and decrypting it with the same password returns the original JSON.</summary>
    [Fact]
    public void Encrypt_then_decrypt_with_pbkdf2_returns_original_json()
    {
        // Act
        var envelope = PasswordProtectedExport.Encrypt(EmptyExport, "secret", FastPbkdf2);
        var decrypted = PasswordProtectedExport.Decrypt(envelope, "secret");

        // Assert
        decrypted.ShouldBe(EmptyExport);
    }

    /// <summary>Encrypting an empty export with a small Argon2id setting and decrypting it with the same password returns the original JSON.</summary>
    [Fact]
    public void Encrypt_then_decrypt_with_argon2id_returns_original_json()
    {
        // Act
        var envelope = PasswordProtectedExport.Encrypt(EmptyExport, "secret", SmallArgon2id);
        var decrypted = PasswordProtectedExport.Decrypt(envelope, "secret");

        // Assert
        decrypted.ShouldBe(EmptyExport);
    }

    /// <summary>The envelope is flagged encrypted and password protected, records the Argon2id kind and 16 memory, and holds validation and data strings that start with "2.".</summary>
    [Fact]
    public void Encrypt_writes_bitwarden_envelope_fields()
    {
        // Act
        var envelope = PasswordProtectedExport.Encrypt("{}", "secret", SmallArgon2id);

        // Assert
        var document = JsonNode.Parse(envelope).ShouldNotBeNull();
        document["encrypted"].ShouldNotBeNull().GetValue<bool>().ShouldBeTrue();
        document["passwordProtected"].ShouldNotBeNull().GetValue<bool>().ShouldBeTrue();
        document["kdfType"].ShouldNotBeNull().GetValue<int>().ShouldBe(1);
        document["kdfMemory"].ShouldNotBeNull().GetValue<int>().ShouldBe(16);
        document["encKeyValidation_DO_NOT_EDIT"].ShouldNotBeNull().GetValue<string>().ShouldStartWith("2.", StringComparison.Ordinal);
        document["data"].ShouldNotBeNull().GetValue<string>().ShouldStartWith("2.", StringComparison.Ordinal);
    }

    /// <summary>Decrypting the PBKDF2 and Argon2id fixtures that were generated outside this code with the fixture password returns the plain-small.json content.</summary>
    /// <param name="fixture">The fixture file of the password-protected export.</param>
    [Theory]
    [InlineData("bitwarden/protected-pbkdf2.json")]
    [InlineData("bitwarden/protected-argon2id.json")]
    public void Decrypt_independently_generated_export_returns_plain_JSON(string fixture)
    {
        // Act
        var decrypted = PasswordProtectedExport.Decrypt(Fixture.Read(fixture), FixturePassword);

        // Assert
        decrypted.ShouldBe(Fixture.Read("bitwarden/plain-small.json"));
    }

    /// <summary>Decrypting with a wrong password throws InvalidDataException whose message says the password is wrong.</summary>
    [Fact]
    public void Decrypt_with_wrong_password_throws_invalid_data()
    {
        // Arrange
        var envelope = Fixture.Read("bitwarden/protected-pbkdf2.json");
        var action = () => PasswordProtectedExport.Decrypt(envelope, "wrong password");

        // Act
        var exception = action.ShouldThrow<InvalidDataException>();

        // Assert
        exception.Message.ShouldContain("password is wrong", StringComparison.Ordinal);
    }

    /// <summary>Flipping one bit of the ciphertext in the data field makes decryption with the correct password throw InvalidDataException.</summary>
    [Fact]
    public void Decrypt_with_tampered_data_throws_invalid_data()
    {
        // Arrange
        var document = JsonNode.Parse(Fixture.Read("bitwarden/protected-pbkdf2.json")).ShouldNotBeNull();
        var parts = document["data"].ShouldNotBeNull().GetValue<string>()[2..].Split('|');
        var ciphertext = Convert.FromBase64String(parts[1]);
        ciphertext[0] ^= 0x01;
        document["data"] = $"2.{parts[0]}|{Convert.ToBase64String(ciphertext)}|{parts[2]}";
        var tampered = document.ToJsonString();
        var action = () => PasswordProtectedExport.Decrypt(tampered, FixturePassword);

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }

    /// <summary>Envelopes whose Argon2id memory, iterations, or parallelism, or whose PBKDF2 iterations, are out of range throw InvalidDataException.</summary>
    /// <param name="kdfType">The key derivation type written into the envelope.</param>
    /// <param name="iterations">The iteration count written into the envelope.</param>
    /// <param name="memory">The Argon2id memory written into the envelope, or null.</param>
    /// <param name="parallelism">The Argon2id parallelism written into the envelope, or null.</param>
    [Theory]
    [InlineData(1, 2, 1_000_000, 1)]
    [InlineData(1, 1, 16, 1)]
    [InlineData(1, 2, 16, 64)]
    [InlineData(0, 4_999, null, null)]
    [InlineData(0, 2_000_001, null, null)]
    public void Decrypt_rejects_out_of_range_kdf_settings_before_deriving(int kdfType, int iterations, int? memory, int? parallelism)
    {
        // Arrange
        var document = JsonNode.Parse(Fixture.Read("bitwarden/protected-argon2id.json")).ShouldNotBeNull();
        document["kdfType"] = kdfType;
        document["kdfIterations"] = iterations;
        document["kdfMemory"] = memory;
        document["kdfParallelism"] = parallelism;
        var unsafeEnvelope = document.ToJsonString();
        var action = () => PasswordProtectedExport.Decrypt(unsafeEnvelope, FixturePassword);

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }

    /// <summary>Decrypting a plain, unencrypted export throws InvalidDataException.</summary>
    [Fact]
    public void Decrypt_rejects_files_that_are_not_password_protected()
    {
        // Arrange
        var plain = Fixture.Read("bitwarden/plain-small.json");
        var action = () => PasswordProtectedExport.Decrypt(plain, "x");

        // Act, Assert
        action.ShouldThrow<InvalidDataException>();
    }

    /// <summary>The Argon2id library derives the tag 0d640df5... from the RFC 9106 section 5.3 inputs.</summary>
    [Fact]
    public void Argon2id_library_matches_rfc_9106_test_vector()
    {
        // Arrange
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

        // Act
        var tag = Convert.ToHexStringLower(argon2.GetBytes(32));

        // Assert
        tag.ShouldBe("0d640df58d78766c08c037a34a8b53c9d01ef0452d75b65eb52520e96b01e659");
    }
}
