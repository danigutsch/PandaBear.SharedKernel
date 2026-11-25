namespace SharedKernel.ImageProcessing;

/// <summary>
/// The outcome of a header-only animation check. Treat every value other than <see cref="Static" /> as a rejection when
/// animated images are not allowed.
/// </summary>
public enum ImageAnimationStatus
{
    /// <summary>The container is truncated, inconsistent, or exceeds the chunk walk bound. This is the default value.</summary>
    Malformed,

    /// <summary>The container is well formed up to the image data and declares no animation.</summary>
    Static,

    /// <summary>The container declares an animation.</summary>
    Animated,

    /// <summary>The content is not a PNG or WebP container.</summary>
    Unsupported
}
