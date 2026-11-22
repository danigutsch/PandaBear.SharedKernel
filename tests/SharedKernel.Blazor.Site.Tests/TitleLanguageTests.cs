using static PandaBear.Blazor.Site.Tests.SiteFixtures;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>
/// Tests that a title in another language than the page is marked on the title alone (WCAG 3.1.2, language of parts), so the rest of a
/// card or section keeps the page's language.
/// </summary>
public sealed class TitleLanguageTests : BunitContext
{
    [Fact]
    public void A_card_marks_only_its_heading_with_the_title_language()
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters
            .Add(card => card.Title, "Patagonia")
            .Add(card => card.TitleLanguage, "en")
            .Add(card => card.Image, Image("/c.jpg", ImageAlternative.Describe("Um lago")))
            .AddChildContent("<p class=\"count\">12 fotos</p>"));

        // Assert
        cut.Find("h3").GetAttribute("lang").ShouldBe("en");
        cut.FindAll("[lang]").Count.ShouldBe(1);
    }

    [Fact]
    public void A_section_marks_only_its_heading_with_the_title_language()
    {
        // Arrange & Act
        var cut = Render<Section>(parameters => parameters
            .Add(section => section.Title, "Patagonia")
            .Add(section => section.TitleLanguage, "en")
            .Add(section => section.Intro, "Oito dias.")
            .AddChildContent("<p>Texto</p>"));

        // Assert
        cut.Find("h2").GetAttribute("lang").ShouldBe("en");
        cut.FindAll("[lang]").Count.ShouldBe(1);
    }

    [Fact]
    public void Without_a_title_language_no_lang_is_written()
    {
        // Arrange & Act
        var card = Render<Card>(parameters => parameters.Add(component => component.Title, "Patagônia"));
        var section = Render<Section>(parameters => parameters.Add(component => component.Title, "Patagônia"));

        // Assert
        card.FindAll("[lang]").ShouldBeEmpty();
        section.FindAll("[lang]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("pt BR")]
    [InlineData("pt_BR")]
    [InlineData("-pt")]
    [InlineData("en\"x")]
    public void A_title_language_that_is_not_a_language_tag_is_rejected(string tag)
    {
        // Arrange
        var card = () => Render<Card>(parameters => parameters.Add(component => component.Title, "A").Add(component => component.TitleLanguage, tag));
        var section = () => Render<Section>(parameters => parameters.Add(component => component.Title, "A").Add(component => component.TitleLanguage, tag));

        // Act & Assert
        card.ShouldThrow<ArgumentException>();
        section.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData("pt")]
    [InlineData("pt-BR")]
    [InlineData("zh-Hant-TW")]
    [InlineData("es-419")]
    public void Common_language_tags_are_accepted(string tag)
    {
        // Arrange & Act
        var cut = Render<Card>(parameters => parameters.Add(component => component.Title, "A").Add(component => component.TitleLanguage, tag));

        // Assert
        cut.Find("h3").GetAttribute("lang").ShouldBe(tag);
    }
}
