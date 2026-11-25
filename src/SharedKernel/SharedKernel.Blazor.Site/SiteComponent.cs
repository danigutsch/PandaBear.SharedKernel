using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// Base class of the site components: a <c>class</c> parameter and pass-through of unmatched attributes to the root element.
/// An unmatched attribute that cannot be an HTML attribute meant for the root element throws <see cref="InvalidOperationException" />
/// before rendering, so a misspelled or removed parameter fails loudly instead of being written to the markup.
/// </summary>
/// <remarks>
/// Blazor matches parameters case-insensitively, so an unmatched name is never a current parameter. It is refused when it starts
/// with an upper-case letter (parameters are PascalCase, HTML attributes are written in lower case), when it names a removed
/// parameter in any letter case (<c>ImageSrc</c>, <c>ImageAlt</c>, <c>ImageWidth</c>, <c>ImageHeight</c>, replaced by <c>Image</c>),
/// or when it holds a character an HTML attribute name cannot hold (white space, quotes, <c>&gt;</c>, <c>/</c>, <c>=</c> or a control
/// character).
/// </remarks>
public abstract class SiteComponent : ComponentBase
{
    private const string ImageReplacement = "Image (a ResponsiveImage)";

    private static readonly Dictionary<string, string> RemovedParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ImageSrc"] = ImageReplacement,
        ["ImageAlt"] = ImageReplacement,
        ["ImageWidth"] = ImageReplacement,
        ["ImageHeight"] = ImageReplacement,
    };

    /// <summary>Gets or sets CSS classes added to the root element after the component's own classes.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>Gets or sets additional HTML attributes applied to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Sets the parameters, then refuses an unmatched attribute that is a misspelled or removed parameter.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns>The task of the base lifecycle.</returns>
    /// <exception cref="InvalidOperationException">An unmatched attribute is not an HTML attribute for the root element.</exception>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        parameters.SetParameterProperties(this);
        RequireHtmlAttributes();
        return base.SetParametersAsync(ParameterView.Empty);
    }

    /// <summary>Combines the component's own class with <see cref="Class" />.</summary>
    /// <param name="own">The component's own class list.</param>
    /// <returns>The class attribute value.</returns>
    protected string CssClass(string own) => string.IsNullOrWhiteSpace(Class) ? own : own + " " + Class;

    private void RequireHtmlAttributes()
    {
        if (AdditionalAttributes is null)
        {
            return;
        }

        var component = GetType().Name;
        foreach (var name in AdditionalAttributes.Keys)
        {
            if (RemovedParameters.TryGetValue(name, out var replacement))
            {
                throw new InvalidOperationException($"{component} has no parameter '{name}'; it was removed, use {replacement}.");
            }

            if (name.Length > 0 && char.IsAsciiLetterUpper(name[0]))
            {
                throw new InvalidOperationException(
                    $"{component} has no parameter '{name}'. Unmatched attributes are written to the root element, and a name that starts with an upper-case letter is taken for a misspelled or removed parameter; write HTML attributes in lower case.");
            }

            if (name.Length == 0 || name.Any(character => char.IsWhiteSpace(character) || char.IsControl(character) || character is '"' or '\'' or '>' or '/' or '='))
            {
                throw new InvalidOperationException($"{component} received an attribute name that is not a valid HTML attribute name.");
            }
        }
    }
}
