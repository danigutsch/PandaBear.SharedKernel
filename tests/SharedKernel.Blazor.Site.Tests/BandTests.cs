using static SharedKernel.Blazor.Site.Tests.SiteFixtures;

namespace SharedKernel.Blazor.Site.Tests;

/// <summary>Tests the full-width band.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class BandTests : BunitContext
{
    [Theory]
    [InlineData(BandTone.Default, "site-band site-band--default")]
    [InlineData(BandTone.Muted, "site-band site-band--muted")]
    [InlineData(BandTone.Primary, "site-band site-band--primary")]
    public void A_band_is_a_plain_division_with_its_tone_class_around_its_content(BandTone tone, string classes)
    {
        // Arrange & Act
        var cut = Render<Band>(parameters => parameters
            .Add(component => component.Tone, tone)
            .AddChildContent("<p id=\"tagline\">Travel with feeling.</p>"));

        // Assert
        var root = cut.Find(".site-band");
        root.TagName.ShouldBe("DIV");
        root.ClassName.ShouldBe(classes);
        root.HasAttribute("role").ShouldBeFalse();
        root.HasAttribute("aria-label").ShouldBeFalse();
        root.Children.Select(child => child.ClassName).ShouldBe(["site-band__inner"]);
        cut.Find(".site-band__inner > #tagline").ShouldNotBeNull();
        cut.FindAll("img").ShouldBeEmpty();
    }

    [Fact]
    public void The_default_tone_is_default()
    {
        // Arrange & Act
        var cut = Render<Band>(parameters => parameters.AddChildContent("x"));

        // Assert
        cut.Find(".site-band").ClassName.ShouldBe("site-band site-band--default");
    }

    [Fact]
    public void An_image_band_renders_its_lazy_image_after_the_content()
    {
        // Arrange & Act
        var cut = Render<Band>(parameters => parameters
            .Add(component => component.Tone, BandTone.Image)
            .Add(component => component.Image, Image("/band.jpg", ImageAlternative.Decorative()))
            .AddChildContent("<h2>Explore</h2>"));

        // Assert
        var root = cut.Find(".site-band");
        root.ClassName.ShouldBe("site-band site-band--image");
        root.Children.Select(child => child.ClassName).ShouldBe(["site-band__inner", "site-band__media"]);
        var image = cut.Find(".site-band__media img");
        image.GetAttribute("src").ShouldBe("/band.jpg");
        image.GetAttribute("alt").ShouldBe(string.Empty);
        image.GetAttribute("loading").ShouldBe("lazy");
        cut.FindAll(".site-image-credit").ShouldBeEmpty();
    }

    [Fact]
    public void An_image_band_shows_a_credit_outside_the_background_layer()
    {
        // Arrange
        var credit = new ImageCreditLine("Photo: Ana Lima", CreditsHref: Link("/credits#band"));

        // Act
        var cut = Render<Band>(parameters => parameters
            .Add(component => component.Tone, BandTone.Image)
            .Add(component => component.Image, Image("/band.jpg", ImageAlternative.Decorative()))
            .Add(component => component.ImageCredit, credit)
            .AddChildContent("<h2>Explore</h2>"));

        // Assert
        cut.Find(".site-band__media img").GetAttribute("alt").ShouldBe(string.Empty);
        cut.Find(".site-band small.site-image-credit a").GetAttribute("href").ShouldBe("/credits#band");
        cut.FindAll(".site-band__media .site-image-credit").ShouldBeEmpty();
    }

    [Fact]
    public void An_image_band_without_an_image_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<Band>(parameters => parameters.Add(component => component.Tone, BandTone.Image));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(Band.Image));
    }

    [Theory]
    [InlineData(BandTone.Default)]
    [InlineData(BandTone.Muted)]
    [InlineData(BandTone.Primary)]
    public void An_image_on_a_band_of_another_tone_is_rejected_instead_of_ignored(BandTone tone)
    {
        // Arrange & Act
        var render = () => Render<Band>(parameters => parameters
            .Add(component => component.Tone, tone)
            .Add(component => component.Image, Image("/band.jpg")));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(Band.Image));
    }

    [Fact]
    public void An_undefined_tone_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<Band>(parameters => parameters.Add(component => component.Tone, (BandTone)12));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(Band.Tone));
    }

    [Fact]
    public void A_section_inside_a_band_keeps_its_own_landmark_semantics()
    {
        // Arrange & Act
        var cut = Render<Band>(parameters => parameters
            .Add(component => component.Tone, BandTone.Muted)
            .AddChildContent<Section>(section => section
                .Add(component => component.Heading, "Past trips")
                .Add(component => component.HeadingId, "past-trips")));

        // Assert
        cut.Find(".site-band__inner > section.site-section").GetAttribute("aria-labelledby").ShouldBe("past-trips");
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_band()
    {
        // Arrange & Act
        var cut = Render<Band>(parameters => parameters
            .Add(component => component.Class, "extra")
            .AddUnmatched("lang", "pt-BR")
            .AddChildContent("x"));

        // Assert
        var root = cut.Find(".site-band");
        BunitElementAssertions.HasClass(root.ClassList, "extra");
        root.GetAttribute("lang").ShouldBe("pt-BR");
    }

    [Theory]
    [InlineData(null, "site-band site-band--muted extra")]
    [InlineData(SiteAlignment.Start, "site-band site-band--muted site-band--text-start extra")]
    [InlineData(SiteAlignment.Center, "site-band site-band--muted site-band--text-center extra")]
    public void The_text_alignment_inherits_by_default_and_otherwise_sets_a_class_after_the_tone(SiteAlignment? alignment, string classes)
    {
        // Arrange & Act
        var cut = Render<Band>(parameters => parameters
            .Add(component => component.Tone, BandTone.Muted)
            .Add(component => component.TextAlignment, alignment)
            .Add(component => component.Class, "extra")
            .AddChildContent("<p>Travel with feeling.</p>"));

        // Assert
        cut.Find(".site-band").ClassName.ShouldBe(classes);
    }

    [Fact]
    public void An_undefined_text_alignment_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<Band>(parameters => parameters
            .Add(component => component.TextAlignment, (SiteAlignment)5)
            .AddChildContent("x"));

        // Assert
        render.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe(nameof(Band.TextAlignment));
    }
}
