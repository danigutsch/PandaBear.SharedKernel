using System.Globalization;
using PandaBear.BuildingBlocks;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the short credit under an image and its record.</summary>
public sealed class ImageCreditCaptionTests : BunitContext
{
    private static readonly LinkTarget CcBy = SiteFixtures.Link("https://creativecommons.org/licenses/by/4.0/");

    /// <summary>Gets credit lines with a blank text, or a license address without the license text it links.</summary>
    public static TheoryData<ImageCreditLine> InvalidLines { get; } = new()
    {
        new ImageCreditLine(" "),
        new ImageCreditLine("Photo: Ana Lima", null, CcBy),
        new ImageCreditLine("Photo: Ana Lima", " ", CcBy),
    };

    [Fact]
    public void A_credit_is_small_text()
    {
        // Arrange & Act
        var cut = Render<ImageCreditCaption>(parameters => parameters.Add(component => component.Credit, new ImageCreditLine("Photo: Ana Lima")));

        // Assert
        var credit = cut.Find("small");
        credit.ClassName.ShouldBe("site-image-credit");
        credit.TextContent.ShouldBe("Photo: Ana Lima");
        credit.Children.ShouldBeEmpty();
    }

    [Fact]
    public void The_credits_address_links_the_text_and_the_license_address_links_the_license()
    {
        // Arrange
        var line = new ImageCreditLine("Photo: Ana Lima", "CC BY 4.0", CcBy, SiteFixtures.Link("/credits#atacama"));

        // Act
        var cut = Render<ImageCreditCaption>(parameters => parameters.Add(component => component.Credit, line));

        // Assert
        cut.Find("small").InnerHtml.ShouldBe(
            "<a href=\"/credits#atacama\">Photo: Ana Lima</a> <a href=\"https://creativecommons.org/licenses/by/4.0/\" rel=\"license\">CC BY 4.0</a>");
    }

    [Fact]
    public void A_license_without_an_address_is_plain_text_after_the_credit()
    {
        // Arrange & Act
        var cut = Render<ImageCreditCaption>(parameters => parameters.Add(component => component.Credit, new ImageCreditLine("Photo: Ana Lima", "All rights reserved")));

        // Assert
        var credit = cut.Find("small");
        credit.TextContent.ShouldBe("Photo: Ana Lima All rights reserved");
        credit.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Class_and_attributes_reach_the_credit()
    {
        // Arrange & Act
        var cut = Render<ImageCreditCaption>(parameters => parameters
            .Add(component => component.Credit, new ImageCreditLine("Photo: Ana Lima"))
            .Add(component => component.Class, "hero-credit")
            .AddUnmatched("lang", "pt"));

        // Assert
        var credit = cut.Find("small");
        credit.ClassName.ShouldBe("site-image-credit hero-credit");
        credit.GetAttribute("lang").ShouldBe("pt");
    }

    [Fact]
    public void A_missing_credit_is_refused()
    {
        // Arrange & Act
        var render = () => Render<ImageCreditCaption>(parameters => parameters.Add(component => component.Credit, null));

        // Assert
        render.ShouldThrow<ArgumentNullException>().ParamName.ShouldBe(nameof(ImageCreditCaption.Credit));
    }

    [Theory]
    [MemberData(nameof(InvalidLines))]
    public void An_invalid_credit_is_refused_on_the_credit_parameter(ImageCreditLine line)
    {
        // Arrange & Act
        var render = () => Render<ImageCreditCaption>(parameters => parameters.Add(component => component.Credit, line));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(ImageCreditCaption.Credit));
    }

    [Fact]
    public void A_shared_credit_maps_to_a_line_of_the_application_format_and_its_linked_license()
    {
        // Arrange
        var credit = ImageCredit.Create("Ana Lima", "Salar", "https://example.org/salar", "CC BY 4.0", "https://creativecommons.org/licenses/by/4.0/", modified: false).ShouldBeSuccess();

        // Act
        var line = ImageCreditLine.FromCredit(credit, "Foto: {0}", CultureInfo.GetCultureInfo("pt-BR"), SiteFixtures.Link("/pt/creditos"));

        // Assert
        line.ShouldBe(new ImageCreditLine("Foto: Ana Lima", "CC BY 4.0", CcBy, SiteFixtures.Link("/pt/creditos")));
    }

    [Fact]
    public void A_shared_credit_without_a_license_link_maps_to_a_plain_license()
    {
        // Arrange
        var credit = ImageCredit.Create("Ana Lima", null, null, "Pixabay", null, modified: true).ShouldBeSuccess();

        // Act
        var line = ImageCreditLine.FromCredit(credit, "Foto: {0}", CultureInfo.InvariantCulture);

        // Assert
        line.ShouldBe(new ImageCreditLine("Foto: Ana Lima", "Pixabay"));
    }
}
