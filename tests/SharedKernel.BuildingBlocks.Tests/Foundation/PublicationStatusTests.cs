namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Tests the publication status shared by published content such as albums, pages and tours.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "ValueObjects")]
public sealed class PublicationStatusTests
{
    [Fact]
    public void Draft_is_the_default_and_published_is_one_so_stored_values_keep_their_meaning()
    {
        // Arrange & Act
        PublicationStatus status = default;
        var draft = (int)PublicationStatus.Draft;
        var published = (int)PublicationStatus.Published;

        // Assert
        status.ShouldBe(PublicationStatus.Draft);
        draft.ShouldBe(0);
        published.ShouldBe(1);
        Enum.GetValues<PublicationStatus>().Length.ShouldBe(2);
    }
}
