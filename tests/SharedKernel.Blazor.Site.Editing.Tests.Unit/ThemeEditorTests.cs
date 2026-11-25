using Microsoft.AspNetCore.Components.Forms;
using SharedKernel.Results;
using static SharedKernel.Blazor.Site.Editing.Tests.ThemeEditorRendering;

namespace SharedKernel.Blazor.Site.Editing.Tests;

/// <summary>Tests the shared theme editor: its controls, their binding, their field paths and where errors land.</summary>
[Testing.TestScope(Testing.TestScope.Unit)]
public sealed class ThemeEditorTests : BunitContext
{
    [Fact]
    public void Every_field_path_has_its_control_or_group_and_every_control_is_a_listed_field()
    {
        // Arrange
        var context = new EditContext(new object());

        // Act
        var cut = this.RenderEditor(context, DefaultModel());

        // Assert
        var expected = ThemeEditor.Fields.Select(field => FieldIds.For(Prefix, field)).ToHashSet(StringComparer.Ordinal);
        var controls = cut.FindAll("input, select").Select(element => element.Id!).ToHashSet(StringComparer.Ordinal);
        var groups = cut.FindAll("fieldset[id]").Select(element => element.Id!).ToHashSet(StringComparer.Ordinal);
        expected.IsSupersetOf(controls).ShouldBeTrue();
        expected.SetEquals([.. controls, .. groups]).ShouldBeTrue();
        ThemeEditor.Fields.ShouldContain("typography.h2.color");
        ThemeEditor.Fields.ShouldContain("typography.brand.color");
        ThemeEditor.Fields.ShouldNotContain("typography.body.color");
        ThemeEditor.Fields.ShouldContain(ThemeEditor.DividerThicknessField);
    }

    [Fact]
    public void Every_control_has_a_label_and_every_colour_and_role_a_legend()
    {
        // Arrange
        var context = new EditContext(new object());

        // Act
        var cut = this.RenderEditor(context, DefaultModel());

        // Assert
        foreach (var control in cut.FindAll("input, select"))
        {
            cut.FindAll($"label[for='{control.Id}']").ShouldHaveSingleItem();
        }

        var legends = cut.FindAll("legend").Select(legend => legend.TextContent).ToArray();
        legends.ShouldContain("Primary (links, main buttons, current menu item)");
        legends.ShouldContain("Primary hover (optional)");
        legends.ShouldContain("Card background");
        legends.ShouldContain("Site name (without a logo)");
        legends.ShouldContain("Shape");
        cut.FindAll("fieldset.theme-color").Count.ShouldBe(Enum.GetValues<ThemeColorRole>().Length);
        cut.FindAll("fieldset.theme-role").Count.ShouldBe(Enum.GetValues<TypographyRole>().Length);
    }

    [Fact]
    public void Typing_a_colour_sets_the_field_draws_its_swatch_and_raises_the_change()
    {
        // Arrange
        var context = new EditContext(new object());
        var model = DefaultModel();
        var changes = 0;
        var cut = this.RenderEditor(context, model, onChange: () => changes++);

        // Act
        cut.Find(Selector("colors.primary.light")).Input("#1f4399");
        cut.Find(Selector("colors.surfaceAlt.dark")).Input("blue");

        // Assert
        model.Colors[ThemeColorRole.Primary].Light.ShouldBe("#1f4399");
        model.Colors[ThemeColorRole.SurfaceAlt].Dark.ShouldBe("blue");
        changes.ShouldBe(2);
        cut.Find($"{Selector("colors.primary")} svg rect").GetAttribute("fill").ShouldBe("#1f4399");
        cut.FindAll($"{Selector("colors.surfaceAlt")} svg").Count.ShouldBe(2);
        cut.FindAll($"{Selector("colors.surfaceAlt")} svg rect").Count.ShouldBe(1);
        cut.FindAll("svg").All(svg => svg.GetAttribute("aria-hidden") == "true").ShouldBeTrue();
        cut.FindAll("[style]").ShouldBeEmpty();
    }

    [Fact]
    public void Only_headings_card_titles_and_the_brand_offer_the_text_primary_and_muted_colours()
    {
        // Arrange
        var context = new EditContext(new object());
        var model = DefaultModel();
        var cut = this.RenderEditor(context, model);

        // Act
        cut.Find(Selector("typography.h2.color")).Change(ThemeColorRole.Primary.ToString());
        cut.Find(Selector("typography.brand.color")).Change(ThemeColorRole.Muted.ToString());

        // Assert
        model.Typography[TypographyRole.H2].Color.ShouldBe(ThemeColorRole.Primary);
        model.Typography[TypographyRole.Brand].Color.ShouldBe(ThemeColorRole.Muted);
        cut.FindAll(Selector("typography.body.color")).ShouldBeEmpty();
        cut.FindAll(Selector("typography.cardTitle.color")).ShouldHaveSingleItem();
        cut.FindAll($"{Selector("typography.h1.color")} option").Select(option => option.TextContent).ToArray()
            .ShouldBe(["Default", "Text", "Primary", "Muted text"]);
    }

