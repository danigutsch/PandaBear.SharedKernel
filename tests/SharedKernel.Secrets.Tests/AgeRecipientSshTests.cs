namespace PandaBear.Secrets.Tests;

/// <summary>Tests that an SSH recipient is one exact, canonical key and nothing else.</summary>
[Trait("Capability", "Secrets")]
public sealed class AgeRecipientSshTests
{
    [Fact]
    public void A_canonical_ed25519_key_is_accepted_and_kept_in_one_form()
    {
        // Arrange
        var line = SshKeyBlobs.Line("ssh-ed25519", SshKeyBlobs.Blob("ssh-ed25519", SshKeyBlobs.Str(new byte[32])));

        // Act
        var recipient = AgeRecipient.Create(line).ShouldBeSuccess();

        // Assert
        recipient.Value.ShouldBe(line);
        recipient.Kind.ShouldBe(AgeRecipientKind.Ssh);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(0)]
    public void An_ed25519_key_of_the_wrong_length_is_rejected(int length)
    {
        // Act
        var result = AgeRecipient.Create(SshKeyBlobs.Line("ssh-ed25519", SshKeyBlobs.Blob("ssh-ed25519", SshKeyBlobs.Str(new byte[length]))));

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Trailing_bytes_after_the_key_blob_are_rejected()
    {
        // Arrange
        var blob = SshKeyBlobs.Blob("ssh-ed25519", SshKeyBlobs.Str(new byte[32])).Concat(new byte[] { 1, 2, 3 }).ToArray();

        // Act
        var ed25519 = AgeRecipient.Create(SshKeyBlobs.Line("ssh-ed25519", blob));
        var rsa = AgeRecipient.Create(SshKeyBlobs.Line("ssh-rsa", SshKeyBlobs.Blob("ssh-rsa", SshKeyBlobs.Str([1, 0, 1]), SshKeyBlobs.Str([0, 0x80, 0, 1])).Concat(new byte[] { 0 }).ToArray()));

        // Assert
        ed25519.IsFailure.ShouldBeTrue();
        rsa.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\t")]
    [InlineData("\u00a0")]
    [InlineData("\u2003")]
    public void Whitespace_anywhere_in_the_key_is_rejected(string whitespace)
    {
        // Arrange
        var good = SshKeyBlobs.Line("ssh-ed25519", SshKeyBlobs.Blob("ssh-ed25519", SshKeyBlobs.Str(new byte[32])));

        // Act
        var inside = AgeRecipient.Create(good.Insert(30, whitespace));
        var trailing = AgeRecipient.Create(good + whitespace);
        var leading = AgeRecipient.Create(whitespace + good);

        // Assert
        inside.IsFailure.ShouldBeTrue();
        trailing.IsFailure.ShouldBeTrue();
        leading.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Non_canonical_base64_is_rejected()
    {
        // Arrange
        var good = SshKeyBlobs.Line("ssh-rsa", SshKeyBlobs.Blob("ssh-rsa", SshKeyBlobs.Str([1, 0, 1]), SshKeyBlobs.Str([0, 0x80, 0, 1])));
        var unpadded = good.TrimEnd('=');

        // Act
        var result = AgeRecipient.Create(unpadded);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Base64_with_non_zero_padding_bits_is_rejected_because_it_is_not_the_canonical_spelling()
    {
        // Arrange
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        var good = SshKeyBlobs.Line("ssh-rsa", SshKeyBlobs.Blob("ssh-rsa", SshKeyBlobs.Str([1, 0, 1]), SshKeyBlobs.Str([0, 0x80, 0, 1])));
        var lastIndex = good.Length - 2;
        var bumped = alphabet[alphabet.IndexOf(good[lastIndex], StringComparison.Ordinal) + 1];
        var variant = good[..lastIndex] + bumped + "=";

        // Act
        var original = AgeRecipient.Create(good);
        var result = AgeRecipient.Create(variant);

        // Assert
        original.IsSuccess.ShouldBeTrue();
        Convert.FromBase64String(variant.Split(' ')[1]).ShouldBe(Convert.FromBase64String(good.Split(' ')[1]));
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void An_rsa_key_must_be_two_well_formed_positive_integers()
    {
        // Assert
        AgeRecipient.Create(SshKeyBlobs.Line("ssh-rsa", SshKeyBlobs.Blob("ssh-rsa", SshKeyBlobs.Str([1, 0, 1]), SshKeyBlobs.Str([0, 0x80, 0, 1])))).IsSuccess.ShouldBeTrue();
        AgeRecipient.Create(SshKeyBlobs.Line("ssh-rsa", SshKeyBlobs.Blob("ssh-rsa", SshKeyBlobs.Str([1, 0, 1])))).IsFailure.ShouldBeTrue();
        AgeRecipient.Create(SshKeyBlobs.Line("ssh-rsa", SshKeyBlobs.Blob("ssh-rsa", SshKeyBlobs.Str([1, 0, 1]), SshKeyBlobs.Str([0x80, 0, 1])))).IsFailure.ShouldBeTrue();
        AgeRecipient.Create(SshKeyBlobs.Line("ssh-rsa", SshKeyBlobs.Blob("ssh-rsa", SshKeyBlobs.Str([0, 0, 1]), SshKeyBlobs.Str([0, 0x80, 0, 1])))).IsFailure.ShouldBeTrue();
        AgeRecipient.Create(SshKeyBlobs.Line("ssh-rsa", SshKeyBlobs.Blob("ssh-rsa", SshKeyBlobs.Str([]), SshKeyBlobs.Str([0, 0x80, 0, 1])))).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void A_length_prefix_that_runs_past_the_blob_is_rejected()
    {
        // Arrange
        var blob = new byte[] { 0, 0, 0, 11, 0x73, 0x73, 0x68 };

        // Act
        var result = AgeRecipient.Create(SshKeyBlobs.Line("ssh-ed25519", blob));

        // Assert
        result.IsFailure.ShouldBeTrue();
    }
}
