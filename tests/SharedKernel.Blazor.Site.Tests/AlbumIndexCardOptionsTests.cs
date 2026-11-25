namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests that the album index passes the card and grid options to its cards and grid.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class AlbumIndexCardOptionsTests : BunitContext
{
    [Fact]
    public void The_card_options_reach_every_album_card()
    {
        // Arrange & Act
        var cut = Render<AlbumIndex>(parameters => parameters
            .Add(index => index.Albums, AlbumRendering.Summaries(2))
            .Add(index => index.CardStyle, CardStyle.Plain)
            .Add(index => index.ImageAspect, CardImageAspect.SixteenByNine)
            .Add(index => index.ImageShadow, CardImageShadow.Offset)
            .Add(index => index.TextAlignment, SiteAlignment.Center)
            .Add(index => index.MoreText, "See the photos")
            .Add(index => index.MoreVariant, ButtonVariant.Primary)
            .Add(index => index.MoreSize, ButtonSize.Compact));

        // Assert
        var cards = cut.FindAll("article");
        cards.Count.ShouldBe(2);
        cards.ShouldAllSatisfy(card => card.ClassName.ShouldBe("site-card site-card--plain site-card--aspect-16x9 site-card--image-shadow site-card--text-center"));
        cut.FindAll(".site-card__more").Select(more => more.ClassName).ShouldBe(
        [
            "site-button site-button--primary site-button--compact site-card__more",
            "site-button site-button--primary site-button--compact site-card__more",
        ]);
    }

    [Fact]
    public void The_grid_options_reach_the_grid()
    {
        // Arrange & Act
        var cut = Render<AlbumIndex>(parameters => parameters
            .Add(index => index.Albums, AlbumRendering.Summaries(1))
            .Add(index => index.MinColumnWidth, CardColumnWidth.Medium)
            .Add(index => index.MaxColumnWidth, CardColumnWidth.Wide)
            .Add(index => index.Gap, SiteSpace.Space6)
            .Add(index => index.ItemAlignment, SiteAlignment.Center));

        // Assert
        cut.Find("div").ClassName.ShouldBe("site-card-grid site-card-grid--min-medium site-card-grid--max-wide site-card-grid--gap-6 site-card-grid--items-center site-album-index");
    }

    [Fact]
    public void Without_options_the_index_keeps_its_boxed_cards_and_default_grid()
    {
        // Arrange & Act
        var cut = Render<AlbumIndex>(parameters => parameters.Add(index => index.Albums, AlbumRendering.Summaries(1)));

        // Assert
        cut.Find("div").ClassName.ShouldBe("site-card-grid site-album-index");
        cut.Find("article").ClassName.ShouldBe("site-card");
        cut.FindAll(".site-card__more").ShouldBeEmpty();
    }
}
