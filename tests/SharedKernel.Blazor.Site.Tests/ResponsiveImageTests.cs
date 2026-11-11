using static PandaBear.Blazor.Site.Tests.SiteFixtures;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests responsive images: <c>srcset</c>, <c>sizes</c> and <c>picture</c> sources, and their validation.</summary>
public sealed class ResponsiveImageTests : BunitContext
{
    [Fact]
    public void A_single_source_image_renders_one_img_without_srcset_or_picture()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.Image, Image("/a.jpg")));

        // Assert
        cut.FindAll("picture").ShouldBeEmpty();
        var image = cut.Find(".site-card__media > img");
        image.GetAttribute("src").ShouldBe("/a.jpg");
        image.HasAttribute("srcset").ShouldBeFalse();
        image.HasAttribute("sizes").ShouldBeFalse();
    }

    [Fact]
    public void Width_descriptors_render_srcset_and_sizes_on_the_img()
    {
        // Arrange
        var image = ResponsiveImage.Create(
            Link("/a.jpg"),
            1200,
            800,
            ImageAlternative.Describe("A lake"),
            sizes: "(min-width: 48rem) 33vw, 100vw",
            srcSet: [ImageCandidate.AtWidth(Link("/a-640.jpg"), 640), ImageCandidate.AtWidth(Link("/a.jpg"), 1200)]);

        // Act
        var cut = Render<Card>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.Image, image));

        // Assert
        var img = cut.Find("img");
        img.GetAttribute("srcset").ShouldBe("/a-640.jpg 640w, /a.jpg 1200w");
        img.GetAttribute("sizes").ShouldBe("(min-width: 48rem) 33vw, 100vw");
        img.GetAttribute("src").ShouldBe("/a.jpg");
        img.GetAttribute("width").ShouldBe("1200");
        img.GetAttribute("height").ShouldBe("800");
    }

    [Fact]
    public void Density_descriptors_need_no_sizes()
    {
        // Arrange
        var image = ResponsiveImage.Create(
            Link("/logo.png"),
            200,
            100,
            ImageAlternative.Describe("Logo"),
            srcSet: [ImageCandidate.AtDensity(Link("/logo.png"), 1m), ImageCandidate.AtDensity(Link("/logo@1.5x.png"), 1.50m), ImageCandidate.AtDensity(Link("/logo@2x.png"), 2m)]);

        // Act
        var cut = Render<Card>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.Image, image));

        // Assert
        var img = cut.Find("img");
        img.GetAttribute("srcset").ShouldBe("/logo.png 1x, /logo@1.5x.png 1.5x, /logo@2x.png 2x");
        img.HasAttribute("sizes").ShouldBeFalse();
    }

    [Fact]
    public void Typed_sources_render_a_picture_in_order_with_the_img_as_fallback()
    {
        // Arrange & Act
        var cut = Render<Hero>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.Image, PictureImage()));

        // Assert
        var picture = cut.Find(".site-hero__media > picture");
        var children = picture.Children;
        children.Select(child => child.TagName).ShouldBeEquivalentTo("SOURCE", "SOURCE", "IMG");
        children[0].GetAttribute("type").ShouldBe("image/avif");
        children[0].GetAttribute("srcset").ShouldBe("/p-800.avif 800w, /p-1600.avif 1600w");
        children[0].GetAttribute("sizes").ShouldBe("(min-width: 48rem) 50vw, 100vw");
        children[1].GetAttribute("type").ShouldBe("image/webp");
        var img = children[2];
        img.GetAttribute("src").ShouldBe("/p.jpg");
        img.GetAttribute("srcset").ShouldBe("/p-800.jpg 800w, /p.jpg 1600w");
        img.GetAttribute("alt").ShouldBe("A lake");
        img.GetAttribute("width").ShouldBe("1600");
        img.GetAttribute("height").ShouldBe("900");
        img.GetAttribute("loading").ShouldBe("eager");
        img.GetAttribute("fetchpriority").ShouldBe("high");
    }

    [Fact]
    public void A_card_picture_loads_lazily_without_a_fetch_priority()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.Image, PictureImage()));

        // Assert
        var img = cut.Find(".site-card__media > picture > img");
        img.GetAttribute("loading").ShouldBe("lazy");
        img.HasAttribute("fetchpriority").ShouldBeFalse();
    }

    [Fact]
    public void Source_types_are_normalized_and_must_be_image_media_types()
    {
        // Arrange
        ImageCandidate[] candidates = [ImageCandidate.AtDensity(Link("/a.avif"), 1m)];

        // Act
        var normalized = ImageSource.Create(" IMAGE/AVIF ", candidates);
        var notAnImage = () => ImageSource.Create("text/html", candidates);
        var injected = () => ImageSource.Create("image/avif\" onerror=\"x", candidates);

        // Assert
        normalized.Type.ShouldBe("image/avif");
        notAnImage.ShouldThrow<ArgumentException>().ParamName.ShouldBe("type");
        injected.ShouldThrow<ArgumentException>().ParamName.ShouldBe("type");
    }

    [Fact]
    public void Width_descriptors_without_sizes_are_rejected()
    {
        // Arrange
        ImageCandidate[] widths = [ImageCandidate.AtWidth(Link("/a-640.jpg"), 640)];

        // Act
        var onImg = () => ResponsiveImage.Create(Link("/a.jpg"), 10, 10, ImageAlternative.Decorative(), srcSet: widths);
        var onSource = () => ResponsiveImage.Create(Link("/a.jpg"), 10, 10, ImageAlternative.Decorative(), sources: [ImageSource.Create("image/webp", widths)]);

        // Assert
        onImg.ShouldThrow<ArgumentException>().ParamName.ShouldBe("sizes");
        onSource.ShouldThrow<ArgumentException>().ParamName.ShouldBe("sizes");
    }

    [Fact]
    public void A_candidate_set_must_not_mix_or_repeat_descriptors()
    {
        // Arrange
        var width = ImageCandidate.AtWidth(Link("/a-640.jpg"), 640);

        // Act
        var mixed = () => ImageSource.Create("image/webp", [width, ImageCandidate.AtDensity(Link("/a@2x.webp"), 2m)]);
        var repeated = () => ImageSource.Create("image/webp", [width, ImageCandidate.AtWidth(Link("/b-640.webp"), 640)]);
        var empty = () => ImageSource.Create("image/webp", []);
        var mixedOnImg = () => ResponsiveImage.Create(Link("/a.jpg"), 10, 10, ImageAlternative.Decorative(), "100vw", [width, ImageCandidate.AtDensity(Link("/a@2x.jpg"), 2m)]);

        // Assert
        mixed.ShouldThrow<ArgumentException>().ParamName.ShouldBe("candidates");
        repeated.ShouldThrow<ArgumentException>().ParamName.ShouldBe("candidates");
        empty.ShouldThrow<ArgumentException>().ParamName.ShouldBe("candidates");
        mixedOnImg.ShouldThrow<ArgumentException>().ParamName.ShouldBe("srcSet");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-640)]
    public void A_width_descriptor_must_be_positive(int width)
    {
        // Arrange & Act
        var create = () => ImageCandidate.AtWidth(Link("/a.jpg"), width);

        // Assert
        create.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("width");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void A_density_descriptor_must_be_positive(string density)
    {
        // Arrange & Act
        var create = () => ImageCandidate.AtDensity(Link("/a.jpg"), decimal.Parse(density, System.Globalization.CultureInfo.InvariantCulture));

        // Assert
        create.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("density");
    }

    [Fact]
    public void A_candidate_address_must_not_start_or_end_with_a_comma_that_would_split_the_srcset()
    {
        // Arrange & Act
        var trailing = () => ImageCandidate.AtWidth(Link("/a.jpg,"), 640);
        var inner = ImageCandidate.AtWidth(Link("/w_640,h_480/a.jpg"), 640);

        // Assert
        trailing.ShouldThrow<ArgumentException>().ParamName.ShouldBe("src");
        inner.ToString().ShouldBe("/w_640,h_480/a.jpg 640w");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("100vw\n")]
    public void Sizes_must_be_a_single_line_of_text(string sizes)
    {
        // Arrange & Act
        var create = () => ResponsiveImage.Create(Link("/a.jpg"), 10, 10, ImageAlternative.Decorative(), sizes);

        // Assert
        create.ShouldThrow<ArgumentException>().ParamName.ShouldBe("sizes");
    }

    [Fact]
    public void Equal_inputs_make_equal_images()
    {
        // Arrange & Act
        var first = PictureImage();
        var second = PictureImage();
        var other = Image("/p.jpg");

        // Assert
        first.ShouldBe(second);
        first.ShouldNotBe(other);
        first.UsesPicture.ShouldBeTrue();
        other.UsesPicture.ShouldBeFalse();
    }
}
