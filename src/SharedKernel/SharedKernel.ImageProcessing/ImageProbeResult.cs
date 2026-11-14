namespace PandaBear.ImageProcessing;

/// <summary>
/// Describes an input image as read from its headers, without decoding pixel data.
/// </summary>
/// <param name="Format">The decoded image format.</param>
/// <param name="Width">The stored width in pixels, before EXIF orientation is applied.</param>
/// <param name="Height">The stored height in pixels, before EXIF orientation is applied.</param>
/// <param name="IsAnimated">Whether the container declares more than one frame or an animation.</param>
public sealed record ImageProbeResult(ImageInputFormat Format, int Width, int Height, bool IsAnimated);
