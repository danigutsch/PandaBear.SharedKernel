using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>Base class of the site components: a <c>class</c> parameter and pass-through of unmatched attributes to the root element.</summary>
public abstract class SiteComponent : ComponentBase
{
    /// <summary>Gets or sets CSS classes added to the root element after the component's own classes.</summary>
    [Parameter]
    public string? Class
    {
        get; set;
    }

    /// <summary>Gets or sets additional attributes applied to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes
    {
        get; set;
    }

    /// <summary>Combines the component's own class with <see cref="Class" />.</summary>
    /// <param name="own">The component's own class list.</param>
    /// <returns>The class attribute value.</returns>
    protected string CssClass(string own) => string.IsNullOrWhiteSpace(Class) ? own : own + " " + Class;
}
