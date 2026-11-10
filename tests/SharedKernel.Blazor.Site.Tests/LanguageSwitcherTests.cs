using static PandaBear.Blazor.Site.Tests.SiteFixtures;

namespace PandaBear.Blazor.Site.Tests;

/// <summary>Tests the language switcher.</summary>
public sealed class LanguageSwitcherTests : BunitContext
{
    private static readonly LanguageLink[] Languages =
    [
        new(Culture("pt-br"), "Portugues", Link("/pt/tours"), IsCurrent: true),
        new(Culture("en"), "English", Link("/en/tours")),
    ];

    [Fact]
    public void Switcher_is_a_labelled_navigation_with_one_link_per_language_carrying_the_canonical_hreflang_and_lang()
    {
        // Arrange & Act
        var cut = Render<LanguageSwitcher>(parameters => parameters
            .Add(component => component.Label, "Language")
            .Add(component => component.Languages, Languages));

        // Assert
        cut.Find("nav").GetAttribute("aria-label").ShouldBe("Language");
        var links = cut.FindAll("nav ul li a");
        links.Count.ShouldBe(2);
        links[0].GetAttribute("hreflang").ShouldBe("pt-BR");
        links[0].GetAttribute("lang").ShouldBe("pt-BR");
        links[0].GetAttribute("href").ShouldBe("/pt/tours");
        links[0].TextContent.ShouldBe("Portugues");
        links[1].GetAttribute("hreflang").ShouldBe("en");
    }

    [Fact]
    public void Only_the_current_language_is_marked()
    {
        // Arrange & Act
        var cut = Render<LanguageSwitcher>(parameters => parameters
            .Add(component => component.Label, "Language")
            .Add(component => component.Languages, Languages));

        // Assert
        var current = cut.FindAll("[aria-current]");
        current.Count.ShouldBe(1);
        current[0].GetAttribute("hreflang").ShouldBe("pt-BR");
        current[0].GetAttribute("aria-current").ShouldBe("true");
    }

    [Fact]
    public void A_missing_culture_is_rejected()
    {
        // Arrange & Act
        var render = () => Render<LanguageSwitcher>(parameters => parameters
            .Add(component => component.Label, "Language")
            .Add(component => component.Languages, [new LanguageLink(null!, "x", Link("/en"))]));

        // Assert
        render.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Extra_attributes_and_class_are_passed_to_the_navigation()
    {
        // Arrange & Act
        var cut = Render<LanguageSwitcher>(parameters => parameters
            .Add(component => component.Label, "Language")
            .Add(component => component.Languages, Languages)
            .Add(component => component.Class, "extra")
            .AddUnmatched("data-test", "x"));

        // Assert
        var nav = cut.Find("nav");
        BunitElementAssertions.HasClass(nav.ClassList, "site-language-switcher");
        BunitElementAssertions.HasClass(nav.ClassList, "extra");
        nav.GetAttribute("data-test").ShouldBe("x");
    }
}
