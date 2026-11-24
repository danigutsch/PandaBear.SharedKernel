using PandaBear.Blazor.Site.Theming;

namespace PandaBear.Blazor.Site.Tests.Theming;

/// <summary>Tests the small live theme preview.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ThemePreviewTests : BunitContext
{
    [Fact]
    public void The_draft_is_applied_through_a_style_block_scoped_to_the_preview_ID()
    {
        // Arrange & Act
        var cut = Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, SiteTheme.Default)
            .Add(component => component.ScopeId, "brand-preview"));

        // Assert
        var root = cut.Find("#brand-preview");
        var style = root.QuerySelector("style")!.TextContent;
        style.ShouldBe(SiteThemeStylesheet.RenderScoped(SiteTheme.Default, "brand-preview"));
        style.ShouldContain("#brand-preview {", StringComparison.Ordinal);
        style.ShouldNotContain(":root", StringComparison.Ordinal);
        style.ShouldNotContain("&quot;", StringComparison.Ordinal);
    }

    [Fact]
    public void The_sample_shows_headings_text_a_link_small_text_buttons_a_card_and_a_navigation_strip()
    {
        // Arrange & Act
        var cut = Render<ThemePreview>(parameters => parameters.Add(component => component.Theme, SiteTheme.Default));

        // Assert
        var sample = cut.Find(".site-theme-preview__sample");
        sample.HasAttribute("inert").ShouldBeTrue();
        sample.QuerySelector(".site-theme-preview__h1")!.TextContent.ShouldBe("Heading one");
        sample.QuerySelector(".site-theme-preview__h2")!.TextContent.ShouldBe("Heading two");
        sample.QuerySelector(".site-theme-preview__h3")!.TextContent.ShouldBe("Heading three");
        sample.QuerySelector("p a").ShouldNotBeNull();
        sample.QuerySelector("small").ShouldNotBeNull();
        sample.QuerySelector(".site-button--primary").ShouldNotBeNull();
        sample.QuerySelector(".site-button--secondary").ShouldNotBeNull();
        sample.QuerySelector(".site-card .site-card__title.site-theme-preview__card-title")!.TextContent.ShouldBe("Card");
        sample.QuerySelector("nav.site-nav a[aria-current=\"page\"]").ShouldNotBeNull();
    }

    [Fact]
    public void The_sample_headings_are_styled_text_so_the_page_outline_keeps_only_the_hosts_headings()
    {
        // Arrange & Act
        var cut = Render<ThemePreview>(parameters => parameters.Add(component => component.Theme, SiteTheme.Default));

        // Assert
        cut.FindAll("h1, h2, h3, h4, h5, h6, [role=\"heading\"]").ShouldBeEmpty();
        cut.Find(".site-theme-preview__sample").QuerySelectorAll(".site-theme-preview__h1, .site-theme-preview__h2, .site-theme-preview__h3, .site-theme-preview__card-title").Length.ShouldBe(4);
    }

    [Fact]
    public void The_contrast_table_and_the_issue_list_stay_outside_the_inert_sample()
    {
        // Arrange
        var colors = ThemeFixtures.DefaultColorsWith(ThemeColorRole.Muted, ThemeFixtures.Pair("#d1d5db", "#9ca3af"));
        var draft = SiteTheme.CreateDraft(colors, SiteTheme.Default.Typography).ShouldBeSuccess();

        // Act
        var cut = Render<ThemePreview>(parameters => parameters.Add(component => component.Theme, draft));

        // Assert
        cut.FindAll("[aria-hidden=\"true\"]").ShouldAllSatisfy(hidden => hidden.ClassList.ShouldContain("site-theme-preview__swatch"));
        cut.Find("table.site-theme-preview__contrast").Closest("[inert]").ShouldBeNull();
        cut.Find(".site-theme-preview__issues").Closest("[inert]").ShouldBeNull();
    }

    [Theory]
    [InlineData(ColorScheme.Light, false, "light", "wide")]
    [InlineData(ColorScheme.Dark, true, "dark", "narrow")]
    public void The_parent_owns_the_scheme_and_width_toggles(ColorScheme scheme, bool narrow, string theme, string width)
    {
        // Arrange & Act
        var cut = Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, SiteTheme.Default)
            .Add(component => component.Scheme, scheme)
            .Add(component => component.Narrow, narrow));

        // Assert
        var root = cut.Find(".site-theme-preview");
        root.GetAttribute("data-theme").ShouldBe(theme);
        root.GetAttribute("data-width").ShouldBe(width);
    }

    [Fact]
    public void Additional_attributes_cannot_replace_the_ID_theme_or_width_the_preview_owns()
    {
        // Arrange & Act
        var cut = Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, SiteTheme.Default)
            .Add(component => component.ScopeId, "brand-preview")
            .Add(component => component.Scheme, ColorScheme.Dark)
            .AddUnmatched("id", "other")
            .AddUnmatched("data-theme", "light")
            .AddUnmatched("data-width", "narrow")
            .AddUnmatched("data-test", "kept"));

        // Assert
        var root = cut.Find(".site-theme-preview");
        root.Id.ShouldBe("brand-preview");
        root.GetAttribute("data-theme").ShouldBe("dark");
        root.GetAttribute("data-width").ShouldBe("wide");
        root.GetAttribute("data-test").ShouldBe("kept");
    }

    [Fact]
    public void Contrast_results_for_the_shown_scheme_are_written_as_text()
    {
        // Arrange & Act
        var cut = Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, SiteTheme.Default)
            .Add(component => component.Scheme, ColorScheme.Dark));

        // Assert
        var table = cut.Find("table.site-theme-preview__contrast");
        table.QuerySelector("caption")!.TextContent.ShouldContain("dark", StringComparison.Ordinal);
        var rows = table.QuerySelectorAll("tbody tr");
        rows.Length.ShouldBe(14);
        rows.ShouldAllSatisfy(row => row.TextContent.ShouldNotContain("on-primary on text", StringComparison.Ordinal));
        var first = rows[0].TextContent;
        first.ShouldContain("text on surface", StringComparison.Ordinal);
        first.ShouldContain(":1", StringComparison.Ordinal);
        first.ShouldContain("4.5:1", StringComparison.Ordinal);
        first.ShouldContain("Pass", StringComparison.Ordinal);
        table.QuerySelectorAll("th[scope=\"col\"]").Length.ShouldBe(4);
    }

    [Fact]
    public void A_failing_pair_says_fail_in_text_and_its_rule_error_is_listed_with_its_field()
    {
        // Arrange
        var colors = ThemeFixtures.DefaultColorsWith(ThemeColorRole.Muted, ThemeFixtures.Pair("#d1d5db", "#9ca3af"));
        var draft = SiteTheme.CreateDraft(colors, SiteTheme.Default.Typography).ShouldBeSuccess();

        // Act
        var cut = Render<ThemePreview>(parameters => parameters.Add(component => component.Theme, draft));

        // Assert
        var failing = cut.FindAll("table.site-theme-preview__contrast tbody tr").Where(row => row.TextContent.Contains("Fail", StringComparison.Ordinal)).ToArray();
        failing.Length.ShouldBe(2);
        failing.ShouldAllSatisfy(row => row.TextContent.ShouldContain("muted on", StringComparison.Ordinal));
        var issue = cut.Find(".site-theme-preview__issues li[data-field=\"colors.muted\"]");
        issue.TextContent.ShouldContain("Error", StringComparison.Ordinal);
        issue.TextContent.ShouldContain("colors.muted", StringComparison.Ordinal);
    }

    [Fact]
    public void Colour_labels_from_the_host_name_the_table_pairs_and_the_contrast_messages()
    {
        // Arrange
        var draft = ThemeFixtures.PaleMutedDraft();

        // Act
        var cut = Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, draft)
            .Add(component => component.ColorLabel, ThemeFixtures.EditorLabel));

        // Assert
        var rows = cut.FindAll("table.site-theme-preview__contrast tbody tr");
        rows[0].QuerySelector("th")!.TextContent.ShouldContain("Body text on Page background", StringComparison.Ordinal);
        rows[0].QuerySelector(".site-theme-preview__swatch")!.GetAttribute("data-foreground").ShouldBe("text");
        rows.Where(row => row.TextContent.Contains("Fail", StringComparison.Ordinal))
            .Select(row => row.QuerySelector("th")!.TextContent.Replace("Aa", string.Empty, StringComparison.Ordinal).Trim())
            .ShouldBe(["Muted text on Page background", "Muted text on Card background"]);
        rows.ShouldContain(row => row.TextContent.Contains("on-primary on primary", StringComparison.Ordinal));
        cut.FindAll(".site-theme-preview__issues li[data-field=\"colors.muted\"]")
            .ShouldAllSatisfy(issue => issue.TextContent.ShouldContain("Muted text on ", StringComparison.Ordinal));
    }

    [Fact]
    public void Errors_from_the_editor_and_rule_warnings_are_shown_once_each()
    {
        // Arrange
        var typography = ThemeFixtures.DefaultTypographyWith(TypographyRole.H3, ThemeFixtures.Sized(2.5m, 3m));
        var draft = SiteTheme.CreateDraft(SiteTheme.Default.Colors, typography).ShouldBeSuccess();
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal) { ["typography.h1.size"] = ["Enter a size in rem."] };

        // Act
        var cut = Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, draft)
            .Add(component => component.Errors, errors));

        // Assert
        var items = cut.FindAll(".site-theme-preview__issues li");
        items.Count.ShouldBe(2);
        items[0].GetAttribute("data-field").ShouldBe("typography.h1.size");
        items[0].TextContent.ShouldContain("Enter a size in rem.", StringComparison.Ordinal);
        items[1].GetAttribute("data-field").ShouldBe("typography.h3.size");
        items[1].TextContent.ShouldContain("Warning", StringComparison.Ordinal);
    }

    [Fact]
    public void Without_issues_no_issue_list_is_rendered()
    {
        // Arrange & Act
        var cut = Render<ThemePreview>(parameters => parameters.Add(component => component.Theme, SiteTheme.Default));

        // Assert
        cut.FindAll(".site-theme-preview__issues").Count.ShouldBe(0);
    }

    [Fact]
    public void Texts_can_be_translated()
    {
        // Arrange
        var texts = new ThemePreviewTexts { Label = "Pré-visualização", Pass = "Passa", ContrastCaption = "Contraste", DarkScheme = "escuro" };

        // Act
        var cut = Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, SiteTheme.Default)
            .Add(component => component.Scheme, ColorScheme.Dark)
            .Add(component => component.Texts, texts));

        // Assert
        cut.Find(".site-theme-preview").GetAttribute("aria-label").ShouldBe("Pré-visualização");
        cut.Find("caption").TextContent.ShouldBe("Contraste (escuro)");
        cut.Find("tbody tr").TextContent.ShouldContain("Passa", StringComparison.Ordinal);
    }

    [Fact]
    public void Re_rendering_with_a_new_draft_updates_the_style_without_script()
    {
        // Arrange
        var cut = Render<ThemePreview>(parameters => parameters.Add(component => component.Theme, SiteTheme.Default));
        var changed = SiteTheme.CreateDraft(ThemeFixtures.DefaultColorsWith(ThemeColorRole.Primary, ThemeFixtures.Pair("#0f766e", "#5eead4")), SiteTheme.Default.Typography).ShouldBeSuccess();

        // Act
        cut.Render(parameters => parameters.Add(component => component.Theme, changed));

        // Assert
        cut.Find("style").TextContent.ShouldContain("--site-color-primary: light-dark(#0f766e, #5eead4);", StringComparison.Ordinal);
        cut.FindAll("script").Count.ShouldBe(0);
    }

    [Fact]
    public void The_style_element_carries_the_nonce_so_a_strict_policy_admits_it()
    {
        // Arrange & Act
        var cut = Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, SiteTheme.Default)
            .Add(component => component.Nonce, "q0Ub+/Xy_-9w4M1Lr2kE8A=="));

        // Assert
        cut.Find("style").GetAttribute("nonce").ShouldBe("q0Ub+/Xy_-9w4M1Lr2kE8A==");
    }

    [Fact]
    public void The_style_element_keeps_its_nonce_when_a_new_draft_is_rendered()
    {
        // Arrange
        var cut = Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, SiteTheme.Default)
            .Add(component => component.Nonce, "bm9uY2U="));
        var changed = SiteTheme.CreateDraft(ThemeFixtures.DefaultColorsWith(ThemeColorRole.Primary, ThemeFixtures.Pair("#0f766e", "#5eead4")), SiteTheme.Default.Typography).ShouldBeSuccess();

        // Act
        cut.Render(parameters => parameters.Add(component => component.Theme, changed));

        // Assert
        var style = cut.Find("style");
        style.GetAttribute("nonce").ShouldBe("bm9uY2U=");
        style.TextContent.ShouldContain("#0f766e", StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_nonce_the_style_element_has_no_nonce_attribute()
    {
        // Arrange & Act
        var cut = Render<ThemePreview>(parameters => parameters.Add(component => component.Theme, SiteTheme.Default));

        // Assert
        cut.Find("style").HasAttribute("nonce").ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc' 'unsafe-inline")]
    [InlineData("abc\" onload=\"x")]
    [InlineData("a=b")]
    public void A_nonce_that_is_not_a_base64_value_is_refused(string nonce)
    {
        // Arrange & Act
        var render = () => Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, SiteTheme.Default)
            .Add(component => component.Nonce, nonce));

        // Assert
        render.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Swatches_are_coloured_by_the_style_element_not_by_style_attributes_a_strict_policy_blocks()
    {
        // Arrange & Act
        var cut = Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, SiteTheme.Default)
            .Add(component => component.ScopeId, "brand-preview"));

        // Assert
        cut.FindAll("[style]").Count.ShouldBe(0);
        var swatch = cut.Find("tbody tr .site-theme-preview__swatch");
        swatch.GetAttribute("data-foreground").ShouldBe("text");
        swatch.GetAttribute("data-background").ShouldBe("surface");
        var style = cut.Find("style").TextContent;
        style.ShouldContain("#brand-preview .site-theme-preview__swatch[data-foreground=\"text\"] {\n    color: var(--site-color-text);\n}", StringComparison.Ordinal);
        style.ShouldContain("#brand-preview .site-theme-preview__swatch[data-background=\"surface\"] {\n    background: var(--site-color-surface);\n}", StringComparison.Ordinal);
    }

    [Fact]
    public void An_unsafe_scope_ID_is_refused()
    {
        // Arrange & Act
        var render = () => Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, SiteTheme.Default)
            .Add(component => component.ScopeId, "x\" onload=\"alert(1)"));

        // Assert
        render.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void The_navigation_strip_starts_with_a_brand_box_of_the_brand_height()
    {
        // Arrange
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, brandHeight: 3.5m).ShouldBeSuccess();

        // Act
        var cut = Render<ThemePreview>(parameters => parameters
            .Add(component => component.Theme, theme)
            .Add(component => component.ScopeId, "brand-preview")
            .Add(component => component.Texts, new ThemePreviewTexts { Brand = "Logo" }));

        // Assert
        var brand = cut.Find(".site-theme-preview__sample .site-nav > .site-theme-preview__brand");
        brand.TextContent.ShouldBe("Logo");
        var style = cut.Find("style").TextContent;
        style.ShouldContain("--site-brand-height: 3.5rem;", StringComparison.Ordinal);
        SiteFiles.Rule(style, "#brand-preview .site-theme-preview__brand").ShouldContain("height: var(--site-brand-height)", StringComparison.Ordinal);
    }
}
