namespace SharedKernel.ImageProcessing;

/// <summary>
/// Describes an input image and the variants to generate.
/// </summary>
/// <param name="Content">The source image stream.</param>
/// <param name="Variants">The requested output variants.</param>
/// <param name="Limits">The decoded image limits.</param>
public sealed record ImageProcessingRequest(
    Stream Content,
    IReadOnlyList<ImageVariantRequest> Variants,
    ImageProcessingLimits Limits);
