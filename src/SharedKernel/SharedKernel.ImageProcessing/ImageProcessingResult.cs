namespace SharedKernel.ImageProcessing;

/// <summary>
/// Contains decoded image details and generated variants.
/// </summary>
/// <param name="Width">The decoded width after orientation is applied.</param>
/// <param name="Height">The decoded height after orientation is applied.</param>
/// <param name="Variants">The generated output variants.</param>
public sealed record ImageProcessingResult(int Width, int Height, IReadOnlyList<ProcessedImageVariant> Variants);
