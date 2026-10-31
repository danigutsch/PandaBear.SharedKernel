namespace PandaBear.DispositionHold.Tests;

/// <summary>
/// Tests Hold placement, release and effectiveness.
/// </summary>
public sealed class HoldTests
{
    private static readonly DateTimeOffset Placed = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Effective = Placed.AddDays(1);
    private static readonly DateTimeOffset ReviewDue = Placed.AddMonths(6);
    private static readonly DispositionScope Scope = DispositionScope.From("document", "doc-1");

    [Fact]
    public void Place_records_only_opaque_metadata()
    {
        // Arrange, Act
        var hold = HoldTestData.Place(expiresAt: Placed.AddYears(1));

        // Assert
        hold.Id.ShouldNotBe(Guid.Empty);
        hold.Scope.ShouldBe(Scope);
        hold.Basis.ShouldBe(HoldBasis.LegalClaim);
        hold.PurposeCode.ShouldBe("claim-defence");
        hold.AuthorityReference.ShouldBe("matter-ref-17");
        hold.PlacedBy.ShouldBe("actor-1");
        hold.PlacedAt.ShouldBe(Placed);
        hold.EffectiveFrom.ShouldBe(Effective);
        hold.ReviewDueAt.ShouldBe(ReviewDue);
        hold.ExpiresAt.ShouldBe(Placed.AddYears(1));
        hold.IsReleased.ShouldBeFalse();
        hold.ReleasedAt.ShouldBeNull();
    }

    [Fact]
    public void Place_rejects_a_default_scope_and_an_undefined_basis()
    {
        // Arrange, Act
        Action defaultScope = () => HoldTestData.Place(scope: default(DispositionScope));
        Action undefinedBasis = () => HoldTestData.Place(basis: (HoldBasis)99);

        // Assert
        _ = defaultScope.ShouldThrow<InvalidOperationException>();
        _ = undefinedBasis.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("matter@example.org")]
    public void Place_rejects_free_text_identifiers(string value)
    {
        // Arrange, Act
        Action purpose = () => HoldTestData.Place(purposeCode: value);
        Action authority = () => HoldTestData.Place(authorityReference: value);
        Action actor = () => HoldTestData.Place(placedBy: value);

        // Assert
        _ = purpose.ShouldThrow<ArgumentException>();
        _ = authority.ShouldThrow<ArgumentException>();
        _ = actor.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Place_rejects_dates_that_are_not_ordered()
    {
        // Arrange, Act
        Action reviewNotAfterEffective = () => HoldTestData.Place(reviewDueAt: Effective);
        Action expiryNotAfterEffective = () => HoldTestData.Place(expiresAt: Effective);

        // Assert
        _ = reviewNotAfterEffective.ShouldThrow<ArgumentException>();
        _ = expiryNotAfterEffective.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Release_records_actor_reason_and_time()
    {
        // Arrange
        var hold = HoldTestData.Place();
        var releasedAt = Effective.AddDays(10);

        // Act
        hold.Release("actor-2", "claim-resolved", releasedAt);

        // Assert
        hold.IsReleased.ShouldBeTrue();
        hold.ReleasedBy.ShouldBe("actor-2");
        hold.ReleaseReasonCode.ShouldBe("claim-resolved");
        hold.ReleasedAt.ShouldBe(releasedAt);
    }

    [Fact]
    public void Release_refuses_a_second_release_and_keeps_the_first()
    {
        // Arrange
        var hold = HoldTestData.Place();
        hold.Release("actor-2", "claim-resolved", Effective.AddDays(10));

        // Act
        Action second = () => hold.Release("actor-3", "other-reason", Effective.AddDays(20));

        // Assert
        _ = second.ShouldThrow<InvalidOperationException>();
        hold.ReleasedBy.ShouldBe("actor-2");
        hold.ReleaseReasonCode.ShouldBe("claim-resolved");
    }

    [Fact]
    public void Release_rejects_invalid_identifiers_without_changing_the_hold()
    {
        // Arrange
        var hold = HoldTestData.Place();

        // Act
        Action badActor = () => hold.Release("a b", "claim-resolved", Effective);
        Action badReason = () => hold.Release("actor-2", "", Effective);

        // Assert
        _ = badActor.ShouldThrow<ArgumentException>();
        _ = badReason.ShouldThrow<ArgumentException>();
        hold.IsReleased.ShouldBeFalse();
    }

    [Fact]
    public void RestrainsAt_is_false_before_the_effective_date_and_true_from_it()
    {
        // Arrange
        var hold = HoldTestData.Place();

        // Act, Assert
        hold.RestrainsAt(Effective.AddTicks(-1)).ShouldBeFalse();
        hold.RestrainsAt(Effective).ShouldBeTrue();
        hold.RestrainsAt(Effective.AddYears(50)).ShouldBeTrue();
    }

    [Fact]
    public void RestrainsAt_stops_at_the_expiry_boundary()
    {
        // Arrange
        var expiry = Placed.AddYears(1);
        var hold = HoldTestData.Place(expiresAt: expiry);

        // Act, Assert
        hold.RestrainsAt(expiry.AddTicks(-1)).ShouldBeTrue();
        hold.RestrainsAt(expiry).ShouldBeFalse();
    }

    [Fact]
    public void RestrainsAt_stops_after_an_explicit_release_even_before_expiry()
    {
        // Arrange
        var hold = HoldTestData.Place(expiresAt: Placed.AddYears(1));
        hold.Release("actor-2", "claim-resolved", Effective.AddDays(10));

        // Act, Assert
        hold.RestrainsAt(Effective.AddDays(11)).ShouldBeFalse();
    }
}
