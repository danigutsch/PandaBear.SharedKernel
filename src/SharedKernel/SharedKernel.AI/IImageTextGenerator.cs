namespace SharedKernel.AI;

/// <summary>
/// Generates draft image accessibility text from image content and trusted context.
/// </summary>
public interface IImageTextGenerator
{
    /// <summary>
    /// Generates draft accessibility text for an image.
    /// </summary>
    /// <param name="request">The generation request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The drafted text.</returns>
    ValueTask<ImageTextGenerationResult> GenerateImageText(ImageTextGenerationRequest request, CancellationToken ct);
}
