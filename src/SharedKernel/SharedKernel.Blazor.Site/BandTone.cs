namespace SharedKernel.Blazor.Site;

/// <summary>The background of a <see cref="Band" />.</summary>
public enum BandTone
{
    /// <summary>The page background (<c>--site-color-surface</c>).</summary>
    Default = 0,

    /// <summary>The alternative background of cards (<c>--site-color-surface-alt</c>).</summary>
    Muted = 1,

    /// <summary>The primary colour, with the on-primary colour for text, in the light scheme.</summary>
    Primary = 2,

    /// <summary>
    /// A photo darkened by the normal-text scrim (<c>--site-scrim</c>), with the on-primary colour for text, in the light scheme;
    /// requires <see cref="Band.Image" />.
    /// </summary>
    Image = 3,
}