    [Fact]
    public void The_shape_fields_set_the_corners_and_the_divider()
    {
        // Arrange
        var context = new EditContext(new object());
        var model = new ThemeEditorModel();
        var cut = this.RenderEditor(context, model);

        // Act
        cut.Find(Selector(ThemeEditor.ControlShapeField)).Change(ControlShape.Pill.ToString());
        cut.Find(Selector(ThemeEditor.DividerColorField)).Change(ThemeColorRole.Border.ToString());
        cut.Find(Selector(ThemeEditor.DividerWidthField)).Change("4.375");
        cut.Find(Selector(ThemeEditor.DividerThicknessField)).Change("0,1875");

        // Assert
        model.ControlShape.ShouldBe(ControlShape.Pill);
        model.DividerColor.ShouldBe(ThemeColorRole.Border);
        model.DividerWidth.ShouldBe(4.375m);
        model.DividerThickness.ShouldBe(0.1875m);
        cut.FindAll($"{Selector(ThemeEditor.DividerColorField)} option").Select(option => option.TextContent).ToArray()
            .ShouldBe(["Default", "Primary", "Accent", "Text", "Muted text", "Borders"]);
        cut.FindAll($"{Selector(ThemeEditor.ControlShapeField)} option").Select(option => option.TextContent).ToArray()
            .ShouldBe(["Default", "Rounded", "Square", "Pill (round ends)"]);
    }

    [Fact]
    public void A_number_takes_a_point_or_a_comma_and_an_empty_value_clears_it()
    {
        // Arrange
        var context = new EditContext(new object());
        var model = DefaultModel();
        var changes = 0;
        var cut = this.RenderEditor(context, model, onChange: () => changes++);

        // Act
        cut.Find(Selector("typography.h1.size")).Change("2,25");
        cut.Find(Selector("typography.h1.sizeMax")).Change(" 3.25 ");
        cut.Find(Selector("typography.h1.letterSpacing")).Change("-0.02");
        cut.Find(Selector(ThemeEditor.BrandHeightField)).Change(string.Empty);

        // Assert
        model.Typography[TypographyRole.H1].SizeMin.ShouldBe(2.25m);
        model.Typography[TypographyRole.H1].SizeMax.ShouldBe(3.25m);
        model.Typography[TypographyRole.H1].LetterSpacing.ShouldBe(-0.02m);
        model.BrandHeight.ShouldBeNull();
        changes.ShouldBe(4);
        cut.Find(Selector("typography.h1.size")).GetAttribute("inputmode").ShouldBe("decimal");
        cut.Find(Selector("typography.h1.letterSpacing")).HasAttribute("inputmode").ShouldBeFalse();
        cut.Find(Selector("typography.body.lineHeight")).GetAttribute("value").ShouldBe("1.6");
    }

