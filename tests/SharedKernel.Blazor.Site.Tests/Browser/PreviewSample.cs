using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using PandaBear.Blazor.Site.Theming;
using PandaBear.Blazor.Site.Tests.Theming;

namespace PandaBear.Blazor.Site.Tests.Browser;

/// <summary>
/// The interactive part of the browser test: a <see cref="ThemePreview" /> with the given nonce, a button that changes the text colour
/// (a diff that edits the existing style element's text) and a button that replaces the preview (a new style element inserted by script).
/// </summary>
public sealed class PreviewSample : ComponentBase
{
    /// <summary>The light text colour the change button applies.</summary>
    public const string ChangedText = "#0f766e";

    private SiteTheme _theme = SiteTheme.Default;
    private int _generation;

    /// <summary>Gets or sets the nonce the preview renders.</summary>
    [Parameter]
    public string? Nonce
    {
        get; set;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenElement(0, "main");
        builder.AddAttribute(1, "data-interactive", RendererInfo.IsInteractive ? "true" : "false");
        builder.AddAttribute(2, "data-generation", _generation);
        builder.OpenElement(3, "button");
        builder.AddAttribute(4, "type", "button");
        builder.AddAttribute(5, "id", "change");
        builder.AddAttribute(6, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, ChangeText));
        builder.AddContent(7, "Change the text colour");
        builder.CloseElement();
        builder.OpenElement(8, "button");
        builder.AddAttribute(9, "type", "button");
        builder.AddAttribute(10, "id", "replace");
        builder.AddAttribute(11, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => _generation++));
        builder.AddContent(12, "Replace the preview");
        builder.CloseElement();
        builder.OpenComponent<ThemePreview>(13);
        builder.SetKey(_generation);
        builder.AddComponentParameter(14, nameof(ThemePreview.Theme), _theme);
        builder.AddComponentParameter(15, nameof(ThemePreview.Nonce), Nonce);
        builder.AddComponentParameter(16, nameof(ThemePreview.ScopeId), "preview");
        builder.CloseComponent();
        builder.CloseElement();
    }

    private void ChangeText()
    {
        var colors = ThemeFixtures.DefaultColorsWith(ThemeColorRole.Text, ThemeFixtures.Pair(ChangedText, "#f9fafb"));
        _theme = SiteTheme.CreateDraft(colors, SiteTheme.Default.Typography).Value;
    }
}
