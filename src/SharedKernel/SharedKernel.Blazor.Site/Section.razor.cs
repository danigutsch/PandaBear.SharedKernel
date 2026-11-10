using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>A page section labelled by its heading, with an optional intro and a decorative divider.</summary>
public sealed partial class Section
{
    private readonly string _headingId = "site-section-" + Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the heading text.</summary>
    [Parameter]
    [EditorRequired]
    public string Title
    {
        get; set;
    } = string.Empty;

    /// <summary>Gets or sets the heading level, from 1 to 6. The default is 2.</summary>
    [Parameter]
    public int Level
    {
        get; set;
    } = 2;

    /// <summary>Gets or sets the introduction under the heading.</summary>
    [Parameter]
    public string? Intro
    {
        get; set;
    }

    /// <summary>Gets or sets whether the decorative divider is shown. The default is <see langword="true" />.</summary>
    [Parameter]
    public bool Divider
    {
        get; set;
    } = true;

    /// <summary>Gets or sets the section body.</summary>
    [Parameter]
    public RenderFragment? ChildContent
    {
        get; set;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        Heading.RequireLevel(Level, nameof(Level));
    }
}
