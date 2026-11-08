namespace PandaBear.BuildingBlocks.Tests;

/// <summary>Tests the time provider that reports a business time zone as local time.</summary>
[Trait("Scope", "Unit")]
[Trait("Capability", "Time")]
public sealed class BusinessTimeProviderTests
{
    private static readonly DateTimeOffset Instant = new(2026, 11, 6, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_local_date_follows_the_business_zone()
    {
        // Arrange
        var provider = new BusinessTimeProvider(BusinessTimeZones.SaoPaulo(), new FixedTimeProvider(Instant));

        // Act
        var today = provider.Today();

        // Assert
        today.ShouldBe(new DateOnly(2026, 11, 5));
        provider.GetUtcNow().ShouldBe(Instant);
        provider.GetLocalNow().Offset.ShouldBe(TimeSpan.FromHours(-3));
    }

    [Fact]
    public void A_utc_business_zone_keeps_the_utc_date()
    {
        // Arrange
        var provider = new BusinessTimeProvider(TimeZoneInfo.Utc, new FixedTimeProvider(Instant));

        // Act
        var today = provider.Today();

        // Assert
        today.ShouldBe(new DateOnly(2026, 11, 6));
        provider.LocalTimeZone.ShouldBe(TimeZoneInfo.Utc);
    }

    [Fact]
    public void Without_an_inner_provider_it_reads_the_system_clock()
    {
        // Arrange
        var provider = new BusinessTimeProvider(TimeZoneInfo.Utc);

        // Act
        var difference = (TimeProvider.System.GetUtcNow() - provider.GetUtcNow()).Duration();

        // Assert
        difference.ShouldBeLessThan(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        // Arrange
        Action noZone = () => _ = new BusinessTimeProvider(null!);
        Action noProvider = () => _ = TimeProviderExtensions.Today(null!);

        // Act & Assert
        noZone.ShouldThrow<ArgumentNullException>();
        noProvider.ShouldThrow<ArgumentNullException>();
    }
}
