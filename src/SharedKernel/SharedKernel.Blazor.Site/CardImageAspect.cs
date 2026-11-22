namespace PandaBear.Blazor.Site;

/// <summary>The aspect ratio a <see cref="Card" /> crops its image to.</summary>
public enum CardImageAspect
{
    /// <summary>4:3, the default.</summary>
    FourByThree = 0,

    /// <summary>16:9, a wide photo (<c>site-card--aspect-16x9</c>).</summary>
    SixteenByNine = 1,

    /// <summary>1:1 (<c>site-card--aspect-1x1</c>).</summary>
    Square = 2,

    /// <summary>The image's own ratio, never cropped (<c>site-card--aspect-natural</c>).</summary>
    Natural = 3,
}
