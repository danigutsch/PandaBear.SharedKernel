using System.Security.Cryptography;
using PandaBear.VaultManager.Tool.Model;
using PandaBear.VaultManager.Tool.Pipeline;
using PandaBear.VaultManager.Tool.Tests.Secrets;

namespace PandaBear.VaultManager.Tool.Tests.Pipeline;

/// <summary>Tests that <see cref="SshKeyNormalizer"/> derives public keys and fingerprints, rewrites keys as PKCS#8, and falls back to secure notes without losing key text.</summary>
public sealed class SshKeyNormalizerTests
{
    /// <summary>An Ed25519 OpenSSH key with a matching stored public key keeps that public key and its comment, gains the derived fingerprint, and produces no extra fields or conversion notes.</summary>
    [Fact]
    public void Normalize_fills_the_fingerprint_and_keeps_a_matching_stored_public_key_with_its_comment()
    {
        // Arrange
        string publicKey = PemFixtures.Ed25519PublicKey + " deploy@example";
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = PemFixtures.Ed25519OpenSsh(), PublicKey = publicKey });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        normalized.Type.ShouldBe(VaultItemType.SshKey);
        var key = normalized.SshKey.ShouldNotBeNull();
        key.KeyFingerprint.ShouldBe(PemFixtures.Ed25519Fingerprint);
        key.PublicKey.ShouldBe(publicKey);
        key.PrivateKey.ShouldBe(PemFixtures.Ed25519OpenSsh());
        normalized.Fields.ShouldBeEmpty();
        normalized.ConversionNotes.ShouldBeEmpty();
    }

    /// <summary>A stored ECDSA public key that does not belong to the Ed25519 private key is replaced by the derived public key, saved in a "Public key (from source)" field, and explained in a conversion note.</summary>
    [Fact]
    public void Normalize_uses_the_derived_public_key_and_keeps_a_stored_one_that_names_another_key()
    {
        // Arrange
        const string OtherKey = "ecdsa-sha2-nistp256 AAAAfixture other@example";
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = PemFixtures.Ed25519OpenSsh(), PublicKey = OtherKey });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        var key = normalized.SshKey.ShouldNotBeNull();
        key.PublicKey.ShouldBe(PemFixtures.Ed25519PublicKey);
        key.KeyFingerprint.ShouldBe(PemFixtures.Ed25519Fingerprint);
        normalized.Fields.ShouldBe([new CustomField("Public key (from source)", OtherKey, CustomFieldType.Text)]);
        normalized.ConversionNotes.ShouldHaveSingleItem().ShouldContain("does not match the private key", StringComparison.Ordinal);
    }

    /// <summary>A stored MD5-style fingerprint that differs from the derived SHA-256 fingerprint is replaced by the derived one and saved in a "Key fingerprint (from source)" field.</summary>
    [Fact]
    public void Normalize_keeps_a_stored_fingerprint_that_differs_from_the_derived_one()
    {
        // Arrange
        const string StoredFingerprint = "MD5:fixture-fingerprint";
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = PemFixtures.Ed25519OpenSsh(), KeyFingerprint = StoredFingerprint });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        var key = normalized.SshKey.ShouldNotBeNull();
        key.PublicKey.ShouldBe(PemFixtures.Ed25519PublicKey);
        key.KeyFingerprint.ShouldBe(PemFixtures.Ed25519Fingerprint);
        normalized.Fields.ShouldBe([new CustomField("Key fingerprint (from source)", StoredFingerprint, CustomFieldType.Text)]);
    }

    /// <summary>A 2048-bit RSA PKCS#1 private key becomes a PKCS#8 key with a derived <c>ssh-rsa</c> public key and SHA256 fingerprint, and one conversion note mentions PKCS#8.</summary>
    [Fact]
    public void Normalize_rewrites_a_pkcs1_rsa_key_as_pkcs8_and_notes_it()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = PemFixtures.RsaPkcs1(rsa) });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        var key = normalized.SshKey.ShouldNotBeNull();
        key.PrivateKey.ShouldStartWith(PemFixtures.Boundary("BEGIN", "PRIVATE KEY"), StringComparison.Ordinal);
        key.PublicKey.ShouldStartWith("ssh-rsa ", StringComparison.Ordinal);
        key.KeyFingerprint.ShouldStartWith("SHA256:", StringComparison.Ordinal);
        normalized.ConversionNotes.ShouldHaveSingleItem().ShouldContain("PKCS#8", StringComparison.Ordinal);
    }

    /// <summary>A "host:" line stored after a PKCS#1 key moves into a hidden "Private key (other text)" field, and the conversion notes name the field without repeating the line.</summary>
    [Fact]
    public void Normalize_keeps_text_stored_after_a_pkcs1_key_in_a_hidden_field()
    {
        // Arrange
        const string HostLine = "host: prod-db-1";
        using var rsa = RSA.Create(2048);
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = PemFixtures.RsaPkcs1(rsa) + "\r\n" + HostLine + "\r\n" });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        var key = normalized.SshKey.ShouldNotBeNull();
        key.PrivateKey.ShouldBe(PemFixtures.Pkcs8(rsa));
        normalized.Fields.ShouldBe([new CustomField("Private key (other text)", HostLine, CustomFieldType.Hidden)]);
        normalized.ConversionNotes.ShouldContain(note => note.Contains("\"Private key (other text)\"", StringComparison.Ordinal));
        normalized.ConversionNotes.ShouldAllSatisfy(note => note.ShouldNotContain(HostLine, StringComparison.Ordinal));
    }

    /// <summary>When an RSA key follows a SEC1 P-256 key, only the first is converted to PKCS#8 with an <c>ecdsa-sha2-nistp256</c> public key, and the second goes unaltered into the hidden other-text field without appearing in notes.</summary>
    [Fact]
    public void Normalize_keeps_a_second_key_stored_after_a_sec1_key_in_a_hidden_field()
    {
        // Arrange
        using var first = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var second = RSA.Create(2048);
        string secondKey = PemFixtures.RsaPkcs1(second);
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = PemFixtures.Sec1(first) + "\n\n" + secondKey });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        var key = normalized.SshKey.ShouldNotBeNull();
        key.PrivateKey.ShouldBe(PemFixtures.Pkcs8(first));
        key.PublicKey.ShouldStartWith("ecdsa-sha2-nistp256 ", StringComparison.Ordinal);
        normalized.Fields.ShouldBe([new CustomField("Private key (other text)", secondKey, CustomFieldType.Hidden)]);
        normalized.ConversionNotes.ShouldAllSatisfy(note => note.ShouldNotContain(secondKey.Split('\n')[1], StringComparison.Ordinal));
    }

    /// <summary>A label line before an OpenSSH Ed25519 key is moved to the hidden other-text field so the key slot holds exactly the key block.</summary>
    [Fact]
    public void Normalize_keeps_only_the_first_key_block_in_the_key_slot_whatever_its_format()
    {
        // Arrange
        const string Label = "deploy key for git.example.com";
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = Label + "\n" + PemFixtures.Ed25519OpenSsh() });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        var key = normalized.SshKey.ShouldNotBeNull();
        key.PrivateKey.ShouldBe(PemFixtures.Ed25519OpenSsh());
        key.KeyFingerprint.ShouldBe(PemFixtures.Ed25519Fingerprint);
        normalized.Fields.ShouldBe([new CustomField("Private key (other text)", Label, CustomFieldType.Hidden)]);
        normalized.ConversionNotes.ShouldHaveSingleItem().ShouldContain("\"Private key (other text)\"", StringComparison.Ordinal);
    }

    /// <summary>A label before and a host line after the key are joined with LF line endings into one hidden other-text field.</summary>
    [Fact]
    public void Normalize_keeps_text_stored_before_and_after_the_key_in_one_hidden_field()
    {
        // Arrange
        const string Label = "deploy key for git.example.com";
        const string HostLine = "host: prod-db-1";
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = Label + "\r\n\r\n" + PemFixtures.Ed25519OpenSsh() + "\r\n" + HostLine + "\r\n" });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        var key = normalized.SshKey.ShouldNotBeNull();
        key.PrivateKey.ShouldBe(PemFixtures.Ed25519OpenSsh());
        key.KeyFingerprint.ShouldBe(PemFixtures.Ed25519Fingerprint);
        normalized.Fields.ShouldBe([new CustomField(SshKeyNormalizer.OtherTextField, Label + "\n" + HostLine, CustomFieldType.Hidden)]);
    }

    /// <summary>An encrypted OpenSSH key followed by an RSA key makes a secure note whose hidden private-key field holds the whole stored text unchanged.</summary>
    [Fact]
    public void Normalize_keeps_both_keys_when_the_first_key_cannot_be_converted()
    {
        // Arrange
        using var second = RSA.Create(2048);
        string stored = PemFixtures.EncryptedOpenSsh() + "\n" + PemFixtures.RsaPkcs1(second);
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = stored });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        normalized.Type.ShouldBe(VaultItemType.SecureNote);
        normalized.Fields.ShouldBe([new CustomField(SshKeyNormalizer.PrivateKeyField, stored, CustomFieldType.Hidden)]);
    }

    /// <summary>An SSH key item that already has a public key and fingerprint is returned as the same instance even when its private-key text has a label and a second key around the first.</summary>
    [Fact]
    public void Normalize_leaves_text_around_the_key_of_a_complete_ssh_key_item_in_its_key_slot()
    {
        // Arrange
        using var second = RSA.Create(2048);
        string stored = "deploy key\n" + PemFixtures.Ed25519OpenSsh() + "\n" + PemFixtures.RsaPkcs1(second);
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = stored, PublicKey = PemFixtures.Ed25519PublicKey, KeyFingerprint = PemFixtures.Ed25519Fingerprint });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        normalized.ShouldBeSameAs(item);
        normalized.SshKey.ShouldNotBeNull().PrivateKey.ShouldBe(stored);
    }

    /// <summary>A SEC1 key with explicit curve parameters becomes a secure note with the PEM kept in a hidden field and a note about the unsupported ECDSA curve that repeats no PEM line.</summary>
    [Fact]
    public void Normalize_turns_a_sec1_key_with_explicit_curve_parameters_into_a_secure_note_that_keeps_the_key()
    {
        // Arrange
        using var named = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var explicitCurve = ECDsa.Create(named.ExportExplicitParameters(includePrivateParameters: true));
        string pem = PemFixtures.Sec1(explicitCurve);
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = pem });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        normalized.Type.ShouldBe(VaultItemType.SecureNote);
        normalized.SshKey.ShouldBeNull();
        normalized.Fields.ShouldBe([new CustomField(SshKeyNormalizer.PrivateKeyField, pem, CustomFieldType.Hidden)]);
        string note = normalized.ConversionNotes.ShouldHaveSingleItem();
        note.ShouldContain("unsupported ECDSA curve", StringComparison.Ordinal);
        pem.Split('\n').ShouldAllSatisfy(line => note.ShouldNotContain(line, StringComparison.Ordinal));
    }

    /// <summary>An encrypted OpenSSH key becomes a secure note that keeps the existing Hostname field and adds hidden private-key and public-key fields, with a note that encrypted keys are not converted and no key line in it.</summary>
    [Fact]
    public void Normalize_turns_a_key_that_cannot_be_converted_into_a_secure_note_that_keeps_the_key()
    {
        // Arrange
        string pem = PemFixtures.EncryptedOpenSsh();
        var host = new CustomField("Hostname", "git.example.com", CustomFieldType.Text);
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = pem, PublicKey = "ssh-ed25519 AAAAfixture" }) with
        {
            Fields = [host]
        };

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        normalized.Type.ShouldBe(VaultItemType.SecureNote);
        normalized.SshKey.ShouldBeNull();
        normalized.Fields.ShouldBe(
        [
            host,
            new CustomField(SshKeyNormalizer.PrivateKeyField, pem, CustomFieldType.Hidden),
            new CustomField(SshKeyNormalizer.PublicKeyField, "ssh-ed25519 AAAAfixture", CustomFieldType.Text),
        ]);
        string note = normalized.ConversionNotes.ShouldHaveSingleItem();
        note.ShouldContain("encrypted keys are not converted", StringComparison.Ordinal);
        note.ShouldNotContain(pem.Split('\n')[1], StringComparison.Ordinal);
    }

    /// <summary>A PKCS#1 RSA or SEC1 EC key block whose body is not valid base64 becomes a secure note keeping the PEM in a hidden field, with a "key body is not base64" note that repeats no PEM line.</summary>
    [Theory]
    [InlineData("RSA PRIVATE KEY")]
    [InlineData("EC PRIVATE KEY")]
    public void Normalize_turns_a_pkcs1_or_sec1_key_whose_body_is_not_base64_into_a_secure_note_that_keeps_the_key(string label)
    {
        // Arrange
        string pem = PemFixtures.WithBrokenBody(PemFixtures.Unencrypted(label), shortenLine: false);
        var item = SshKeyItems.Item(new SshKeyData { PrivateKey = pem });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        normalized.Type.ShouldBe(VaultItemType.SecureNote);
        normalized.SshKey.ShouldBeNull();
        normalized.Fields.ShouldBe([new CustomField(SshKeyNormalizer.PrivateKeyField, pem, CustomFieldType.Hidden)]);
        string note = normalized.ConversionNotes.ShouldHaveSingleItem();
        note.ShouldContain("key body is not base64", StringComparison.Ordinal);
        pem.Split('\n').ShouldAllSatisfy(line => note.ShouldNotContain(line, StringComparison.Ordinal));
    }

    /// <summary>An SSH key item holding only a public key becomes a secure note with that public key in a text field and a "no private key" conversion note.</summary>
    [Fact]
    public void Normalize_turns_an_ssh_key_item_without_a_private_key_into_a_secure_note()
    {
        // Arrange
        var item = SshKeyItems.Item(new SshKeyData { PublicKey = PemFixtures.Ed25519PublicKey });

        // Act
        var normalized = SshKeyNormalizer.Normalize(item);

        // Assert
        normalized.Type.ShouldBe(VaultItemType.SecureNote);
        normalized.Fields.ShouldBe([new CustomField(SshKeyNormalizer.PublicKeyField, PemFixtures.Ed25519PublicKey, CustomFieldType.Text)]);
        normalized.ConversionNotes.ShouldHaveSingleItem().ShouldContain("no private key", StringComparison.Ordinal);
    }

    /// <summary>An SSH key item with private key, public key, and fingerprint already set, and a secure note, are each returned as the same instance.</summary>
    [Fact]
    public void Normalize_leaves_complete_ssh_keys_and_other_items_unchanged()
    {
        // Arrange
        var complete = SshKeyItems.Item(new SshKeyData { PrivateKey = "fixture-private", PublicKey = "fixture-public", KeyFingerprint = "fixture-fingerprint" });
        var note = complete with
        {
            Type = VaultItemType.SecureNote,
            SshKey = null
        };

        // Act
        var normalizedKey = SshKeyNormalizer.Normalize(complete);
        var normalizedNote = SshKeyNormalizer.Normalize(note);

        // Assert
        normalizedKey.ShouldBeSameAs(complete);
        normalizedNote.ShouldBeSameAs(note);
    }
}
