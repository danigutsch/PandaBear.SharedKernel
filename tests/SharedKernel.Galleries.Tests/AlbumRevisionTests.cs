using static PandaBear.Galleries.Tests.AlbumFixtures;

namespace PandaBear.Galleries.Tests;

/// <summary>Tests revising, restoring and ordering albums.</summary>
[Trait(Testing.TestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
[Trait("Capability", "Albums")]
public sealed class AlbumRevisionTests
{
    private readonly PhotoLibrary _library = new();

    [Fact]
    public void Revising_with_the_same_data_reports_no_change()
    {
        // Arrange
        var photo = _library.Add(Portuguese, English);
        var definition = Definition([photo], Translation("en", "Patagonia"), Translation("pt-BR", "Patagônia", PublicationStatus.Published));
        var album = Album.Create(definition, Rules, _library).ShouldBeSuccess();
        var reordered = definition with { Translations = [Translation("pt-BR", " Patagônia ", PublicationStatus.Published, "patagonia"), Translation("en", "Patagonia")] };

        // Act
        var changed = album.Revise(reordered, Rules, _library).ShouldBeSuccess();

        // Assert
        changed.ShouldBeFalse();
    }

    [Fact]
    public void Revising_with_new_data_applies_it_and_reports_a_change()
    {
        // Arrange
        var first = _library.Add(Portuguese);
        var second = _library.Add(Portuguese);
        var album = Album.Create(Definition([first]), Rules, _library).ShouldBeSuccess();
        var related = Guid.CreateVersion7();

        // Act
        var changed = album.Revise(
            new AlbumDefinition<Guid, Guid>(5, related, [second, first], second, [Translation("pt-BR", "Andes", PublicationStatus.Published)]),
            Rules,
            _library).ShouldBeSuccess();

        // Assert
        changed.ShouldBeTrue();
        album.SortOrder.ShouldBe(5);
        album.RelatedId.ShouldBe(related);
        album.PhotoIds.ShouldBe([second, first]);
        album.CoverId.ShouldBe(second);
        album.Translations.ShouldHaveSingleItem().Title.ShouldBe("Andes");
        album.IsPublishedIn(Portuguese).ShouldBeTrue();
    }

    [Theory]
    [InlineData("sortOrder")]
    [InlineData("related")]
    [InlineData("photos")]
    [InlineData("cover")]
    [InlineData("title")]
    [InlineData("slug")]
    [InlineData("status")]
    [InlineData("culture")]
    public void Each_kind_of_change_is_reported(string change)
    {
        // Arrange
        var first = _library.Add(Portuguese, English);
        var second = _library.Add(Portuguese, English);
        var definition = Definition([first, second], Translation("pt-BR", "Andes"));
        var album = Album.Create(definition, Rules, _library).ShouldBeSuccess();
        var revised = change switch
        {
            "sortOrder" => definition with { SortOrder = 1 },
            "related" => definition with { RelatedId = Guid.CreateVersion7() },
            "photos" => definition with { PhotoIds = [second, first] },
            "cover" => definition with { CoverId = first },
            "title" => definition with { Translations = [Translation("pt-BR", "Andes 2")] },
            "slug" => definition with { Translations = [Translation("pt-BR", "Andes", slug: "andes-2")] },
            "status" => definition with { Translations = [Translation("pt-BR", "Andes", PublicationStatus.Published)] },
            _ => definition with { Translations = [Translation("pt-BR", "Andes"), Translation("en", "Andes")] },
        };

        // Act
        var changed = album.Revise(revised, Rules, _library).ShouldBeSuccess();

        // Assert
        changed.ShouldBeTrue();
    }

    [Fact]
    public void A_failed_revision_leaves_the_album_unchanged()
    {
        // Arrange
        var photo = _library.Add(Portuguese);
        var album = Album.Create(Definition([photo], Translation("pt-BR", "Andes")), Rules, _library).ShouldBeSuccess();

        // Act
        var result = album.Revise(new AlbumDefinition<Guid, Guid>(9, null, [photo, photo], null, [Translation("pt-BR", "Novo")]), Rules, _library);

        // Assert
        result.IsFailure.ShouldBeTrue();
        album.SortOrder.ShouldBe(0);
        album.PhotoIds.ShouldBe([photo]);
        album.Translations[0].Title.ShouldBe("Andes");
    }

    [Fact]
    public void A_stored_album_is_restored_without_validation()
    {
        // Arrange
        var photo = Guid.CreateVersion7();
        var related = Guid.CreateVersion7();
        var translation = AlbumTranslation.Restore(Portuguese, "Andes", Slug.Restore("andes"), PublicationStatus.Published);

        // Act
        var album = Album.Restore<Guid, Guid>(2, related, [photo], photo, [translation]);

        // Assert
        album.SortOrder.ShouldBe(2);
        album.RelatedId.ShouldBe(related);
        album.PhotoIds.ShouldBe([photo]);
        album.CoverId.ShouldBe(photo);
        album.Translations.ShouldBe([translation]);
    }

    [Fact]
    public void Albums_are_shown_by_sort_order_with_the_ID_as_tie_break()
    {
        // Arrange
        (int Sort, int Id)[] albums = [(2, 1), (1, 9), (1, 3), (0, 7)];

        // Act
        var ordered = AlbumOrdering.InDisplayOrder(albums, album => album.Sort, album => album.Id);

        // Assert
        ordered.Select(album => album.Id).ShouldBe([7, 3, 9, 1]);
    }
}
