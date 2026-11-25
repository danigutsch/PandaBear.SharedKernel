namespace SharedKernel.Blazor.Site.Tests;

/// <summary>
/// Tests that the look the components share is set once in the stylesheet: the eyebrow, the heading group, the plain list and the
/// private alignment properties, so the copies cannot drift apart again.
/// </summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class SiteSharedRulesStylesheetTests
{
    private static readonly string[] Eyebrows = [".site-card__eyebrow", ".site-hero__eyebrow", ".site-section__eyebrow", ".site-step-list__number"];

    private static readonly string[] PlainLists =
    [
        ".site-contact-card__channels",
        ".site-credits-list",
        ".site-language-switcher ul",
        ".site-nav__list",
        ".site-nav__sublist",
        ".site-pager__list",
        ".site-separated-list",
        ".site-step-list",
    ];

    [Fact]
    public void Every_eyebrow_takes_its_small_bold_spaced_look_from_one_rule()
    {
        // Arrange
        var rules = SiteFiles.Rules(File.ReadAllText(SiteFiles.StylesheetPath()));

        // Act
        var spaced = rules.Where(rule => rule.Body.Contains("letter-spacing: 0.05em", StringComparison.Ordinal)).ToArray();

        // Assert
        var eyebrow = spaced.ShouldHaveSingleItem();
        eyebrow.Selectors.ShouldBeEquivalentTo(Eyebrows);
        eyebrow.Body.ShouldContain("font-size: var(--site-font-size-sm)", StringComparison.Ordinal);
        eyebrow.Body.ShouldContain("font-weight: 700", StringComparison.Ordinal);
        eyebrow.Body.ShouldContain("color: var(--site-color-primary)", StringComparison.Ordinal);
    }

    [Fact]
    public void The_heading_groups_of_a_card_a_hero_and_a_section_share_one_margin_rule()
    {
        // Arrange
        var rules = SiteFiles.Rules(File.ReadAllText(SiteFiles.StylesheetPath()));
        string[] groups = [".site-card__heading", ".site-hero__heading", ".site-section__heading"];

        // Act
        var matching = rules.Where(rule => rule.Selectors.Intersect(groups, StringComparer.Ordinal).Any()).ToArray();

        // Assert
        var group = matching.ShouldHaveSingleItem();
        group.Selectors.ShouldBeEquivalentTo(groups);
        group.Body.Trim().ShouldBe("margin: 0;");
    }

    [Fact]
    public void Lists_without_markers_share_one_plain_list_rule()
    {
        // Arrange
        var rules = SiteFiles.Rules(File.ReadAllText(SiteFiles.StylesheetPath()));

        // Act
        var unmarked = rules.Where(rule => rule.Selectors.Intersect(PlainLists, StringComparer.Ordinal).Any()
            && rule.Body.Contains("list-style", StringComparison.Ordinal)).ToArray();

        // Assert
        var plain = unmarked.ShouldHaveSingleItem();
        plain.Selectors.ShouldBeEquivalentTo(PlainLists);
        plain.Body.ShouldContain("margin: 0", StringComparison.Ordinal);
        plain.Body.ShouldContain("padding: 0", StringComparison.Ordinal);
        plain.Body.ShouldContain("list-style: none", StringComparison.Ordinal);
    }

    [Fact]
    public void The_centred_alignment_is_one_rule_after_the_card_and_both_start_resets_set_the_same_properties()
    {
        // Arrange
        var rules = SiteFiles.Rules(File.ReadAllText(SiteFiles.StylesheetPath()));

        // Act
        var centred = rules.Where(rule => rule.Body.Contains("--_site-centre-margin: auto", StringComparison.Ordinal)).ToArray();
        var started = rules.Where(rule => rule.Body.Contains("--_site-centre-margin: initial", StringComparison.Ordinal)).ToArray();

        // Assert
        var centre = centred.ShouldHaveSingleItem();
        centre.Selectors.ShouldBeEquivalentTo([".site-card--text-center", ".site-section--text-center", ".site-band--text-center"]);
        centre.Body.ShouldContain("text-align: center", StringComparison.Ordinal);
        started.Select(rule => string.Join(", ", rule.Selectors)).ShouldBeEquivalentTo([":is(.site-section--text-start, .site-band--text-start)", ".site-card"]);
        started.Select(rule => rule.Declarations("--_site-", "text-align:")).Distinct(StringComparer.Ordinal).ShouldHaveSingleItem().ShouldBe(
            "--_site-centre-margin: initial;--_site-centre-justify: initial;--_site-image-position: initial;--_site-quote-rule: initial;text-align: start;");
        started.ShouldHaveSingleItem(rule => rule.Selectors[0] == ".site-card").Position.ShouldBeLessThan(centre.Position);
    }
}
