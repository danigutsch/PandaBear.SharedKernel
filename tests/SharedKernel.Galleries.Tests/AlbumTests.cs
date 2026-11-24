using static PandaBear.Galleries.Tests.AlbumFixtures;

namespace PandaBear.Galleries.Tests;

/// <summary>Tests creating and validating an album.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "Albums")]
public sealed class AlbumTests
{
    private readonly PhotoLibrary _library = new();

    [Fact]
    public void A_valid_definition_creates_an_album_whose_first_photo_is_the_cover()
    {
        // Arrange
        var first = _library.Add(Portuguese, English);
        var second = _library.Add(Portuguese, English);
        var related = Guid.CreateVersion7();
        var definition = new AlbumDefinition<Guid, Guid>(3, related, [first, second], null,
            [Translation("pt-BR", " Patagônia ", PublicationStatus.Published), Translation("en", "Patagonia", slug: "patagonia-trip")]);

        // Act
        var album = Album.Create(definition, Rules, _library).ShouldBeSuccess();

        // Assert
        album.SortOrder.ShouldBe(3);
        album.RelatedId.ShouldBe(related);
        album.PhotoIds.ShouldBe([first, second]);
        album.CoverId.ShouldBeNull();
        album.EffectiveCoverId.ShouldBe(first);
        album.Translations.Count.ShouldBe(2);
        var portuguese = album.Translations[0];
        portuguese.Culture.ShouldBe(Portuguese);
        portuguese.Title.ShouldBe("Patagônia");
        portuguese.Slug.Value.ShouldBe("patagonia");
        portuguese.Status.ShouldBe(PublicationStatus.Published);
        album.Translations[1].Slug.Value.ShouldBe("patagonia-trip");
        album.IsPublishedIn(Portuguese).ShouldBeTrue();
        album.IsPublishedIn(English).ShouldBeFalse();
    }

    [Fact]
    public void A_chosen_cover_must_be_one_of_the_photos()
    {
        // Arrange
        var first = _library.Add(Portuguese);
        var second = _library.Add(Portuguese);
        var outside = _library.Add(Portuguese);

        // Act
        var chosen = Album.Create(Definition([first, second]) with { CoverId = second }, Rules, _library).ShouldBeSuccess();
        var foreign = Album.Create(Definition([first, second]) with { CoverId = outside }, Rules, _library);

        // Assert
        chosen.CoverId.ShouldBe(second);
        chosen.EffectiveCoverId.ShouldBe(second);
        Fields(foreign.ErrorDetails).ShouldBe([AlbumFields.CoverId]);
    }

    [Fact]
    public void An_album_without_photos_has_no_cover()
    {
        // Arrange & Act
        var album = Album.Create(Definition([]), Rules, _library).ShouldBeSuccess();

        // Assert
        album.PhotoIds.ShouldBeEmpty();
        album.EffectiveCoverId.ShouldBeNull();
    }

    [Fact]
    public void More_photos_than_the_maximum_are_refused()
    {
        // Arrange
        Guid[] photos = [.. Enumerable.Range(0, Rules.MaxPhotos + 1).Select(_ => _library.Add(Portuguese))];

        // Act
        var result = Album.Create(Definition(photos), Rules, _library);

        // Assert
        result.ErrorDetails.ShouldHaveValidationError(AlbumFields.PhotoIds, "An album cannot hold more than 4 photos.");
    }

    [Fact]
    public void A_photo_that_does_not_exist_or_appears_twice_is_refused()
    {
        // Arrange
        var photo = _library.Add(Portuguese);

        // Act
        var missing = Album.Create(Definition([photo, Guid.CreateVersion7()]), Rules, _library);
        var twice = Album.Create(Definition([photo, photo]), Rules, _library);

        // Assert
        missing.ErrorDetails.ShouldHaveValidationError(AlbumFields.PhotoIds, "A photo does not exist.");
        twice.ErrorDetails.ShouldHaveValidationError(AlbumFields.PhotoIds, "A photo appears twice.");
    }

    [Fact]
    public void Translations_must_use_the_album_cultures_once_each_and_include_the_default_culture()
    {
        // Arrange & Act
        var unknown = Album.Create(Definition([], Translation("pt-BR", "A"), Translation("fr", "B")), Rules, _library);
        var twice = Album.Create(Definition([], Translation("pt-BR", "A"), Translation("PT-br", "B")), Rules, _library);
        var noDefault = Album.Create(Definition([], Translation("en", "A")), Rules, _library);

        // Assert
        Messages(unknown.ErrorDetails, AlbumFields.Translations).ShouldBe(["Each translation must use one of the album languages: pt-BR, en."]);
        Messages(twice.ErrorDetails, AlbumFields.Translations).ShouldBe(["Each language can have only one translation."]);
        Messages(noDefault.ErrorDetails, AlbumFields.Translations).ShouldBe(["A translation in the default language (pt-BR) is required."]);
    }

    [Theory]
    [InlineData(null, "Title is required.")]
    [InlineData("   ", "Title is required.")]
    [InlineData("0123456789012345678901234567890123456789X", "Title cannot exceed 40 characters.")]
    public void A_missing_or_long_title_is_refused_on_its_culture(string? title, string message)
    {
        // Arrange & Act
        var result = Album.Create(Definition([], Translation("pt-BR", title, slug: "album")), Rules, _library);

        // Assert
        result.ErrorDetails.ShouldHaveValidationError("pt-BR.title", message);
    }

