namespace SharedKernel.ImageProcessing;

/// <summary>
/// Contains an encoded image variant.
/// </summary>
/// <param name="Name">The caller-defined variant name.</param>
/// <param name="Format">The encoded output format.</param>
/// <param name="Width">The encoded width in pixels.</param>
/// <param name="Height">The encoded height in pixels.</param>
/// <param name="Content">The encoded bytes.</param>
public sealed record ProcessedImageVariant(string Name, ImageOutputFormat Format, int Width, int Height, ReadOnlyMemory<byte> Content);
