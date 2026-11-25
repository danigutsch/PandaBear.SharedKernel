namespace SharedKernel.Secrets.Tests;

/// <summary>Tests the validated age recipient.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class AgeRecipientTests
{
    [Fact]
    public void An_x25519_recipient_is_kept()
    {
        // Act
        var recipient = AgeRecipient.Create(TestRecipients.First).ShouldBeSuccess();

        // Assert
        recipient.Value.ShouldBe(TestRecipients.First);
        recipient.Kind.ShouldBe(AgeRecipientKind.X25519);
    }

    [Fact]
    public void An_uppercase_recipient_is_stored_in_lowercase_and_equals_the_lowercase_one()
    {
        // Act
        var upper = AgeRecipient.Create(TestRecipients.First.ToUpper(System.Globalization.CultureInfo.InvariantCulture)).ShouldBeSuccess();

        // Assert
        upper.Value.ShouldBe(TestRecipients.First);
        upper.ShouldBe(TestRecipients.Recipient(TestRecipients.First));
    }

    [Fact]
    public void An_ssh_rsa_recipient_is_accepted_and_kept_exactly()
    {
        // Act
        var recipient = AgeRecipient.Create(TestRecipients.SshRsa).ShouldBeSuccess();

        // Assert
        recipient.Kind.ShouldBe(AgeRecipientKind.Ssh);
        recipient.Value.ShouldBe(TestRecipients.SshRsa);
    }

    [Theory]
    [InlineData("ssh-ed25519")]
    [InlineData("ssh-ed25519 ")]
    [InlineData("ssh-dss AAAA")]
    public void An_incomplete_ssh_line_is_rejected(string input)
    {
        // Act
        var result = AgeRecipient.Create(input);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(" comment")]
    [InlineData("  ")]
    public void An_ssh_line_with_a_comment_or_extra_spaces_is_rejected(string suffix)
    {
        // Act
        var result = AgeRecipient.Create(TestRecipients.SshEd25519 + suffix);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void An_ssh_key_that_does_not_match_its_key_type_is_rejected()
    {
        // Act
        var result = AgeRecipient.Create("ssh-rsa " + TestRecipients.SshEd25519["ssh-ed25519 ".Length..]);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void An_ssh_ed25519_recipient_is_accepted_without_a_comment()
    {
        // Act
        var recipient = AgeRecipient.Create(TestRecipients.SshEd25519).ShouldBeSuccess();

        // Assert
        recipient.Kind.ShouldBe(AgeRecipientKind.Ssh);
        recipient.Value.ShouldBe(TestRecipients.SshEd25519);
    }

    [Theory]
    [InlineData("age1yubikey", AgeRecipientKind.Plugin)]
    [InlineData("age1tpm", AgeRecipientKind.Plugin)]
    [InlineData("age1se", AgeRecipientKind.Plugin)]
    [InlineData("age1fido2-hmac", AgeRecipientKind.Plugin)]
    [InlineData("age1pq", AgeRecipientKind.Hybrid)]
    [InlineData("age1tag", AgeRecipientKind.Tag)]
    [InlineData("age1tagpq", AgeRecipientKind.Tag)]
    public void A_bech32_recipient_with_an_allow_listed_human_readable_part_is_accepted(string hrp, AgeRecipientKind kind)
    {
        // Arrange
        var text = Bech32Encoder.Encode(hrp, [1, 2, 3, 4, 5, 6, 7, 8]);

        // Act
        var recipient = AgeRecipient.Create(text).ShouldBeSuccess();

        // Assert
        recipient.Kind.ShouldBe(kind);
        recipient.Value.ShouldBe(text);
    }

    [Fact]
    public void A_valid_bech32_string_with_a_human_readable_part_off_the_allow_list_is_rejected()
    {
        // Arrange
        var text = Bech32Encoder.Encode("age1unknownplugin", [1, 2, 3]);

        // Act
        var result = AgeRecipient.Create(text);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void An_x25519_recipient_with_the_wrong_key_length_is_rejected_even_with_a_valid_checksum()
    {
        // Arrange
        var text = Bech32Encoder.Encode("age", new byte[51]);

        // Act
        var result = AgeRecipient.Create(text);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("age1")]
    [InlineData("age1s6ljywnmxkxndew9qztcjgfa3lsqfwe0rec66a9jjugzuxaagg3sgumkyg")]
    [InlineData("Age1s6ljywnmxkxndew9qztcjgfa3lsqfwe0rec66a9jjugzuxaagg3sgumkyf")]
    [InlineData("ssh-ed25519 !!!notbase64")]
    [InlineData("ssh-dss AAAA")]
    [InlineData("not a recipient")]
    public void An_invalid_recipient_is_rejected_without_echoing_it(string? input)
    {
        // Act
        var result = AgeRecipient.Create(input);

        // Assert
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["recipient"].ShouldHaveSingleItem();
        if (!string.IsNullOrEmpty(input))
        {
            message.ShouldNotContain(input, StringComparison.Ordinal);
            result.ErrorDetails.Detail.ShouldNotContain(input, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(FakeKeys.Secret)]
    [InlineData(FakeKeys.LowercaseSecret)]
    [InlineData("AGE-PLUGIN-YUBIKEY-1QQQQQQQQQQQQ")]
    public void A_private_identity_is_rejected_as_a_recipient_with_a_clear_message(string identity)
    {
        // Act
        var result = AgeRecipient.Create(identity);

        // Assert
        var message = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["recipient"].ShouldHaveSingleItem();
        message.ShouldContain("private age identity", StringComparison.Ordinal);
        message.ShouldNotContain(identity, StringComparison.Ordinal);
    }

    [Fact]
    public void A_recipient_longer_than_the_limit_is_rejected()
    {
        // Act
        var result = AgeRecipient.Create(new string('a', AgeRecipient.MaxLength + 1));

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void The_error_field_name_is_the_one_given()
    {
        // Act
        var result = AgeRecipient.Create("x", "admins[0]");

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull().ContainsKey("admins[0]").ShouldBeTrue();
    }
}
