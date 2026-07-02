namespace SharedKernel.ImageProcessing;

/// <summary>
/// Describes one encoded image output variant.
/// </summary>
/// <param name="Name">The caller-defined variant name.</param>
/// <param name="Format">The encoded output format.</param>
/// <param name="MaxWidth">The maximum output width in pixels.</param>
/// <param name="Quality">The encoder quality from 1 through 100.</param>
/// <param name="MaxHeight">The optional maximum output height in pixels.</param>
public sealed record ImageVariantRequest(
    string Name,
    ImageOutputFormat Format,
    int MaxWidth,
    int Quality,
    int? MaxHeight = null);
