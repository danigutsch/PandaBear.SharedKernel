namespace SharedKernel.BuildingBlocks;

/// <summary>
/// The application's knowledge of its images, which content rules read: whether an image exists, and whether it may be shown in content
/// published in a culture. One application asks for alt text, or a decorative mark, in that culture; another asks that the image is
/// processed and its text reviewed.
/// </summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
public interface IImageEligibility<in TImageId>
{
    /// <summary>Determines whether an image exists.</summary>
    /// <param name="imageId">The image.</param>
    /// <returns><see langword="true"/> when it exists.</returns>
    bool Exists(TImageId imageId);

    /// <summary>Determines whether an image may be shown in content published in a culture.</summary>
    /// <param name="imageId">The image.</param>
    /// <param name="culture">The culture of the published content.</param>
    /// <returns><see langword="true"/> when it may.</returns>
    bool IsEligible(TImageId imageId, ContentCulture culture);
}
