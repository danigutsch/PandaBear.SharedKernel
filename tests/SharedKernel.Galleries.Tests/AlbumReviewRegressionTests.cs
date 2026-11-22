using static PandaBear.Galleries.Tests.AlbumFixtures;

namespace PandaBear.Galleries.Tests;

/// <summary>Regression tests for the album model review: malformed input, restored data and exposed collections.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Albums")]
public sealed class AlbumReviewRegressionTests
{
    private readonly PhotoLibrary _library = new();

    [Fact]
    public void A_title_that_is_not_valid_text_is_a_validation_error_on_its_culture()
    {
        // Arrange
        string[] titles = [new(['A', 'b', 'c', (char)0xD800]), new([(char)0xDC00, 'A', 'b', 'c'])];

        // Act
        var results = titles.Select(title => Album.Create(Definition([], Translation("pt-BR", title, slug: "abc")), Rules, _library)).ToArray();

        // Assert
        results.ShouldAllSatisfy(result => result.ErrorDetails.ShouldHaveValidationError("pt-BR.title", "Title is not valid text."));
    }

    [Fact]
    public void The_exposed_lists_cannot_be_changed_by_casting_them()
    {
        // Arrange
        var photo = _library.Add(Portuguese);
        var created = Album.Create(Definition([photo], Translation("pt-BR", "Andes")), Rules, _library).ShouldBeSuccess();

        // Act
        var restored = Album.Restore<Guid, Guid>(0, null, [photo], null, [AlbumTranslation.Restore(Portuguese, "Andes", Slug.Restore("andes"), PublicationStatus.Draft)]);

        // Assert
        created.Translations.ShouldNotBeAssignableTo<List<AlbumTranslation>>();
        created.PhotoIds.ShouldNotBeAssignableTo<List<Guid>>();
        restored.Translations.ShouldNotBeAssignableTo<List<AlbumTranslation>>();
        created.Translations.ShouldBeAssignableTo<ICollection<AlbumTranslation>>().IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public void Restoring_two_translations_of_one_culture_fails_in_the_mapper_instead_of_on_every_page()
    {
        // Arrange
        AlbumTranslation[] translations =
        [
            AlbumTranslation.Restore(Portuguese, "A", Slug.Restore("a"), PublicationStatus.Published),
            AlbumTranslation.Restore(ContentCulture.Restore("pt-BR"), "B", Slug.Restore("b"), PublicationStatus.Published),
        ];
        Action restore = () => Album.Restore<Guid, Guid>(0, null, [Guid.CreateVersion7()], null, translations);

        // Act
        var failure = restore.ShouldThrow<ArgumentException>();

        // Assert
        failure.ParamName.ShouldBe("translations");
    }

    [Fact]
    public void Restoring_a_cover_outside_the_photos_or_an_undefined_status_fails()
    {
        // Arrange
        Action foreignCover = () => Album.Restore<Guid, Guid>(0, null, [Guid.CreateVersion7()], Guid.CreateVersion7(), []);
        Action undefinedStatus = () => AlbumTranslation.Restore(Portuguese, "A", Slug.Restore("a"), (PublicationStatus)7);

        // Act & Assert
        foreignCover.ShouldThrow<ArgumentException>().ParamName.ShouldBe("coverId");
        undefinedStatus.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("status");
    }

    [Fact]
    public void Reordering_translations_is_a_change_because_the_same_language_fallback_follows_their_order()
    {
        // Arrange
        var brazil = ContentCulture.Restore("pt-BR");
        var portugal = ContentCulture.Restore("pt-PT");
        var cultures = SupportedCultures.Create(brazil, [brazil, portugal]).Value;
        var rules = new AlbumRules(cultures, 4);
        var photo = _library.Add(brazil, portugal);
        AlbumTranslationDefinition brazilian = Translation("pt-BR", "Fotos", PublicationStatus.Published);
        AlbumTranslationDefinition portuguese = Translation("pt-PT", "Fotografias", PublicationStatus.Published);
        var album = Album.Create(Definition([photo], brazilian, portuguese), rules, _library).ShouldBeSuccess();
        var before = album.PublishedIn(ContentCulture.Restore("pt"), cultures).ShouldNotBeNull().Title;

        // Act
        var changed = album.Revise(Definition([photo], portuguese, brazilian), rules, _library).ShouldBeSuccess();

        // Assert
        changed.ShouldBeTrue();
        album.Translations.Select(translation => translation.Culture).ShouldBe([portugal, brazil]);
        before.ShouldBe("Fotos");
        album.PublishedIn(ContentCulture.Restore("pt"), cultures).ShouldNotBeNull().Title.ShouldBe("Fotografias");
    }

    [Fact]
    public void A_revision_fails_when_a_published_photo_is_no_longer_eligible()
    {
        // Arrange
        var photo = _library.Add(Portuguese);
        var definition = Definition([photo], Translation("pt-BR", "Andes", PublicationStatus.Published));
        var album = Album.Create(definition, Rules, _library).ShouldBeSuccess();
        _library.Withdraw(photo, Portuguese);

        // Act
        var result = album.Revise(definition with { SortOrder = 1 }, Rules, _library);

        // Assert
        Fields(result.ErrorDetails).ShouldBe(["pt-BR.photos"]);
        album.SortOrder.ShouldBe(0);
        album.IsPublishedIn(Portuguese).ShouldBeTrue();
    }

    [Fact]
    public void Finding_ineligible_photos_rejects_missing_arguments()
    {
        // Arrange
        Action noPhotos = () => AlbumPublishing.FindIneligible(null!, Portuguese, _library);
        Action noCulture = () => AlbumPublishing.FindIneligible([Guid.CreateVersion7()], null!, _library);
        Action noLibrary = () => AlbumPublishing.FindIneligible([Guid.CreateVersion7()], Portuguese, null!);

        // Act & Assert
        noPhotos.ShouldThrow<ArgumentNullException>();
        noCulture.ShouldThrow<ArgumentNullException>();
        noLibrary.ShouldThrow<ArgumentNullException>();
    }
}
