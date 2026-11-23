using static PandaBear.Offerings.Tests.OfferingFixtures;

namespace PandaBear.Offerings.Tests;

/// <summary>Tests the cover, the gallery and the images an offering shows.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Offerings")]
public sealed class OfferingImageTests
{
    private readonly ImageLibrary _images = new();

    [Fact]
    public void The_images_are_the_cover_the_gallery_and_the_step_images_each_once()
    {
        // Arrange
        var cover = _images.Add(Portuguese);
        var first = _images.Add(Portuguese);
        var second = _images.Add(Portuguese);
        var step = _images.Add(Portuguese);

        // Act
        var offering = Valid(Tour() with
        {
            CoverId = cover,
            GalleryIds = [first, second],
            Steps = [new TourStep(step), new TourStep(first), new TourStep(null)],
        }, _images);

        // Assert
        offering.CoverId.ShouldBe(cover);
        offering.GalleryIds.ShouldBe([first, second]);
        offering.ImageIds.ShouldBe([cover, first, second, step]);
    }

    [Fact]
    public void The_cover_need_not_be_in_the_gallery_but_must_exist()
    {
        // Act
        var missing = Refused(Tour() with { CoverId = Guid.CreateVersion7() }, _images);

        // Assert
        missing.ShouldHaveValidationError(OfferingFields.CoverId, "The cover image does not exist.");
    }

    [Fact]
    public void The_gallery_is_bounded_and_its_images_exist_and_are_distinct()
    {
        // Arrange
        var image = _images.Add(Portuguese);
        Guid[] many = [.. Enumerable.Range(0, 4).Select(_ => _images.Add(Portuguese))];

        // Act
        var tooMany = Refused(Tour() with { GalleryIds = many }, _images);
        var missing = Refused(Tour() with { GalleryIds = [image, Guid.CreateVersion7()] }, _images);
        var twice = Refused(Tour() with { GalleryIds = [image, image] }, _images);

        // Assert
        tooMany.ShouldHaveValidationError(OfferingFields.GalleryIds, "A gallery cannot hold more than 3 images.");
        missing.ShouldHaveValidationError(OfferingFields.GalleryIds, "A gallery image does not exist.");
        twice.ShouldHaveValidationError(OfferingFields.GalleryIds, "A gallery image appears twice.");
    }

    [Fact]
    public void Publishing_needs_every_image_eligible_in_that_language()
    {
        // Arrange
        var cover = _images.Add(Portuguese, English);
        var portugueseOnly = _images.Add(Portuguese);
        var definition = Tour(Translation(status: PublicationStatus.Published), Translation("en", PublicationStatus.Published)) with
        {
            CoverId = cover,
            Steps = [new TourStep(portugueseOnly), new TourStep(null), new TourStep(null)],
        };

        // Act
        var error = Refused(definition, _images);
        var ineligible = OfferingPublishing.FindIneligible([cover, portugueseOnly, portugueseOnly], English, _images);

        // Assert
        Fields(error).ShouldBe([OfferingFields.Of(English, OfferingFields.Images)]);
        error.ShouldHaveValidationError("en.images", "Every image must be eligible in this language before publishing; 1 are not.");
        ineligible.ShouldBe([portugueseOnly]);
    }

    [Fact]
    public void Finding_ineligible_images_refuses_missing_arguments()
    {
        // Act
        Action noImages = () => OfferingPublishing.FindIneligible(null!, English, _images);
        Action noCulture = () => OfferingPublishing.FindIneligible([Guid.Empty], null!, _images);
        Action noKnowledge = () => OfferingPublishing.FindIneligible([Guid.Empty], English, null!);

        // Assert
        noImages.ShouldThrow<ArgumentNullException>();
        noCulture.ShouldThrow<ArgumentNullException>();
        noKnowledge.ShouldThrow<ArgumentNullException>();
    }
}
