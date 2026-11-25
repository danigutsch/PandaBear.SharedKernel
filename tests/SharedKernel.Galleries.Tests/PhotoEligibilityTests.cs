using static SharedKernel.Galleries.Tests.AlbumFixtures;

namespace SharedKernel.Galleries.Tests;

/// <summary>Tests that the album photo knowledge is the shared image eligibility of BuildingBlocks.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "Albums")]
public sealed class PhotoEligibilityTests
{
    [Fact]
    public void Album_photo_knowledge_answers_as_image_eligibility()
    {
        // Arrange
        var library = new PhotoLibrary();
        var photo = library.Add(Portuguese);
        object knowledge = library;

        // Act
        var images = knowledge.ShouldBeAssignableTo<IImageEligibility<Guid>>();

        // Assert
        images.Exists(photo).ShouldBeTrue();
        images.IsEligible(photo, Portuguese).ShouldBeTrue();
        images.IsEligible(photo, English).ShouldBeFalse();
    }
}
