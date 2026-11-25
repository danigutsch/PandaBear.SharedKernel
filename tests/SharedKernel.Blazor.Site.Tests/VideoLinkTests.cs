using static PandaBear.Blazor.Site.Tests.SiteFixtures;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the link to a video on another site.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class VideoLinkTests : BunitContext
{
    [Fact]
    public void A_video_link_is_one_link_with_the_poster_a_play_mark_the_title_and_the_hidden_site_hint()
    {
        // Arrange & Act
        var cut = this.RenderVideoLink();

        // Assert
        var link = cut.Find("a.site-video-link");
        link.GetAttribute("href").ShouldBe("https://www.youtube.com/watch?v=abc123");
        link.GetAttribute("rel").ShouldBe("noreferrer");
        link.Children.Select(child => child.ClassName).ShouldBe(
            ["site-video-link__poster", "site-video-link__play", "site-video-link__title", "site-visually-hidden"]);
        link.QuerySelector(".site-video-link__play")!.GetAttribute("aria-hidden").ShouldBe("true");
        link.QuerySelector(".site-video-link__title")!.TextContent.ShouldBe("Patagonia in winter");
        link.QuerySelector(".site-visually-hidden")!.TextContent.ShouldBe(" (opens YouTube)");
        var poster = link.QuerySelector("img")!;
        poster.GetAttribute("alt").ShouldBe(string.Empty);
        poster.GetAttribute("loading").ShouldBe("lazy");
        cut.FindAll("iframe").ShouldBeEmpty();
        cut.FindAll("script").ShouldBeEmpty();
    }

    [Fact]
    public void The_accessible_name_is_the_title_then_the_site_hint()
    {
        // Arrange & Act
        var cut = this.RenderVideoLink();

        // Assert
        cut.Find("a").TextContent.ShouldBe("Patagonia in winter (opens YouTube)");
    }

    [Theory]
    [InlineData("/videos/one")]
    [InlineData("http://www.youtube.com/watch?v=abc123")]
    [InlineData("mailto:a@example.org")]
    public void A_video_address_that_is_not_absolute_https_is_rejected(string href)
    {
        // Arrange & Act
        var render = () => this.RenderVideoLink(href: href);

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(VideoLink.Href));
    }

    [Fact]
    public void A_described_poster_is_rejected_because_the_title_names_the_link()
    {
        // Arrange & Act
        var render = () => this.RenderVideoLink(poster: Image("/poster.jpg", ImageAlternative.Describe("A glacier")));

        // Assert
        render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(VideoLink.Poster));
    }

    [Fact]
    public void A_poster_from_another_site_is_rejected_so_the_page_requests_nothing_from_it()
    {
        // Arrange
        var remote = ResponsiveImage.Create(Link("https://i.ytimg.com/vi/abc123/hq.jpg"), 480, 360, ImageAlternative.Decorative());
        var remoteCandidate = ResponsiveImage.Create(
            Link("/poster.jpg"),
            480,
            360,
            ImageAlternative.Decorative(),
            srcSet: [ImageCandidate.AtDensity(Link("https://i.ytimg.com/vi/abc123/maxres.jpg"), 2m)]);
        var remoteSource = ResponsiveImage.Create(
            Link("/poster.jpg"),
            480,
            360,
            ImageAlternative.Decorative(),
            sources: [ImageSource.Create("image/webp", [ImageCandidate.AtDensity(Link("https://i.ytimg.com/vi_webp/abc123/hq.webp"), 1m)])]);

        // Act
        Action[] renders = [() => this.RenderVideoLink(poster: remote), () => this.RenderVideoLink(poster: remoteCandidate), () => this.RenderVideoLink(poster: remoteSource)];

        // Assert
        renders.ShouldAllSatisfy(render => render.ShouldThrow<ArgumentException>().ParamName.ShouldBe(nameof(VideoLink.Poster)));
    }

    [Theory]
    [InlineData("", "(opens YouTube)")]
    [InlineData("Patagonia", " ")]
    public void A_video_link_without_a_title_or_site_hint_is_rejected(string title, string hint)
    {
        // Arrange & Act
        var render = () => this.RenderVideoLink(title: title, hint: hint);

        // Assert
        render.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_link()
    {
        // Arrange & Act
        var cut = Render<VideoLink>(parameters => parameters
            .Add(component => component.Href, Link("https://vimeo.com/1"))
            .Add(component => component.Title, "Atacama")
            .Add(component => component.SiteHint, "(opens Vimeo)")
            .Add(component => component.Poster, Image("/poster.jpg", ImageAlternative.Decorative()))
            .Add(component => component.Class, "extra")
            .AddUnmatched("hreflang", "pt")
            .AddUnmatched("lang", "pt-BR"));

        // Assert
        var link = cut.Find("a");
        BunitElementAssertions.HasClass(link.ClassList, "extra");
        link.GetAttribute("hreflang").ShouldBe("pt");
        link.GetAttribute("lang").ShouldBe("pt-BR");
    }
}
