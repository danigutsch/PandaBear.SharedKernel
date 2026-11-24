using PandaBear.Results;

namespace PandaBear.Packages.Tests;

/// <summary>Tests the source bundle that <see cref="PackageInventory.CheckUpdates"/> uses.</summary>
[Trait(TestTraitNames.CapabilityName, "Packages")]
public sealed class UpdateSourcesTests
{
    [Fact]
    public void The_default_sources_use_osv_and_one_registry_source_and_contact_nothing()
    {
        // Act
        using var sources = UpdateSources.CreateDefault();

        // Assert
        sources.Advisories.ShouldBeOfType<OsvAdvisorySource>();
        sources.Latest.ShouldBeOfType<RegistryReleaseSource>();
        ReferenceEquals(sources.Latest, sources.ReleaseDates).ShouldBeTrue();
        sources.Time.ShouldBe(TimeProvider.System);
    }

    [Fact]
    public void Disposing_the_sources_disposes_what_they_own()
    {
        // Arrange
        using var owned = CountingSources.Default();
        var sources = owned.Create();

        // Act
        sources.Dispose();

        // Assert
        owned.Disposed.ShouldBeTrue();
    }

    [Fact]
    public void A_null_source_or_clock_is_rejected()
    {
        // Arrange
        var advisories = new FakeAdvisorySource(Result.Ok<IReadOnlyList<Advisory>>([]));
        var registry = new FakeRegistrySource([], []);

        Action[] creations =
        [
            () => _ = new UpdateSources(null!, registry, registry, TimeProvider.System),
            () => _ = new UpdateSources(advisories, null!, registry, TimeProvider.System),
            () => _ = new UpdateSources(advisories, registry, null!, TimeProvider.System),
            () => _ = new UpdateSources(advisories, registry, registry, null!),
        ];

        // Act and assert
        foreach (var create in creations)
        {
            create.ShouldThrow<ArgumentNullException>();
        }
    }
}
