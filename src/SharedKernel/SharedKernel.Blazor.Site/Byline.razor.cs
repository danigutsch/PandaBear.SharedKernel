using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Site;

/// <summary>A person: a round photo, the name and the job title, in reading order.</summary>
public sealed partial class Byline
{
    /// <summary>Gets or sets the name.</summary>
    [Parameter]
    [EditorRequired]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the job title or role, for example "Founder"; null or blank renders nothing.</summary>
    [Parameter]
    public string? JobTitle { get; set; }

    /// <summary>
    /// Gets or sets the photo, loaded lazily and shown as a circle. Make it decorative when the name beside it says all it shows.
    /// </summary>
    [Parameter]
    public ResponsiveImage? Photo { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet() => ArgumentException.ThrowIfNullOrWhiteSpace(Name);
}