    [Fact]
    public void A_given_slug_must_be_a_valid_slug_and_a_missing_one_comes_from_the_title()
    {
        // Arrange & Act
        var derived = Album.Create(Definition([], Translation("pt-BR", "Viagem à Patagônia, 2027!")), Rules, _library).ShouldBeSuccess();
        var invalid = Album.Create(Definition([], Translation("pt-BR", "Viagem", slug: "a/b")), Rules, _library);
        var noLatinLetters = Album.Create(Definition([], Translation("pt-BR", "東京")), Rules, _library);

        // Assert
        derived.Translations[0].Slug.Value.ShouldBe("viagem-a-patagonia-2027");
        Fields(invalid.ErrorDetails).ShouldBe(["pt-BR.slug"]);
        Fields(noLatinLetters.ErrorDetails).ShouldBe(["pt-BR.slug"]);
    }

    [Fact]
    public void An_undefined_status_is_refused()
    {
        // Arrange & Act
        var result = Album.Create(Definition([], Translation("pt-BR", "A", (PublicationStatus)7)), Rules, _library);

        // Assert
        result.ErrorDetails.ShouldHaveValidationError("pt-BR.status", "The publication status is not valid.");
    }

    [Fact]
    public void A_published_translation_needs_a_photo()
    {
        // Arrange & Act
        var result = Album.Create(Definition([], Translation("pt-BR", "A", PublicationStatus.Published)), Rules, _library);

        // Assert
        result.ErrorDetails.ShouldHaveValidationError("pt-BR.photos", "An album needs at least one photo before it is published.");
    }

    [Fact]
    public void A_translation_is_published_only_when_every_photo_is_eligible_in_its_culture()
    {
        // Arrange
        var both = _library.Add(Portuguese, English);
        var portugueseOnly = _library.Add(Portuguese);
        var none = _library.Add();

        // Act
        var englishPublished = Album.Create(
            Definition([both, portugueseOnly, none], Translation("pt-BR", "A"), Translation("en", "B", PublicationStatus.Published)), Rules, _library);
        var englishDraft = Album.Create(
            Definition([both, portugueseOnly, none], Translation("pt-BR", "A"), Translation("en", "B")), Rules, _library);

        // Assert
        englishPublished.ErrorDetails.ShouldHaveValidationError("en.photos", "Every photo must be eligible in this language before publishing; 2 are not.");
        englishDraft.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void The_photos_that_block_publishing_in_a_culture_are_listed_in_album_order()
    {
        // Arrange
        var both = _library.Add(Portuguese, English);
        var portugueseOnly = _library.Add(Portuguese);
        var none = _library.Add();

        // Act
        var ineligible = AlbumPublishing.FindIneligible([none, both, portugueseOnly, none], English, _library);

        // Assert
        ineligible.ShouldBe([none, portugueseOnly]);
    }

    [Fact]
    public void Every_problem_is_reported_at_once_with_its_field_path()
    {
        // Arrange
        var photo = _library.Add();

        // Act
        var result = Album.Create(
            new AlbumDefinition<Guid, Guid>(0, null, [photo, photo], Guid.CreateVersion7(),
                [Translation("pt-BR", null, PublicationStatus.Published), Translation("en", "B", slug: "x?y")]),
            Rules,
            _library);

        // Assert
        Fields(result.ErrorDetails).ShouldBe([AlbumFields.CoverId, "en.slug", AlbumFields.PhotoIds, "pt-BR.photos", "pt-BR.title"]);
    }

    [Fact]
    public void Missing_lists_count_as_empty()
    {
        // Arrange & Act
        var noTranslations = Album.Create(new AlbumDefinition<Guid, Guid>(0, null, null, null, null), Rules, _library);

        // Assert
        Fields(noTranslations.ErrorDetails).ShouldBe([AlbumFields.Translations]);
    }

    [Fact]
    public void Invalid_arguments_are_rejected()
    {
        // Arrange
        var definition = Definition([]);
        Action noDefinition = () => Album.Create<Guid, Guid>(null!, Rules, _library);
        Action noRules = () => Album.Create(definition, null!, _library);
        Action noLibrary = () => Album.Create(definition, Rules, null!);
        Action noCultures = () => _ = new AlbumRules(null!, 1);
        Action noPhotos = () => _ = new AlbumRules(Cultures, 0);
        Action noTitle = () => _ = new AlbumRules(Cultures, 1) { MaxTitleLength = 0 };
        Action noSlug = () => _ = new AlbumRules(Cultures, 1) { MaxSlugLength = 0 };

        // Act & Assert
        noDefinition.ShouldThrow<ArgumentNullException>();
        noRules.ShouldThrow<ArgumentNullException>();
        noLibrary.ShouldThrow<ArgumentNullException>();
        noCultures.ShouldThrow<ArgumentNullException>();
        noPhotos.ShouldThrow<ArgumentOutOfRangeException>();
        noTitle.ShouldThrow<ArgumentOutOfRangeException>();
        noSlug.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Field_paths_of_a_culture_start_with_the_culture_name()
    {
        // Arrange & Act
        var path = AlbumFields.Of(Portuguese, AlbumFields.Title);

        // Assert
        path.ShouldBe("pt-BR.title");
    }
}
