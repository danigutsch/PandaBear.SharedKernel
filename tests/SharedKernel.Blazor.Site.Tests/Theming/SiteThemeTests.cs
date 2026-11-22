using PandaBear.Blazor.Site.Theming;
using PandaBear.Results;

namespace PandaBear.Blazor.Site.Tests.Theming;

/// <summary>Tests the theme rules: contrast in both schemes, readable sizes, heading order and font references.</summary>
public sealed class SiteThemeTests
{
    [Fact]
    public void The_default_theme_meets_every_rule_and_has_no_warnings()
    {
        // Arrange
        var theme = SiteTheme.Default;

        // Act
        var review = theme.Review();
        var created = SiteTheme.Create(theme.Colors, theme.Typography, theme.Fonts);

        // Assert
        review.Errors.ShouldBeEmpty();
        review.Warnings.ShouldBeEmpty();
        review.HasErrors.ShouldBeFalse();
        created.ShouldBeSuccess().ShouldBe(theme);
    }

    [Fact]
    public void The_default_theme_matches_the_stylesheet_defaults()
    {
        // Arrange
        var colors = SiteTheme.Default.Colors;
        string[] tokens = ["primary", "primary-hover", "on-primary", "accent", "accent-hover", "on-accent", "text", "muted", "surface", "surface-alt", "border", "focus"];

        // Act
        var light = tokens.Select(token => SiteFiles.Colors(dark: false)["color-" + token]).ToArray();
        var dark = tokens.Select(token => SiteFiles.Colors(dark: true)["color-" + token]).ToArray();

        // Assert
        Enum.GetValues<ThemeColorRole>().Select(role => colors[role].Light.Value).ShouldBe(light);
        Enum.GetValues<ThemeColorRole>().Select(role => colors[role].Dark.Value).ShouldBe(dark);
        SiteTheme.Default.Typography[TypographyRole.Body].LineHeight.ShouldBe(1.6m);
        SiteTheme.Default.Typography[TypographyRole.H1].Size!.Min.ShouldBe(2.125m);
        SiteTheme.Default.Typography[TypographyRole.H1].Size!.Max.ShouldBe(3.5m);
        SiteTheme.Default.Typography[TypographyRole.Button].Weight.ShouldBe(600);
        SiteTheme.Default.Typography[TypographyRole.Link].ShouldBe(TypographyStyle.Inherit);
    }

    [Fact]
    public void The_review_checks_fourteen_pairs_in_each_scheme_and_the_photo_scrim_in_the_light_scheme()
    {
        // Arrange & Act
        var contrast = SiteTheme.Default.Review().Contrast;

        // Assert
        contrast.Count.ShouldBe(29);
        contrast.Count(check => check.Scheme == ColorScheme.Dark).ShouldBe(14);
        contrast.Select(check => check.Scheme).ShouldBe([.. Enumerable.Repeat(ColorScheme.Light, 15), .. Enumerable.Repeat(ColorScheme.Dark, 14)]);
        contrast.ShouldContain(check => check.Foreground == ThemeColorRole.OnPrimary && check.Background == ThemeColorRole.Text && check.Scheme == ColorScheme.Light);
        contrast.ShouldContain(check => check.Foreground == ThemeColorRole.Muted && check.Background == ThemeColorRole.SurfaceAlt && check.Minimum == 4.5 && check.Criterion == "1.4.3");
        contrast.ShouldContain(check => check.Foreground == ThemeColorRole.OnPrimary && check.Background == ThemeColorRole.PrimaryHover);
        contrast.ShouldContain(check => check.Foreground == ThemeColorRole.OnAccent && check.Background == ThemeColorRole.AccentHover);
        contrast.ShouldContain(check => check.Foreground == ThemeColorRole.Focus && check.Background == ThemeColorRole.Surface && check.Minimum == 3.0 && check.Criterion == "1.4.11");
        contrast.ShouldAllSatisfy(check => check.Passes.ShouldBeTrue());
    }

