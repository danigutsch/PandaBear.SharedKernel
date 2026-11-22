namespace PandaBear.Blazor.Site;

/// <summary>The decoration behind the image of a <see cref="CardStyle.Plain" /> card.</summary>
public enum CardImageShadow
{
    /// <summary>No shadow, the default.</summary>
    None = 0,

    /// <summary>
    /// A hard block of the border colour offset one spacing step (<c>--site-space-2</c>) towards the end and the bottom
    /// (<c>site-card--image-shadow</c>). It is decorative and needs no contrast. A boxed card refuses it, because its box would clip it.
    /// </summary>
    Offset = 1,
}
