namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the credit of an image in an image grid.</summary>
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
    public void A_credit_with_a_blank_text_is_refused()
    {
        // Arrange
        var image = new GalleryImage(SiteFixtures.Image("/a.jpg")) { Credit = new ImageCreditLine(" ") };

        // Act
        var render = () => Render<ImageGrid>(parameters => parameters.Add(component => component.Images, [image]));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ImageGrid.Images));
    }
}