    [Fact]
    public void Low_contrast_text_in_the_dark_scheme_only_is_an_error_on_the_foreground_field()
    {
        // Arrange
        var colors = ThemeFixtures.DefaultColorsWith(ThemeColorRole.Muted, ThemeFixtures.Pair("#4b5563", "#4b5563"));

        // Act
        var result = SiteTheme.Create(colors, SiteTheme.Default.Typography);
        var review = SiteTheme.CreateDraft(colors, SiteTheme.Default.Typography).ShouldBeSuccess().Review();

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, ResultErrorCodes.Invalid);
        var messages = result.FieldErrors()["colors.muted"];
        messages.ShouldContain(message => message.Contains("dark", StringComparison.Ordinal) && message.Contains("4.5:1", StringComparison.Ordinal));
        messages.ShouldAllSatisfy(message => message.ShouldNotContain("light scheme", StringComparison.Ordinal));
        review.Contrast.Where(check => !check.Passes).ShouldAllSatisfy(check => check.Scheme.ShouldBe(ColorScheme.Dark));
    }

    [Fact]
    public void A_focus_colour_below_3_to_1_is_an_error_under_the_non_text_rule()
    {
        // Arrange
        var colors = ThemeFixtures.DefaultColorsWith(ThemeColorRole.Focus, ThemeFixtures.Pair("#e5e7eb", "#93c5fd"));

        // Act
        var result = SiteTheme.Create(colors, SiteTheme.Default.Typography);

        // Assert
        result.FieldErrors()["colors.focus"].ShouldContain(message => message.Contains("3:1", StringComparison.Ordinal) && message.Contains("1.4.11", StringComparison.Ordinal));
    }

    [Fact]
    public void A_focus_colour_between_3_and_4_5_to_1_is_enough()
    {
        // Arrange: #7a7a7a is 4.29:1 on white and 3.9:1 on the light surface-alt.
        var colors = ThemeFixtures.DefaultColorsWith(ThemeColorRole.Focus, ThemeFixtures.Pair("#7a7a7a", "#93c5fd"));

        // Act
        var result = SiteTheme.Create(colors, SiteTheme.Default.Typography);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Text_on_the_primary_colour_is_checked_against_the_hover_colour_too()
    {
        // Arrange: on-primary passes on primary, but the hover colour is as light as the text.
        var colors = ThemeFixtures.DefaultColorsWith(ThemeColorRole.PrimaryHover, ThemeFixtures.Pair("#dbeafe", "#60a5fa"));

        // Act
        var result = SiteTheme.Create(colors, SiteTheme.Default.Typography);

        // Assert
        result.FieldErrors().Keys.ShouldContain("colors.onPrimary");
        result.FieldErrors().Keys.ShouldContain("colors.primaryHover");
    }

    [Fact]
    public void Contrast_messages_name_the_colours_by_their_CSS_names_without_labels()
    {
        // Arrange
        var draft = ThemeFixtures.PaleMutedDraft();

        // Act
        var review = draft.Review();

        // Assert
        review.Errors.Select(issue => issue.Message).ShouldBe(
        [
            "muted on surface is 1.47:1 in the light scheme; text needs at least 4.5:1 (WCAG 2.2 SC 1.4.3).",
            "muted on surface-alt is 1.33:1 in the light scheme; text needs at least 4.5:1 (WCAG 2.2 SC 1.4.3).",
        ]);
    }

    [Fact]
    public void Contrast_messages_name_the_colours_by_the_labels_the_host_gives()
    {
        // Arrange
        var colors = ThemeFixtures.PaleMutedDraft().Colors;

        // Act
        var review = SiteTheme.CreateDraft(colors, SiteTheme.Default.Typography).ShouldBeSuccess().Review(ThemeFixtures.EditorLabel);
        var created = SiteTheme.Create(colors, SiteTheme.Default.Typography, colorLabel: ThemeFixtures.EditorLabel);

        // Assert
        review.Errors.Select(issue => issue.Message).ShouldBe(
        [
            "Muted text on Page background is 1.47:1 in the light scheme; text needs at least 4.5:1 (WCAG 2.2 SC 1.4.3).",
            "Muted text on Card background is 1.33:1 in the light scheme; text needs at least 4.5:1 (WCAG 2.2 SC 1.4.3).",
        ]);
        created.FieldErrors()["colors.muted"].ShouldBe(review.Errors.Select(issue => issue.Message));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void A_role_the_host_gives_no_label_keeps_its_CSS_name(string? label)
    {
        // Arrange
        var draft = ThemeFixtures.PaleMutedDraft();

        // Act
        var review = draft.Review(role => role == ThemeColorRole.Muted ? label : ThemeFixtures.EditorLabel(role));

        // Assert
        review.Errors[0].Message.ShouldStartWith("muted on Page background is ", StringComparison.Ordinal);
        review.Errors[1].Message.ShouldStartWith("muted on Card background is ", StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_colours_that_are_not_given_are_derived_by_mixing_with_the_text_colour()
    {
        // Arrange
        var defaults = SiteTheme.Default.Colors;

        // Act
        var colors = SiteColors.Create(
            primary: defaults.Primary,
            onPrimary: defaults.OnPrimary,
            accent: defaults.Accent,
            onAccent: defaults.OnAccent,
            text: defaults.Text,
            muted: defaults.Muted,
            surface: defaults.Surface,
            surfaceAlt: defaults.SurfaceAlt,
            border: defaults.Border,
            focus: defaults.Focus);

        // Assert
        colors.IsDerived(ThemeColorRole.PrimaryHover).ShouldBeTrue();
        colors.IsDerived(ThemeColorRole.Primary).ShouldBeFalse();
        colors.PrimaryHover.ShouldNotBe(colors.Primary);
        colors.PrimaryHover.Light.RelativeLuminance.ShouldBeLessThan(colors.Primary.Light.RelativeLuminance);
        colors.PrimaryHover.Dark.RelativeLuminance.ShouldBeGreaterThan(colors.Primary.Dark.RelativeLuminance);
        colors.AccentHover.Light.RelativeLuminance.ShouldBeLessThan(colors.Accent.Light.RelativeLuminance);
        SiteTheme.Create(colors, SiteTheme.Default.Typography).ShouldBeSuccess();
    }

    [Theory]
    [InlineData(0.9375)]
    [InlineData(0.875)]
    public void Body_text_smaller_than_1rem_is_an_error_unless_small_text_is_allowed(double min)
    {
        // Arrange
        var typography = ThemeFixtures.DefaultTypographyWith(TypographyRole.Body, ThemeFixtures.Sized((decimal)min, 1.125m));

        // Act
        var refused = SiteTheme.Create(SiteTheme.Default.Colors, typography);
        var allowed = SiteTheme.Create(SiteTheme.Default.Colors, typography, allowSmallText: true);

        // Assert
        refused.FieldErrors().Keys.ShouldContain("typography.body.size");
        allowed.ShouldBeSuccess().AllowsSmallText.ShouldBeTrue();
    }

    [Theory]
    [InlineData(TypographyRole.Small, "typography.small.size")]
    [InlineData(TypographyRole.H6, "typography.h6.size")]
    [InlineData(TypographyRole.Button, "typography.button.size")]
    [InlineData(TypographyRole.Nav, "typography.nav.size")]
    public void Any_other_text_smaller_than_0_875rem_is_an_error_unless_small_text_is_allowed(TypographyRole role, string field)
    {
        // Arrange
        var typography = ThemeFixtures.DefaultTypographyWith(role, ThemeFixtures.Sized(0.75m));

        // Act
        var refused = SiteTheme.Create(SiteTheme.Default.Colors, typography);
        var allowed = SiteTheme.Create(SiteTheme.Default.Colors, typography, allowSmallText: true);

        // Assert
        refused.FieldErrors().Keys.ShouldContain(field);
        allowed.FieldErrors().Keys.ShouldNotContain(field);
    }

    [Theory]
    [InlineData("1.25", 400, false)]
    [InlineData("1.25", 600, false)]
    [InlineData("1.1", 700, false)]
    [InlineData("1.1667", 700, true)]
    [InlineData("1.5", 400, true)]
    public void The_page_heading_must_be_large_text_because_it_sits_on_the_large_text_scrim(string size, int weight, bool valid)
    {
        // Arrange
        var rem = decimal.Parse(size, System.Globalization.CultureInfo.InvariantCulture);
        var style = TypographyStyle.Create(size: FontSize.Fixed(rem).ShouldBeSuccess(), weight: weight).ShouldBeSuccess();
        var typography = ThemeFixtures.DefaultTypographyWith(TypographyRole.H1, style);

        // Act
        var result = SiteTheme.Create(SiteTheme.Default.Colors, typography, allowSmallText: true);

        // Assert
        if (valid)
        {
            result.ShouldBeSuccess();
        }
        else
        {
            result.FieldErrors()["typography.h1.size"].ShouldBe(
                ["The page heading sits on a photo darkened for large text, so it must be at least 1.5rem, or 1.1667rem (14pt) at weight 700 (WCAG 2.2 SC 1.4.3)."]);
        }
    }

    [Fact]
    public void A_page_heading_size_without_a_weight_takes_the_default_bold_weight()
    {
        // Arrange
        var typography = ThemeFixtures.DefaultTypographyWith(TypographyRole.H1, ThemeFixtures.Sized(1.25m));

        // Act
        var result = SiteTheme.Create(SiteTheme.Default.Colors, typography);

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public void Small_text_at_exactly_0_875rem_and_body_at_exactly_1rem_are_allowed()
    {
        // Arrange
        var typography = SiteTheme.Default.Typography
            .With(TypographyRole.Small, ThemeFixtures.Sized(0.875m))
            .With(TypographyRole.Body, ThemeFixtures.Sized(1m));

        // Act
        var result = SiteTheme.Create(SiteTheme.Default.Colors, typography);

        // Assert
        result.ShouldBeSuccess();
    }

    [Theory]
    [InlineData(1.2, false)]
    [InlineData(1.3, true)]
    [InlineData(2.0, true)]
    [InlineData(2.1, false)]
    public void Body_line_height_must_be_from_1_3_to_2(double lineHeight, bool valid)
    {
        // Arrange
        var body = TypographyStyle.Create(lineHeight: (decimal)lineHeight).ShouldBeSuccess();
        var typography = ThemeFixtures.DefaultTypographyWith(TypographyRole.Body, body);

        // Act
        var result = SiteTheme.Create(SiteTheme.Default.Colors, typography);

        // Assert
        result.IsSuccess.ShouldBe(valid);
        if (!valid)
        {
            result.FieldErrors().Keys.ShouldContain("typography.body.lineHeight");
        }
    }

    [Fact]
    public void A_fluid_size_whose_maximum_exceeds_2_5_times_its_minimum_is_an_error()
    {
        // Arrange
        var typography = ThemeFixtures.DefaultTypographyWith(TypographyRole.H1, ThemeFixtures.Sized(1.5m, 4m));

        // Act
        var result = SiteTheme.Create(SiteTheme.Default.Colors, typography);

        // Assert
        result.FieldErrors()["typography.h1.size"].ShouldContain(message => message.Contains("1.4.4", StringComparison.Ordinal));
    }

    [Fact]
    public void A_heading_larger_than_the_level_above_is_a_warning_not_an_error()
    {
        // Arrange
        var typography = ThemeFixtures.DefaultTypographyWith(TypographyRole.H3, ThemeFixtures.Sized(2.5m, 3m));

        // Act
        var result = SiteTheme.Create(SiteTheme.Default.Colors, typography);
        var warnings = result.ShouldBeSuccess().Review().Warnings;

        // Assert
        warnings.ShouldHaveSingleItem().Field.ShouldBe("typography.h3.size");
    }

    [Fact]
    public void Heading_order_compares_the_minimum_and_the_maximum_separately()
    {
        // Arrange: h5 is fixed at 1rem, h4 is 0.9rem to 1.4rem, so h4's minimum is below h5's.
        var typography = ThemeFixtures.DefaultTypographyWith(TypographyRole.H4, ThemeFixtures.Sized(0.9m, 1.4m));

        // Act
        var warnings = SiteTheme.CreateDraft(SiteTheme.Default.Colors, typography).ShouldBeSuccess().Review().Warnings;

        // Assert
        warnings.ShouldContain(warning => warning.Field == "typography.h5.size");
    }

    [Fact]
    public void A_role_that_references_a_missing_font_is_an_error_even_for_a_draft()
    {
        // Arrange
        var style = TypographyStyle.Create(fontKey: "brand", fallback: GenericFontFamily.Serif).ShouldBeSuccess();
        var typography = ThemeFixtures.DefaultTypographyWith(TypographyRole.H2, style);

        // Act
        var draft = SiteTheme.CreateDraft(SiteTheme.Default.Colors, typography);
        var withFont = SiteTheme.Create(SiteTheme.Default.Colors, typography, [ThemeFixtures.Font("brand", "Playfair Display")]);

        // Assert
        draft.FieldErrors().Keys.ShouldContain("typography.h2.fontKey");
        withFont.ShouldBeSuccess().Fonts.ShouldHaveSingleItem().Key.ShouldBe("brand");
    }

    [Fact]
    public void Repeated_font_keys_are_an_error_on_the_later_font()
    {
        // Arrange
        ThemeFont[] fonts = [ThemeFixtures.Font("brand", "Inter"), ThemeFixtures.Font("body", "Inter"), ThemeFixtures.Font("brand", "Lora")];

        // Act
        var result = SiteTheme.CreateDraft(SiteTheme.Default.Colors, SiteTheme.Default.Typography, fonts);

        // Assert
        result.FieldErrors().Keys.ShouldBe(["fonts[2].key"]);
    }

    [Fact]
    public void A_draft_keeps_rule_violations_so_a_preview_can_show_them()
    {
        // Arrange
        var colors = ThemeFixtures.DefaultColorsWith(ThemeColorRole.Text, ThemeFixtures.Pair("#bbbbbb", "#f3f4f6"));

        // Act
        var draft = SiteTheme.CreateDraft(colors, SiteTheme.Default.Typography).ShouldBeSuccess();
        var review = draft.Review();

        // Assert
        review.HasErrors.ShouldBeTrue();
        review.Errors.ShouldAllSatisfy(error => error.Field.ShouldBeOneOf("colors.text", "colors.onPrimary"));
        review.ToValidationErrors().Keys.ShouldBe(["colors.text", "colors.onPrimary"]);
    }

    [Fact]
    public void Unset_typography_values_fall_back_to_the_default_theme()
    {
        // Arrange
        var typography = ThemeFixtures.DefaultTypographyWith(TypographyRole.H2, TypographyStyle.Create(weight: 400).ShouldBeSuccess());
        var theme = SiteTheme.Create(SiteTheme.Default.Colors, typography).ShouldBeSuccess();

        // Act
        var effective = theme.Effective(TypographyRole.H2);

        // Assert
        effective.Weight.ShouldBe(400);
        effective.Size.ShouldBe(SiteTheme.Default.Typography[TypographyRole.H2].Size);
        effective.LineHeight.ShouldBe(1.2m);
    }

    [Fact]
    public void Themes_with_the_same_values_are_equal()
    {
        // Arrange & Act
        var first = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, [ThemeFixtures.Font("brand", "Inter")]).ShouldBeSuccess();
        var second = SiteTheme.Create(SiteTheme.Default.Colors, SiteTheme.Default.Typography, [ThemeFixtures.Font("brand", "Inter")]).ShouldBeSuccess();

        // Assert
        first.ShouldBe(second);
        first.ShouldNotBe(SiteTheme.Default);
    }
}
