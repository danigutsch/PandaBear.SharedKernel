using static PandaBear.Blazor.Site.Tests.SiteFixtures;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the head metadata rendered into a head outlet.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class PageHeadTests : BunitContext
{
    [Fact]
    public void Title_description_canonical_and_open_graph_basics_are_rendered()
    {
        // Arrange & Act
        var head = this.RenderHead(parameters => parameters
            .Add(component => component.Title, "Atacama | Brand")
            .Add(component => component.Description, "Eight days in the desert.")
            .Add(component => component.CanonicalUrl, new Uri("https://example.org/en/tours/atacama"))
            .Add(component => component.SiteName, "Brand")
            .Add(component => component.Locale, Culture("en-us"))
            .Add(component => component.ImageUrl, new Uri("https://example.org/atacama.jpg")));

        // Assert
        head.Find("title").TextContent.ShouldBe("Atacama | Brand");
        head.Find("meta[name=description]").GetAttribute("content").ShouldBe("Eight days in the desert.");
        head.Find("link[rel=canonical]").GetAttribute("href").ShouldBe("https://example.org/en/tours/atacama");
        head.Find("meta[property='og:title']").GetAttribute("content").ShouldBe("Atacama | Brand");
        head.Find("meta[property='og:description']").GetAttribute("content").ShouldBe("Eight days in the desert.");
        head.Find("meta[property='og:url']").GetAttribute("content").ShouldBe("https://example.org/en/tours/atacama");
        head.Find("meta[property='og:type']").GetAttribute("content").ShouldBe("website");
        head.Find("meta[property='og:site_name']").GetAttribute("content").ShouldBe("Brand");
        head.Find("meta[property='og:locale']").GetAttribute("content").ShouldBe("en_US");
        head.Find("meta[property='og:image']").GetAttribute("content").ShouldBe("https://example.org/atacama.jpg");
    }

    [Theory]
    [InlineData("zh-Hant-TW", "zh_TW")]
    [InlineData("pt-br", "pt_BR")]
    [InlineData("sr-Latn-RS", "sr_RS")]
    [InlineData("en", "en")]
    [InlineData("zh-Hant", "zh")]
    public void Og_locale_is_the_language_and_region_without_a_script(string culture, string expected)
    {
        // Arrange & Act
        var head = this.RenderHead(parameters => parameters
            .Add(component => component.Title, "Title")
            .Add(component => component.Locale, Culture(culture)));

        // Assert
        head.Find("meta[property='og:locale']").GetAttribute("content").ShouldBe(expected);
    }

    [Fact]
    public void Alternates_render_hreflang_links_including_x_default()
    {
        // Arrange & Act
        var head = this.RenderHead(parameters => parameters
            .Add(component => component.Title, "Atacama")
            .Add(component => component.Alternates, [new AlternateLink(Culture("en"), new Uri("https://example.org/en/x")), new AlternateLink(Culture("pt-br"), new Uri("https://example.org/pt/x"))])
            .Add(component => component.DefaultAlternateUrl, new Uri("https://example.org/en/x")));

        // Assert
        var alternates = head.FindAll("link[rel=alternate]");
        alternates.Select(link => link.GetAttribute("hreflang")).ShouldBeEquivalentTo("en", "pt-BR", "x-default");
        alternates.Select(link => link.GetAttribute("href")).ShouldBeEquivalentTo(
            "https://example.org/en/x",
            "https://example.org/pt/x",
            "https://example.org/en/x");
    }

    [Fact]
    public void Optional_metadata_is_omitted_when_not_supplied()
    {
        // Arrange & Act
        var head = this.RenderHead(parameters => parameters.Add(component => component.Title, "Atacama"));

        // Assert
        head.Find("title").TextContent.ShouldBe("Atacama");
        head.FindAll("meta[name=description]").ShouldBeEmpty();
        head.FindAll("meta[name=robots]").ShouldBeEmpty();
        head.FindAll("link[rel=canonical]").ShouldBeEmpty();
        head.FindAll("link[rel=alternate]").ShouldBeEmpty();
        head.FindAll("meta[property='og:url']").ShouldBeEmpty();
        head.FindAll("meta[property='og:image']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("noindex")]
    [InlineData("noindex, nofollow")]
    public void A_robots_directive_is_rendered_as_the_robots_meta_tag(string directive)
    {
        // Arrange & Act
        var head = this.RenderHead(parameters => parameters
            .Add(component => component.Title, "Page not found | Brand")
            .Add(component => component.Robots, directive));

        // Assert
        head.Find("title").TextContent.ShouldBe("Page not found | Brand");
        head.FindAll("meta[name=robots]").ShouldHaveSingleItem().GetAttribute("content").ShouldBe(directive);
        head.FindAll("link[rel=canonical]").ShouldBeEmpty();
        head.FindAll("link[rel=alternate]").ShouldBeEmpty();
        head.FindAll("meta[property='og:url']").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_robots_directive_renders_no_robots_meta_tag(string directive)
    {
        // Arrange & Act
        var head = this.RenderHead(parameters => parameters
            .Add(component => component.Title, "Atacama")
            .Add(component => component.Robots, directive));

        // Assert
        head.FindAll("meta[name=robots]").ShouldBeEmpty();
    }

    [Fact]
    public void Robots_directive_is_encoded()
    {
        // Arrange & Act
        var head = this.RenderHead(parameters => parameters
            .Add(component => component.Title, "x")
            .Add(component => component.Robots, "noindex\"><script>alert(1)</script>"));

        // Assert
        head.FindAll("script").ShouldBeEmpty();
        head.Find("meta[name=robots]").GetAttribute("content").ShouldBe("noindex\"><script>alert(1)</script>");
    }

    [Theory]
    [InlineData("/relative")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.org/x")]
    public void Canonical_alternate_and_image_addresses_must_be_absolute_web_addresses(string text)
    {
        // Arrange
        var address = new Uri(text, UriKind.RelativeOrAbsolute);

        // Act
        var canonical = () => Render<PageHead>(parameters => parameters
            .Add(component => component.Title, "x")
            .Add(component => component.CanonicalUrl, address));
        var alternate = () => Render<PageHead>(parameters => parameters
            .Add(component => component.Title, "x")
            .Add(component => component.Alternates, [new AlternateLink(Culture("en"), address)]));
        var image = () => Render<PageHead>(parameters => parameters
            .Add(component => component.Title, "x")
            .Add(component => component.ImageUrl, address));

        // Assert
        canonical.ShouldThrow<ArgumentException>();
        alternate.ShouldThrow<ArgumentException>();
        image.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void A_missing_alternate_culture_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<PageHead>(parameters => parameters
            .Add(component => component.Title, "x")
            .Add(component => component.Alternates, [new AlternateLink(null!, new Uri("https://example.org/x"))]));

        // Assert
        render.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Description_is_encoded()
    {
        // Arrange & Act
        var head = this.RenderHead(parameters => parameters
            .Add(component => component.Title, "x")
            .Add(component => component.Description, "\"><script>alert(1)</script>"));

        // Assert
        head.FindAll("script").ShouldBeEmpty();
        head.Find("meta[name=description]").GetAttribute("content").ShouldBe("\"><script>alert(1)</script>");
    }
}