    [Theory]
    [InlineData("1.2.5")]
    [InlineData("1,2.5")]
    [InlineData("wide")]
    public async Task Text_that_is_not_a_number_keeps_the_value_and_is_an_error_on_its_field_when_the_form_validates(string text)
    {
        // Arrange
        var context = new EditContext(new object());
        var model = DefaultModel();
        var changes = 0;
        var cut = this.RenderEditor(context, model, onChange: () => changes++);
        await cut.Find(Selector(ThemeEditor.DividerWidthField)).ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = text });

        // Act
        var valid = await cut.InvokeAsync(() => context.ValidateAsync(Xunit.TestContext.Current.CancellationToken));

        // Assert
        valid.ShouldBeFalse();
        model.DividerWidth.ShouldBe(SiteShape.DefaultDividerWidth);
        changes.ShouldBe(0);
        var input = cut.Find(Selector(ThemeEditor.DividerWidthField));
        input.GetAttribute("value").ShouldBe(text);
        input.GetAttribute("aria-invalid").ShouldBe("true");
        cut.Find($"#{input.Id}-error").TextContent.ShouldBe("Divider width must be a number, like 4.");
        context.GetValidationMessages(context.Field(ThemeEditor.DividerWidthField)).ShouldBe(["Divider width must be a number, like 4."]);
    }

    [Fact]
    public async Task Rule_errors_of_a_save_land_on_their_controls_and_the_summary_links_to_them()
    {
        // Arrange
        var context = new EditContext(new object());
        using var submission = ServerFormSubmission.ForRecord(context, ThemeEditor.Fields, Prefix, "theme");
        var cut = this.RenderEditor(context, DefaultModel());
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["shape.dividerThickness"] = ["The divider must be at most 1rem thick."],
            ["typography.h2.color"] = ["The colour must be the text, primary or muted colour."],
            ["colors.muted"] = ["Muted text on Card background is 1.33:1."],
        };

        // Act
        var saved = await cut.InvokeAsync(() => submission.Submit(() => Task.FromResult(Result.Invalid("The theme breaks a rule.", errors))));

        // Assert
        saved.ShouldBeFalse();
        submission.Error.ShouldBeNull();
        submission.Summary.Select(item => item.TargetId).Order(StringComparer.Ordinal).ToArray().ShouldBe(
            [FieldIds.For(Prefix, "colors.muted"), FieldIds.For(Prefix, "shape.dividerThickness"), FieldIds.For(Prefix, "typography.h2.color")]);
        cut.Find(Selector("shape.dividerThickness")).GetAttribute("aria-invalid").ShouldBe("true");
        cut.Find($"{Selector("shape.dividerThickness")}-error").TextContent.ShouldBe("The divider must be at most 1rem thick.");
        cut.Find(Selector("typography.h2.color")).GetAttribute("aria-describedby").ShouldBe(FieldIds.For(Prefix, "typography.h2.color") + "-error");
        cut.Find(Selector("colors.muted")).GetAttribute("tabindex").ShouldBe("-1");
        cut.Find($"{Selector("colors.muted")} .validation-message").TextContent.ShouldBe("Muted text on Card background is 1.33:1.");
    }

    [Fact]
    public void The_font_list_offers_the_families_and_keeps_a_key_that_is_not_offered()
    {
        // Arrange
        var context = new EditContext(new object());
        var model = DefaultModel();
        model.Typography[TypographyRole.H1].FontKey = "retired";
        var cut = this.RenderEditor(context, model, fonts: [new ThemeFontOption("noto-sans", "Noto Sans"), new ThemeFontOption("lora", "Lora")]);

        // Act
        cut.Find(Selector("typography.body.fontKey")).Change("lora");
        cut.Find(Selector("typography.h2.fontKey")).Change("noto-sans");
        cut.Find(Selector("typography.h2.fontKey")).Change(string.Empty);

        // Assert
        model.Typography[TypographyRole.Body].FontKey.ShouldBe("lora");
        model.Typography[TypographyRole.H2].FontKey.ShouldBeNull();
        cut.FindAll($"{Selector("typography.h1.fontKey")} option").Select(option => option.TextContent).ToArray()
            .ShouldBe(["Default (system font)", "Noto Sans", "Lora", "retired"]);
        cut.FindAll($"{Selector("typography.h3.fontKey")} option").Count.ShouldBe(3);
    }

    [Fact]
    public void The_select_fields_set_fallback_weight_case_and_style()
    {
        // Arrange
        var context = new EditContext(new object());
        var model = new ThemeEditorModel();
        var cut = this.RenderEditor(context, model);

        // Act
        cut.Find(Selector("typography.body.fallback")).Change(GenericFontFamily.Serif.ToString());
        cut.Find(Selector("typography.nav.weight")).Change("600");
        cut.Find(Selector("typography.h2.transform")).Change(ThemeTextTransform.Uppercase.ToString());
        cut.Find(Selector("typography.small.italic")).Change("italic");
        cut.Find(Selector("typography.link.italic")).Change("normal");
        cut.Find(Selector("typography.h3.italic")).Change("italic");
        cut.Find(Selector("typography.h3.italic")).Change(string.Empty);

        // Assert
        model.Typography[TypographyRole.Body].Fallback.ShouldBe(GenericFontFamily.Serif);
        model.Typography[TypographyRole.Nav].Weight.ShouldBe(600);
        model.Typography[TypographyRole.H2].Transform.ShouldBe(ThemeTextTransform.Uppercase);
        model.Typography[TypographyRole.Small].Italic.ShouldBe(true);
        model.Typography[TypographyRole.Link].Italic.ShouldBe(false);
        model.Typography[TypographyRole.H3].Italic.ShouldBeNull();
        cut.Find(Selector("typography.small.italic")).GetAttribute("value").ShouldBe("italic");
        cut.Find(Selector("typography.link.italic")).GetAttribute("value").ShouldBe("normal");
        cut.FindAll($"{Selector("typography.nav.weight")} option").Count.ShouldBe(10);
    }

    [Fact]
    public void The_small_text_switch_is_a_labelled_checkbox()
    {
        // Arrange
        var context = new EditContext(new object());
        var model = new ThemeEditorModel();
        var cut = this.RenderEditor(context, model);

        // Act
        cut.Find(Selector(ThemeEditor.AllowSmallTextField)).Change(true);

        // Assert
        model.AllowSmallText.ShouldBeTrue();
        cut.Find($"label[for='{FieldIds.For(Prefix, ThemeEditor.AllowSmallTextField)}']").TextContent
            .ShouldBe("Allow body text below 1rem and other text below 0.875rem");
    }

    [Fact]
    public async Task The_host_texts_and_names_replace_the_defaults()
    {
        // Arrange
        var context = new EditContext(new object());
        var texts = new ThemeEditorTexts
        {
            ColorsLegend = "Cores",
            ColorName = role => role == ThemeColorRole.Muted ? "Texto suave" : role.ToString(),
            ColorHint = _ => null,
            TypographyName = role => "Papel " + role,
            DividerWidth = "Largura do divisor (rem)",
            NotANumber = (name, example) => $"{name}: use um número como {example}.",
        };
        var cut = this.RenderEditor(context, DefaultModel(), texts: texts);
        await cut.Find(Selector(ThemeEditor.DividerWidthField)).ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "x" });

        // Act
        await cut.InvokeAsync(() => context.ValidateAsync(Xunit.TestContext.Current.CancellationToken));

        // Assert
        cut.FindAll("legend").Select(legend => legend.TextContent).ShouldContain("Cores");
        cut.FindAll("legend").Select(legend => legend.TextContent).ShouldContain("Primary");
        cut.FindAll("legend").Select(legend => legend.TextContent).ShouldContain("Papel H1");
        cut.Find($"{Selector(ThemeEditor.DividerColorField)} option[value='Muted']").TextContent.ShouldBe("Texto suave");
        cut.Find($"{Selector(ThemeEditor.DividerWidthField)}-error").TextContent.ShouldBe("Largura do divisor: use um número como 4.");
    }

    [Fact]
    public void A_new_model_shows_its_numbers()
    {
        // Arrange
        var context = new EditContext(new object());
        var model = DefaultModel();
        var cut = this.RenderEditor(context, model);
        var replacement = DefaultModel();
        replacement.BrandHeight = 3.5m;

        // Act
        cut.FindComponent<ThemeEditor>().Render(parameters => parameters.Add(item => item.Model, replacement));

        // Assert
        cut.Find(Selector(ThemeEditor.BrandHeightField)).GetAttribute("value").ShouldBe("3.5");
    }

    [Fact]
    public async Task Disposing_the_editor_removes_its_number_errors()
    {
        // Arrange
        var context = new EditContext(new object());
        var cut = this.RenderEditor(context, DefaultModel());
        await cut.Find(Selector(ThemeEditor.DividerWidthField)).ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "x" });
        await cut.InvokeAsync(() => context.ValidateAsync(Xunit.TestContext.Current.CancellationToken));

        // Act
        cut.FindComponent<ThemeEditor>().Instance.Dispose();

        // Assert
        context.GetValidationMessages(context.Field(ThemeEditor.DividerWidthField)).ShouldBeEmpty();
    }

    [Fact]
    public void The_editor_needs_an_edit_context_a_model_texts_and_an_ID_prefix()
    {
        // Arrange
        Action withoutContext = () => Render<ThemeEditor>(parameters => parameters
            .Add(item => item.Model, DefaultModel())
            .Add(item => item.IdPrefix, Prefix));
        Action withoutModel = () => this.RenderEditor(new EditContext(new object()), null!);
        Action withoutTexts = () => Render<Microsoft.AspNetCore.Components.CascadingValue<EditContext>>(parameters => parameters
            .Add(value => value.Value, new EditContext(new object()))
            .AddChildContent<ThemeEditor>(editor => editor.Add(item => item.Model, DefaultModel()).Add(item => item.IdPrefix, Prefix).Add(item => item.Texts, null!)));
        Action withoutPrefix = () => Render<Microsoft.AspNetCore.Components.CascadingValue<EditContext>>(parameters => parameters
            .Add(value => value.Value, new EditContext(new object()))
            .AddChildContent<ThemeEditor>(editor => editor.Add(item => item.Model, DefaultModel()).Add(item => item.IdPrefix, " ")));

        // Act & Assert
        withoutContext.ShouldThrow<InvalidOperationException>();
        withoutModel.ShouldThrow<ArgumentNullException>();
        withoutTexts.ShouldThrow<ArgumentNullException>();
        withoutPrefix.ShouldThrow<ArgumentException>();
    }
}
