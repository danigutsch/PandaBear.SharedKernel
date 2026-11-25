namespace PandaBear.Secrets.Tests;

/// <summary>Tests that a file's recipients must equal the rule's recipient set exactly.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class RecipientVerificationTests
{
    private static readonly SopsCreationRule Rule = TestRecipients.Rule("x", 2, TestRecipients.First, TestRecipients.Second);

    [Fact]
    public void The_same_recipients_are_exact()
    {
        // Act
        var verification = VerificationChecks.Verify(Rule, TestRecipients.First, TestRecipients.Second);

        // Assert
        verification.IsExact.ShouldBeTrue();
    }

    [Fact]
    public void A_reordered_set_is_exact()
    {
        // Act
        var verification = VerificationChecks.Verify(Rule, TestRecipients.Second, TestRecipients.First);

        // Assert
        verification.IsExact.ShouldBeTrue();
    }

    [Fact]
    public void A_subset_is_not_exact_and_names_the_missing_recipient()
    {
        // Act
        var verification = VerificationChecks.Verify(Rule, TestRecipients.First);

        // Assert
        verification.IsExact.ShouldBeFalse();
        verification.Missing.Select(static r => r.Value).ShouldBe([TestRecipients.Second]);
        verification.Unexpected.ShouldBeEmpty();
    }

    [Fact]
    public void A_superset_is_not_exact_and_names_the_unexpected_recipient()
    {
        // Act
        var verification = VerificationChecks.Verify(Rule, TestRecipients.First, TestRecipients.Second, TestRecipients.Third);

        // Assert
        verification.IsExact.ShouldBeFalse();
        verification.Unexpected.Select(static r => r.Value).ShouldBe([TestRecipients.Third]);
        verification.Missing.ShouldBeEmpty();
    }

    [Fact]
    public void A_different_set_of_the_same_size_is_not_exact()
    {
        // Act
        var verification = VerificationChecks.Verify(Rule, TestRecipients.First, TestRecipients.Third);

        // Assert
        verification.IsExact.ShouldBeFalse();
        verification.Missing.Count.ShouldBe(1);
        verification.Unexpected.Count.ShouldBe(1);
    }

    [Fact]
    public void A_file_with_no_age_recipients_is_not_exact()
    {
        // Act
        var verification = RecipientVerification.Compare(new SopsFileRecipients([], []), Rule);

        // Assert
        verification.IsExact.ShouldBeFalse();
        verification.Missing.Count.ShouldBe(2);
    }

    [Fact]
    public void Matching_age_recipients_do_not_hide_a_non_age_key_holder()
    {
        // Arrange
        var holders = new SopsFileRecipients([TestRecipients.Recipient(TestRecipients.First), TestRecipients.Recipient(TestRecipients.Second)], ["kms"]);

        // Act
        var verification = RecipientVerification.Compare(holders, Rule);

        // Assert
        verification.IsExact.ShouldBeFalse();
        verification.OtherKeyTypes.ShouldBe(["kms"]);
    }

    [Fact]
    public void Compare_rejects_null_arguments()
    {
        // Act
        Action noHolders = () => RecipientVerification.Compare(null!, Rule);
        Action noRule = () => RecipientVerification.Compare(new SopsFileRecipients([], []), null!);

        // Assert
        noHolders.ShouldThrow<ArgumentNullException>();
        noRule.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void The_description_of_a_file_rejects_null_and_orders_its_content()
    {
        // Act
        Action noAge = () => _ = new SopsFileRecipients(null!, []);
        Action noOther = () => _ = new SopsFileRecipients([], null!);
        var holders = new SopsFileRecipients([TestRecipients.Recipient(TestRecipients.First), TestRecipients.Recipient(TestRecipients.Second)], ["pgp", "kms"]);

        // Assert
        noAge.ShouldThrow<ArgumentNullException>();
        noOther.ShouldThrow<ArgumentNullException>();
        holders.OtherKeyTypes.ShouldBe(["kms", "pgp"]);
        holders.Age.Select(static r => r.Value).ShouldBe([TestRecipients.Second, TestRecipients.First]);
    }
}
