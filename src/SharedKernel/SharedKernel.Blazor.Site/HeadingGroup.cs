using Microsoft.AspNetCore.Components;

namespace SharedKernel.Blazor.Site;

/// <summary>
/// Renders a heading with an optional eyebrow above it and subtitle under it, the one heading group of a <see cref="Card" />, a
/// <see cref="Hero" />, a <see cref="Section" /> and a <see cref="StepList" /> step. With either line the heading is an <c>hgroup</c> of
/// the eyebrow <c>p</c>, the heading and the subtitle <c>p</c>, in reading order, so neither line enters the outline or the heading's
/// name; with neither it is the heading alone. A null or blank line renders nothing, and the text is encoded.
/// </summary>
internal static class HeadingGroup
{
    /// <summary>Creates the heading group fragment.</summary>
    /// <param name="classes">The class names of the component that renders it.</param>
    /// <param name="eyebrow">The short line above the heading, or <see langword="null" />.</param>
    /// <param name="heading">The heading element.</param>
    /// <param name="subtitle">The line under the heading, or <see langword="null" />.</param>
    /// <returns>The fragment.</returns>
    public static RenderFragment Render(HeadingGroupClasses classes, string? eyebrow, RenderFragment heading, string? subtitle = null) => builder =>
    {
        var hasEyebrow = !string.IsNullOrWhiteSpace(eyebrow);
        var hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
        if (!hasEyebrow && !hasSubtitle)
        {
            builder.AddContent(0, heading);
            return;
        }

        builder.OpenElement(1, "hgroup");
        builder.AddAttribute(2, "class", classes.Group);
        if (hasEyebrow)
        {
            builder.OpenElement(3, "p");
            builder.AddAttribute(4, "class", classes.Eyebrow);
            builder.AddContent(5, eyebrow);
            builder.CloseElement();
        }

        builder.AddContent(6, heading);
        if (hasSubtitle)
        {
            builder.OpenElement(7, "p");
            builder.AddAttribute(8, "class", classes.Subtitle);
            builder.AddContent(9, subtitle);
            builder.CloseElement();
        }

        builder.CloseElement();
    };
}
