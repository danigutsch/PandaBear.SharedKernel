namespace PandaBear.Blazor.Site;

/// <summary>The accessibility and layout rules shared by the components that show an optional image.</summary>
internal static class ImageRules
{
    /// <summary>Checks an optional image: with a source it needs an accessibility decision and positive dimensions.</summary>
    /// <param name="src">The image address, if any.</param>
    /// <param name="alternative">The accessibility decision.</param>
    /// <param name="width">The intrinsic width in pixels, if any.</param>
    /// <param name="height">The intrinsic height in pixels, if any.</param>
    /// <param name="alternativeName">The name of the alternative parameter, used in the failure message.</param>
    /// <exception cref="InvalidOperationException">There is an image source and no <paramref name="alternative" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    public static void Check(LinkTarget? src, ImageAlternative? alternative, int? width, int? height, string alternativeName)
    {
        if (src is null)
        {
            return;
        }

        if (alternative is null)
        {
            throw new InvalidOperationException($"An image needs an accessibility decision: set {alternativeName} to ImageAlternative.Describe(...) or ImageAlternative.Decorative().");
        }

        if (width is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "An image dimension must be positive so the layout can reserve its space.");
        }

        if (height is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "An image dimension must be positive so the layout can reserve its space.");
        }
    }
}
