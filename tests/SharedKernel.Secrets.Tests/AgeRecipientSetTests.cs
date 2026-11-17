namespace PandaBear.Secrets.Tests;

/// <summary>Tests the recipient set and its order-insensitive equality.</summary>
[Trait("Capability", "Secrets")]
public sealed class AgeRecipientSetTests
{
    [Fact]
    public void A_reordered_set_is_equal()
    {
        // Act
        var first = TestRecipients.Set(TestRecipients.First, TestRecipients.Second);
        var reordered = TestRecipients.Set(TestRecipients.Second, TestRecipients.First);

        // Assert
        reordered.ShouldBe(first);
        reordered.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void A_subset_and_a_superset_are_not_equal()
    {
        // Arrange
        var set = TestRecipients.Set(TestRecipients.First, TestRecipients.Second);

        // Assert
        TestRecipients.Set(TestRecipients.First).ShouldNotBe(set);
        TestRecipients.Set(TestRecipients.First, TestRecipients.Second, TestRecipients.Third).ShouldNotBe(set);
        TestRecipients.Set(TestRecipients.First, TestRecipients.Third).ShouldNotBe(set);
    }

    [Fact]
    public void Sets_that_share_their_first_recipient_but_differ_later_are_not_equal()
    {
        // Arrange
        var secondAndThird = TestRecipients.Set(TestRecipients.Third, TestRecipients.Second);

        // Assert
        TestRecipients.Set(TestRecipients.Third, TestRecipients.First).ShouldNotBe(secondAndThird);
        TestRecipients.Set(TestRecipients.Third, TestRecipients.Second, TestRecipients.First).ShouldNotBe(secondAndThird);
        TestRecipients.Set(TestRecipients.Third).ShouldNotBe(secondAndThird);
    }

    [Fact]
    public void The_recipients_are_ordered_by_text()
    {
        // Act
        var set = TestRecipients.Set(TestRecipients.Second, TestRecipients.Third, TestRecipients.First);

        // Assert
        set.Recipients.Select(static r => r.Value).ShouldBe([TestRecipients.Third, TestRecipients.Second, TestRecipients.First]);
        set.Count.ShouldBe(3);
    }

    [Fact]
    public void Except_returns_the_recipients_the_other_set_lacks()
    {
        // Arrange
        var all = TestRecipients.Set(TestRecipients.First, TestRecipients.Second, TestRecipients.Third);
        var some = TestRecipients.Set(TestRecipients.Second);

        // Act
        var rest = all.Except(some);

        // Assert
        rest.Select(static r => r.Value).ShouldBe([TestRecipients.Third, TestRecipients.First]);
        all.Contains(TestRecipients.Recipient(TestRecipients.Second)).ShouldBeTrue();
        some.Contains(TestRecipients.Recipient(TestRecipients.First)).ShouldBeFalse();
    }

    [Fact]
    public void A_null_empty_duplicated_or_null_containing_input_is_rejected()
    {
        // Arrange
        var first = TestRecipients.Recipient(TestRecipients.First);

        // Assert
        AgeRecipientSet.Create(null).IsFailure.ShouldBeTrue();
        AgeRecipientSet.Create([]).IsFailure.ShouldBeTrue();
        AgeRecipientSet.Create([first, first]).IsFailure.ShouldBeTrue();
        AgeRecipientSet.Create([first, null]).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Contains_and_except_reject_null()
    {
        // Arrange
        var set = TestRecipients.Set(TestRecipients.First);

        // Assert
        Action act1 = () => set.Contains(null!);
        act1.ShouldThrow<ArgumentNullException>();
        Action act2 = () => set.Except(null!);
        act2.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void ToString_lists_the_public_recipients()
    {
        // Act
        var text = TestRecipients.Set(TestRecipients.First, TestRecipients.Second).ToString();

        // Assert
        text.ShouldContain(TestRecipients.First, StringComparison.Ordinal);
        text.ShouldContain(TestRecipients.Second, StringComparison.Ordinal);
    }
}
