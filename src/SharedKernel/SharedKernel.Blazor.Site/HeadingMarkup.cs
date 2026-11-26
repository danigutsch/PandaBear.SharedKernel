using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site;

/// <summary>Renders an <c>h1</c> to <c>h6</c> element for a level the caller configures.</summary>
internal static class HeadingMarkup
{
    /// <summary>Checks a heading level.</summary>
    /// <param name="level">The level.</param>
    /// <param name="parameterName">The parameter name reported on failure.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level" /> is not from 1 to 6.</exception>
    public static void RequireLevel(int level, string parameterName)
    {
        if (level is < 1 or > 6)
        {
            throw new ArgumentOutOfRangeException(parameterName, level, "A heading level must be from 1 to 6.");
        }
    }

    /// <summary>Creates the heading fragment.</summary>
    /// <param name="level">The level, already checked with <see cref="RequireLevel" />.</param>
    /// <param name="cssClass">The CSS class.</param>
    /// <param name="id">The element id, if any.</param>
    /// <param name="content">The heading content.</param>
    /// <param name="language">The language tag of the heading text when it differs from the page's, written as <c>lang</c>; checked with <see cref="LanguageTag.Require" />.</param>
    /// <returns>The fragment.</returns>
    public static RenderFragment Render(int level, string cssClass, string? id, RenderFragment content, string? language = null) => builder =>
    {
        builder.OpenElement(0, "h" + level.ToString(CultureInfo.InvariantCulture));
        builder.AddAttribute(1, "id", id);
        builder.AddAttribute(2, "class", cssClass);
        builder.AddAttribute(3, "lang", language);
        builder.AddContent(4, content);
        builder.CloseElement();
    };
}
