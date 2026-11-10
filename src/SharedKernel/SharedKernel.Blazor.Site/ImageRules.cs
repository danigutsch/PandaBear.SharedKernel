namespace PandaBear.Blazor.Site;

/// <summary>The safety, accessibility and layout rules shared by the components that show an optional image.</summary>
internal static class ImageRules
{
    /// <summary>
    /// Checks an optional image: with a source, the source must be root-relative or https, and the image needs an accessibility
    /// decision and a positive width and height, so the layout reserves its space before it loads.
    /// </summary>
    /// <param name="src">The image address, if any.</param>
    /// <param name="alternative">The accessibility decision.</param>
    /// <param name="width">The intrinsic width in pixels, if any.</param>
    /// <param name="height">The intrinsic height in pixels, if any.</param>
    /// <param name="prefix">
    /// The prefix of the parameter names in failure messages: <c>Image</c> names <c>ImageSrc</c>, <c>ImageAlt</c>, <c>ImageWidth</c>
    /// and <c>ImageHeight</c>; <c>GalleryImage.</c> names <c>GalleryImage.Src</c> and so on.
    /// </param>
    /// <param name="parameterName">The parameter reported by argument exceptions, or <see langword="null" /> for the named one.</param>
    /// <exception cref="ArgumentException">The source is not root-relative or https, or a dimension is missing.</exception>
    /// <exception cref="InvalidOperationException">There is an image source and no <paramref name="alternative" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    public static void Check(LinkTarget? src, ImageAlternative? alternative, int? width, int? height, string prefix, string? parameterName = null)
    {
        if (src is null)
        {
            return;
        }

        if (!src.IsImageSource)
        {
            throw new ArgumentException($"{prefix}Src must be a root-relative or https address.", parameterName ?? prefix + "Src");
        }

        if (alternative is null)
        {
            throw new InvalidOperationException($"An image needs an accessibility decision: set {prefix}Alt to ImageAlternative.Describe(...) or ImageAlternative.Decorative().");
        }

        if (width is null)
        {
            throw new ArgumentException($"An image needs {prefix}Width and {prefix}Height so the layout can reserve its space.", parameterName ?? prefix + "Width");
        }

        if (height is null)
        {
            throw new ArgumentException($"An image needs {prefix}Width and {prefix}Height so the layout can reserve its space.", parameterName ?? prefix + "Height");
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName ?? prefix + "Width", width, "An image dimension must be positive so the layout can reserve its space.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName ?? prefix + "Height", height, "An image dimension must be positive so the layout can reserve its space.");
        }
    }
}
