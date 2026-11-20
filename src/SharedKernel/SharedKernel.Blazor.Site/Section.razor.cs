using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>
/// A page section with a heading, an optional intro and a decorative divider. With <see cref="HeadingId" /> the heading gets that id
/// and the section is labelled by it (<c>aria-labelledby</c>), which makes it a named region landmark; without it no id is generated,
/// so the markup is the same during prerendering and in the interactive circuit.
/// </summary>
public sealed partial class Section
{
    /// <summary>Gets or sets the heading text.</summary>
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the id of the heading, unique on the page and without white space. When set, the section is labelled by its heading.
    /// </summary>
    [Parameter]
    public string? HeadingId { get; set; }

    /// <summary>Gets or sets the heading level, from 1 to 6. The default is 2.</summary>
    [Parameter]
    public int Level { get; set; } = 2;

    /// <summary>Gets or sets the introduction under the heading.</summary>
    [Parameter]
    public string? Intro { get; set; }

    /// <summary>Gets or sets whether the decorative divider is shown. The default is <see langword="true" />.</summary>
    [Parameter]
    public bool Divider { get; set; } = true;

    /// <summary>
    /// Gets or sets content beside the text, such as photos. The section's content is then a <see cref="Split" />: the heading, intro
    /// and body in its first column and the media in the second from the <c>48rem</c> breakpoint on, the media after them below it,
    /// so reading order matches visual order.
    /// </summary>
    [Parameter]
    public RenderFragment? Media { get; set; }

    /// <summary>Gets or sets the section body.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        Heading.RequireLevel(Level, nameof(Level));
        if (HeadingId is not null && (HeadingId.Length == 0 || HeadingId.AsSpan().ContainsAnyWhiteSpace()))
        {
            throw new ArgumentException("A heading id must not be empty or contain white space.", nameof(HeadingId));
        }
    }
}
