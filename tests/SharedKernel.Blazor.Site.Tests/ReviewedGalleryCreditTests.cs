using PandaBear.BuildingBlocks;
using PandaBear.ContentRights;
using static PandaBear.Blazor.Site.Tests.PhotoCreditFixture;
using static PandaBear.Blazor.Site.Tests.SiteFixtures;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the optional photo credit on images through the gallery and album entry points.</summary>
public sealed class ReviewedGalleryCreditTests : BunitContext
{
    [Fact]
    public void Mixed_credits_keep_photo_links_captions_and_alt_text_on_their_own_figures()
    {
        // Arrange
        var first = Photo("cascata", "Cascata", ContentLicenseKind.CcBy, "CC BY 4.0",
            "https://creativecommons.org/licenses/by/4.0/") with
        {
            DetailsHref = LinkTarget.FromString("/creditos#cascata"),
        };
        var second = Photo("serra", "Serra", ContentLicenseKind.CustomTerms, "Termos do arquivo",
            "https://archive.example/terms") with
        {
            DetailsHref = LinkTarget.FromString("/creditos#serra"),
        };
        GalleryImage[] images =
        [
            Images(1)[0] with { Href = LinkTarget.FromString("/full/cascata.jpg"), ReviewedCredit = first },
            new GalleryImage(Image("/decorative.jpg", ImageAlternative.Decorative())) { ReviewedCredit = second },
            Images(2)[1],
        ];

        // Act
        var cut = Render<ImageGrid>(p => p.Add(component => component.Images, images).Add(component => component.CreditLabels, Labels));

        // Assert
        var figures = cut.FindAll("figure");
        figures.Count.ShouldBe(3);
        figures[0].QuerySelectorAll(":scope > figcaption").Length.ShouldBe(1);
        figures[0].QuerySelector("figcaption")!.TextContent.ShouldContain("First", StringComparison.Ordinal);
        figures[0].QuerySelector("a[href='/full/cascata.jpg'] img")!.GetAttribute("alt").ShouldBe("Photo 1");
        figures[0].QuerySelector("a[href='/creditos#cascata']").ShouldNotBeNull();
        figures[0].QuerySelector("a[href='https://creativecommons.org/licenses/by/4.0/']").ShouldNotBeNull();
        figures[1].QuerySelector("img")!.GetAttribute("alt").ShouldBe(string.Empty);
        figures[1].QuerySelector("figcaption a[href='/creditos#serra']").ShouldNotBeNull();
        figures[1].QuerySelector("figcaption a[href='https://archive.example/terms']").ShouldNotBeNull();
        figures[1].QuerySelector("a[href='/creditos#cascata']").ShouldBeNull();
        figures[2].QuerySelector("figcaption").ShouldBeNull();
    }

    [Fact]
    public void An_empty_credit_creates_no_caption_and_the_original_deconstruction_is_preserved()
    {
        // Arrange
        var original = Images(2)[1];
        var item = new PhotoCreditItem("empty", "Empty", ContentAttribution.Create(new()).ShouldBeSuccess().ToPublicCredit());
        var credited = original with { ReviewedCredit = item };

        // Act
        var (image, caption) = credited;
        var cut = Render<ImageGrid>(p => p.Add(component => component.Images, [credited]).Add(component => component.CreditLabels, Labels));

        // Assert
        image.ShouldBe(original.Image);
        caption.ShouldBeNull();
        cut.FindAll("figcaption").Count.ShouldBe(0);
    }

