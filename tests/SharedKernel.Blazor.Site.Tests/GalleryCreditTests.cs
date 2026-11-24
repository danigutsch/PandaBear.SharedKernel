using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the credit of an image in an image grid.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class GalleryCreditTests : BunitContext
{
    private static readonly ImageCreditLine Credit = new("Photo: Ana Lima", "CC BY 4.0", SiteFixtures.Link("https://creativecommons.org/licenses/by/4.0/"), SiteFixtures.Link("/credits"));

    [Fact]
    public void A_credit_follows_the_caption_in_the_figure_caption()
    {
        // Arrange
        var image = new GalleryImage(SiteFixtures.Image("/a.jpg"), "Salt flats") { Credit = Credit };

        // Act
        var cut = Render<ImageGrid>(parameters => parameters.Add(component => component.Images, [image]));

        // Assert
        var caption = cut.Find("figure > figcaption");
        caption.ChildNodes.Length.ShouldBe(2);
        caption.FirstChild!.TextContent.ShouldBe("Salt flats");
        var credit = caption.QuerySelector("small.site-image-credit").ShouldNotBeNull();
        credit.TextContent.ShouldBe("Photo: Ana Lima CC BY 4.0");
        credit.QuerySelector("a[rel='license']")!.GetAttribute("href").ShouldBe("https://creativecommons.org/licenses/by/4.0/");
    }

    [Fact]
    public void A_credit_without_a_caption_is_the_figure_caption_alone()
    {
        // Arrange
        var image = new GalleryImage(SiteFixtures.Image("/a.jpg")) { Credit = new ImageCreditLine("Photo: Ana Lima") };

        // Act
        var cut = Render<ImageGrid>(parameters => parameters.Add(component => component.Images, [image]));

        // Assert
        cut.Find("figure > figcaption").InnerHtml.ShouldBe("<small class=\"site-image-credit\">Photo: Ana Lima</small>");
    }

    [Fact]
    public void A_credit_stays_outside_the_image_link_so_the_link_keeps_the_alternative_text_as_its_name()
    {
        // Arrange
        var image = new GalleryImage(SiteFixtures.Image("/a.jpg", ImageAlternative.Describe("Salt flats at dusk"))) { Href = SiteFixtures.Link("/a-large.jpg"), Credit = Credit };

        // Act
        var cut = Render<ImageGrid>(parameters => parameters.Add(component => component.Images, [image]));

        // Assert
        var link = cut.Find("a.site-image-grid__link");
        link.TextContent.ShouldBeEmpty();
        link.QuerySelector("img")!.GetAttribute("alt").ShouldBe("Salt flats at dusk");
        link.QuerySelector(".site-image-credit").ShouldBeNull();
        cut.Find("figure").Children.Select(child => child.TagName).ShouldBe(["A", "FIGCAPTION"]);
    }

    [Fact]
    public void An_image_without_caption_or_credit_has_no_figure_caption()
    {
        // Arrange & Act
        var cut = Render<ImageGrid>(parameters => parameters.Add(component => component.Images, [new GalleryImage(SiteFixtures.Image("/a.jpg"))]));

        // Assert
        cut.FindAll("figcaption").ShouldBeEmpty();
    }

    [Fact]
    public void Photo_language_applies_to_the_link_image_and_caption_while_credit_and_other_photos_inherit()
    {
        // Arrange
        var fallback = new GalleryImage(SiteFixtures.Image("/a.jpg", ImageAlternative.Describe("Montanhas ao amanhecer")), "Serra ao amanhecer")
        {
            Href = SiteFixtures.Link("/a-large.jpg"),
            Credit = Credit,
            TextLanguage = "pt-BR",
        };
        var translated = new GalleryImage(SiteFixtures.Image("/b.jpg", ImageAlternative.Describe("Valley")), "Valley");

        // Act
        var cut = this.RenderAlbum(PageWindow.Create(1, 2, 4), [fallback, translated]);

        // Assert
        var figures = cut.FindAll("figure");
        figures.Count.ShouldBe(2);
        figures[0].HasAttribute("lang").ShouldBeFalse();
        var link = figures[0].QuerySelector("a.site-image-grid__link").ShouldNotBeNull();
        link.GetAttribute("lang").ShouldBe("pt-BR");
        var image = link.QuerySelector("img").ShouldNotBeNull();
        image.GetAttribute("lang").ShouldBe("pt-BR");
        image.GetAttribute("alt").ShouldBe("Montanhas ao amanhecer");
        var caption = figures[0].QuerySelector("figcaption span[lang='pt-BR']").ShouldNotBeNull();
        caption.TextContent.ShouldBe("Serra ao amanhecer");
        var credit = figures[0].QuerySelector("figcaption > .site-image-credit").ShouldNotBeNull();
        credit.Closest("[lang]").ShouldBeNull();
        figures[1].QuerySelector("[lang]").ShouldBeNull();
        cut.Find("nav.site-pager").Closest("[lang]").ShouldBeNull();
    }

    [Fact]
    public void An_unlinked_photo_can_mark_the_language_of_its_alternative_without_a_caption()
    {
        // Arrange
        var image = new GalleryImage(SiteFixtures.Image("/a.jpg", ImageAlternative.Describe("Montanhas"))) { TextLanguage = "pt" };

        // Act
        var cut = Render<ImageGrid>(parameters => parameters.Add(component => component.Images, [image]));

        // Assert
        cut.Find("img").GetAttribute("lang").ShouldBe("pt");
        cut.FindAll("figcaption").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("pt_BR")]
    public void An_invalid_photo_language_is_refused_on_the_images_parameter(string language)
    {
        // Arrange
        var image = new GalleryImage(SiteFixtures.Image("/a.jpg")) { TextLanguage = language };

        // Act
        var render = () => Render<ImageGrid>(parameters => parameters.Add(component => component.Images, [image]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ImageGrid.Images));
    }

    [Theory]
    [InlineData(" ", false)]
    [InlineData("Photo: Ana Lima", true)]
    public void An_invalid_credit_is_refused_on_the_images_parameter(string text, bool licenseAddressWithoutText)
    {
        // Arrange
        var licenseHref = licenseAddressWithoutText ? SiteFixtures.Link("https://creativecommons.org/licenses/by/4.0/") : null;
        var image = new GalleryImage(SiteFixtures.Image("/a.jpg")) { Credit = new ImageCreditLine(text, null, licenseHref) };

        // Act
        var render = () => Render<ImageGrid>(parameters => parameters.Add(component => component.Images, [image]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ImageGrid.Images));
    }
}
