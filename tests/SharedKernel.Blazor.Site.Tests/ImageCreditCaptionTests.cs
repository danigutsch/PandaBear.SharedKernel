namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the short credit under an image.</summary>
public sealed class ImageCreditCaptionTests : BunitContext
{
    [Fact]
    public void A_credit_is_small_text()
    {
        // Arrange & Act
        var cut = Render<ImageCreditCaption>(parameters => parameters.Add(component => component.Text, "Photo: Ana Lima"));

        // Assert
        var credit = cut.Find("small");
        credit.ClassName.ShouldBe("site-image-credit");
        credit.TextContent.ShouldBe("Photo: Ana Lima");
        credit.Children.ShouldBeEmpty();
    }

    [Fact]
    public void The_credits_address_links_the_text_and_the_licence_address_links_the_licence()
    {
        // Arrange & Act
        var cut = Render<ImageCreditCaption>(parameters => parameters
            .Add(component => component.Text, "Photo: Ana Lima")
            .Add(component => component.CreditsHref, SiteFixtures.Link("/credits#atacama"))
            .Add(component => component.LicenceText, "CC BY 4.0")
            .Add(component => component.LicenceHref, SiteFixtures.Link("https://creativecommons.org/licenses/by/4.0/")));

        // Assert
        cut.Find("small").InnerHtml.ShouldBe(
            "<a href=\"/credits#atacama\">Photo: Ana Lima</a> <a href=\"https://creativecommons.org/licenses/by/4.0/\" rel=\"license\">CC BY 4.0</a>");
    }

    [Fact]
    public void A_licence_without_an_address_is_plain_text_after_the_credit()
    {
        // Arrange & Act
        var cut = Render<ImageCreditCaption>(parameters => parameters
            .Add(component => component.Text, "Photo: Ana Lima")
            .Add(component => component.LicenceText, "All rights reserved"));

        // Assert
        var credit = cut.Find("small");
        credit.TextContent.ShouldBe("Photo: Ana Lima All rights reserved");
        credit.Children.ShouldBeEmpty();
    }

    [Fact]
    public void A_record_gives_the_same_credit()
    {
        // Arrange
        var line = new ImageCreditLine("Photo: Ana Lima", "CC BY 4.0", SiteFixtures.Link("https://creativecommons.org/licenses/by/4.0/"), SiteFixtures.Link("/credits"));

        // Act
        var cut = Render<ImageCreditCaption>(parameters => parameters
            .Add(component => component.Text, line.Text)
            .Add(component => component.LicenceText, line.LicenceText)
            .Add(component => component.LicenceHref, line.LicenceHref)
            .Add(component => component.CreditsHref, line.CreditsHref));

        // Assert
        cut.FindAll("a").Select(link => link.TextContent).ShouldBe(["Photo: Ana Lima", "CC BY 4.0"]);
    }

    [Fact]
    public void Class_and_attributes_reach_the_credit()
    {
        // Arrange & Act
        var cut = Render<ImageCreditCaption>(parameters => parameters
            .Add(component => component.Text, "Photo: Ana Lima")
            .Add(component => component.Class, "hero-credit")
            .AddUnmatched("lang", "pt"));

        // Assert
        var credit = cut.Find("small");
        credit.ClassName.ShouldBe("site-image-credit hero-credit");
        credit.GetAttribute("lang").ShouldBe("pt");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_text_is_refused(string text)
    {
        // Arrange & Act
        var render = () => Render<ImageCreditCaption>(parameters => parameters.Add(component => component.Text, text));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ImageCreditCaption.Text));
    }

    [Fact]
    public void A_licence_address_without_its_text_is_refused()
    {
        // Arrange & Act
        var render = () => Render<ImageCreditCaption>(parameters => parameters
            .Add(component => component.Text, "Photo: Ana Lima")
            .Add(component => component.LicenceHref, SiteFixtures.Link("https://creativecommons.org/licenses/by/4.0/")));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ImageCreditCaption.LicenceText));
    }
}
