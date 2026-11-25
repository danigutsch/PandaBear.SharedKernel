using static SharedKernel.Galleries.Tests.AlbumFixtures;

namespace SharedKernel.Galleries.Tests;

/// <summary>Tests the published read view of an album and its pages of photos.</summary>
[Trait(Testing.TestTraitNames.ScopeName, "unit")]
[Trait(Testing.TestTraitNames.CapabilityName, "Albums")]
public sealed class AlbumViewTests
{
    private readonly PhotoLibrary _library = new();

    [Fact]
    public void The_view_shows_the_requested_published_culture()
    {
        // Arrange
        var album = PublishedAlbum(_library, 3, out var photos);

        // Act
        var view = album.PublishedIn(English, Cultures).ShouldNotBeNull();

        // Assert
        view.Culture.ShouldBe(English);
        view.Title.ShouldBe("Patagonia");
        view.Slug.Value.ShouldBe("patagonia");
        view.PhotoIds.ShouldBe(photos);
        view.PhotoCount.ShouldBe(3);
        view.CoverId.ShouldBe(photos[0]);
        view.SortOrder.ShouldBe(4);
        view.RelatedId.ShouldBeNull();
    }

    [Fact]
    public void A_draft_culture_falls_back_to_the_published_default_culture()
    {
        // Arrange
        var photo = _library.Add(Portuguese, English);
        var album = Album.Create(Definition([photo], Translation("pt-BR", "Patagônia", PublicationStatus.Published), Translation("en", "Patagonia")), Rules, _library)
            .ShouldBeSuccess();

        // Act
        var english = album.PublishedIn(English, Cultures).ShouldNotBeNull();
        var neutral = album.PublishedIn(ContentCulture.Restore("pt"), Cultures).ShouldNotBeNull();

        // Assert
        english.Culture.ShouldBe(Portuguese);
        english.Title.ShouldBe("Patagônia");
        neutral.Culture.ShouldBe(Portuguese);
    }

    [Fact]
    public void An_album_without_a_published_translation_has_no_view()
    {
        // Arrange
        var album = Album.Create(Definition([]), Rules, _library).ShouldBeSuccess();

        // Act
        var view = album.PublishedIn(Portuguese, Cultures);

        // Assert
        view.ShouldBeNull();
    }

    [Theory]
    [InlineData(null, 1, new[] { 0, 1 }, false)]
    [InlineData("2", 2, new[] { 2 }, false)]
    [InlineData("3", 2, new[] { 2 }, true)]
    [InlineData("x", 1, new[] { 0, 1 }, true)]
    public void A_page_holds_the_photos_of_its_window(string? requested, int page, int[] expected, bool outOfRange)
    {
        // Arrange
        var view = PublishedAlbum(_library, 3, out var photos).PublishedIn(Portuguese, Cultures).ShouldNotBeNull();

        // Act
        var slice = view.Page(requested, pageSize: 2);

        // Assert
        slice.Window.Page.ShouldBe(page);
        slice.Window.PageCount.ShouldBe(2);
        slice.Window.IsOutOfRange.ShouldBe(outOfRange);
        slice.PhotoIds.ShouldBe([.. expected.Select(index => photos[index])]);
    }

    [Fact]
    public void A_page_can_be_asked_for_by_number()
    {
        // Arrange
        var view = PublishedAlbum(_library, 3, out var photos).PublishedIn(Portuguese, Cultures).ShouldNotBeNull();

        // Act
        var slice = view.Page(2, pageSize: 2);

        // Assert
        slice.PhotoIds.ShouldBe([photos[2]]);
    }

    [Fact]
    public void Invalid_view_arguments_are_rejected()
    {
        // Arrange
        var album = PublishedAlbum(_library, 1, out _);
        var view = album.PublishedIn(Portuguese, Cultures).ShouldNotBeNull();
        Action noCultures = () => album.PublishedIn(Portuguese, null!);
        Action noPageSize = () => view.Page("1", 0);

        // Act & Assert
        noCultures.ShouldThrow<ArgumentNullException>();
        noPageSize.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
