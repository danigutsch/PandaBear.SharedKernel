using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace SharedKernel.Blazor.Site.Editing.Tests;

/// <summary>Renders the theme editor inside an edit context, as a host's form does.</summary>
internal static class ThemeEditorRendering
{
    /// <summary>The id prefix the tests use.</summary>
    public const string Prefix = "site-theme";

    /// <summary>Renders the editor of a model with the id prefix <see cref="Prefix" />.</summary>
    /// <param name="context">The test context.</param>
    /// <param name="editContext">The form's edit context.</param>
    /// <param name="model">The theme fields.</param>
    /// <param name="fonts">The offered font families.</param>
    /// <param name="texts">The texts, or the defaults.</param>
    /// <param name="onChange">Called after every change.</param>
    /// <returns>The rendered tree.</returns>
    public static IRenderedComponent<CascadingValue<EditContext>> RenderEditor(
        this BunitContext context,
        EditContext editContext,
        ThemeEditorModel model,
        IReadOnlyList<ThemeFontOption>? fonts = null,
        ThemeEditorTexts? texts = null,
        Action? onChange = null) =>
        context.Render<CascadingValue<EditContext>>(parameters => parameters
            .Add(value => value.Value, editContext)
            .AddChildContent<ThemeEditor>(editor => editor
                .Add(item => item.Model, model)
                .Add(item => item.IdPrefix, Prefix)
                .Add(item => item.Fonts, fonts)
                .Add(item => item.Texts, texts ?? new ThemeEditorTexts())
                .Add(item => item.OnChange, onChange ?? (() => { }))));

    /// <summary>Gets the control id of a field path.</summary>
    /// <param name="field">The field path.</param>
    /// <returns>The id with a leading <c>#</c>, for a selector.</returns>
    public static string Selector(string field) => "#" + FieldIds.For(Prefix, field);

    /// <summary>Creates the fields of the default theme.</summary>
    /// <returns>The model.</returns>
    public static ThemeEditorModel DefaultModel() => ThemeEditorModel.FromTheme(SiteTheme.Default);
}
