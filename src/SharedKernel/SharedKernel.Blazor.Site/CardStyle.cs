namespace SharedKernel.Blazor.Site;

/// <summary>The look of a <see cref="Card" />.</summary>
public enum CardStyle
{
    /// <summary>A box: the alternative background, a border and the surface radius around the image and the text.</summary>
    Boxed = 0,

    /// <summary>
    /// No box: no background, no border, the colour of the surroundings, and the image with the surface radius of its own
    /// (<c>site-card--plain</c>).
    /// </summary>
    Plain = 1,
}