    [Fact]
    public void A_gallery_with_public_credits_requires_caller_localized_labels()
    {
        // Arrange
        var credited = Images(1)[0] with { ReviewedCredit = Photo("serra", "Serra", ContentLicenseKind.ReservedRights, "Direitos reservados") };

        // Act
        var render = () => Render<ImageGrid>(p => p.Add(component => component.Images, [credited]));

        // Assert
        render.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(ImageGrid.CreditLabels));
    }

    [Fact]
    public void Album_sections_pass_localized_credits_to_the_grid()
    {
        // Arrange
        var item = Photo("serra", "Serra", ContentLicenseKind.Cc0, "CC0 1.0", "https://creativecommons.org/publicdomain/zero/1.0/");

        // Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 4, 1), [Images(1)[0] with { ReviewedCredit = item }],
            p => p.Add(component => component.CreditLabels, Labels));

        // Assert
        cut.Find("figure figcaption a[href='https://creativecommons.org/publicdomain/zero/1.0/']").TextContent
            .ShouldContain("CC0 1.0", StringComparison.Ordinal);
        cut.Find("figcaption details > summary").TextContent.ShouldContain(Labels.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_credit_lines_keep_their_markup_alongside_reviewed_credits()
    {
        // Arrange
        var existing = Images(2)[0] with { Credit = new ImageCreditLine("Photo: Existing creator", CreditsHref: Link("/credits#existing")) };
        var reviewed = Images(2)[1] with { ReviewedCredit = Photo("reviewed", "Reviewed photo", ContentLicenseKind.Unknown) };

        // Act
        var cut = Render<ImageGrid>(p => p.Add(component => component.Images, [existing, reviewed]).Add(component => component.CreditLabels, Labels));

        // Assert
        cut.Find("figure small.site-image-credit a[href='/credits#existing']").TextContent.ShouldBe("Photo: Existing creator");
        cut.FindAll("figure > figcaption").Count.ShouldBe(2);
        cut.Find("figure .site-photo-credit details > summary").TextContent.ShouldContain(Labels.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void A_photo_cannot_supply_two_credit_representations()
    {
        // Arrange
        var image = Images(1)[0] with
        {
            Credit = new ImageCreditLine("Photo: Existing creator"),
            ReviewedCredit = Photo("reviewed", "Reviewed photo", ContentLicenseKind.Unknown),
        };

        // Act
        var render = () => Render<ImageGrid>(p => p.Add(component => component.Images, [image]).Add(component => component.CreditLabels, Labels));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ImageGrid.Images));
    }

    [Fact]
    public void Reviewed_credit_and_pagination_inherit_the_page_language_for_a_fallback_photo()
    {
        // Arrange
        var fallback = new GalleryImage(Image("/serra.jpg", ImageAlternative.Describe("Montanhas ao amanhecer")), "Serra ao amanhecer")
        {
            Href = Link("/serra-large.jpg"),
            TextLanguage = "pt-BR",
            ReviewedCredit = Photo("serra", "Serra", ContentLicenseKind.CcBy, "CC BY 4.0", "https://creativecommons.org/licenses/by/4.0/"),
        };
        var translated = Images(2)[1] with { Credit = new ImageCreditLine("Photo: Existing creator", CreditsHref: Link("/credits#existing")) };

        // Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 2, 4), [fallback, translated], parameters => parameters
            .Add(component => component.CreditLabels, Labels));

        // Assert
        var figures = cut.FindAll("figure");
        figures.Count.ShouldBe(2);
        var linkedPhoto = cut.Find("a[href='/serra-large.jpg']");
        linkedPhoto.GetAttribute("lang").ShouldBe("pt-BR");
        linkedPhoto.QuerySelector("img").ShouldNotBeNull().GetAttribute("lang").ShouldBe("pt-BR");
        cut.Find("figcaption span[lang='pt-BR']").TextContent.ShouldBe("Serra ao amanhecer");
        var details = cut.Find("figcaption .site-photo-credit details > summary");
        details.TextContent.ShouldContain(Labels.Details, StringComparison.Ordinal);
        details.Closest("[lang]").ShouldBeNull();
        cut.Find("figcaption .site-image-credit a[href='/credits#existing']").Closest("[lang]").ShouldBeNull();
        cut.Find("nav.site-pager").Closest("[lang]").ShouldBeNull();
    }
}
