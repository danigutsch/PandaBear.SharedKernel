using SharedKernel.BuildingBlocks;

namespace SharedKernel.Offerings;

/// <summary>Reads the publish rule of offerings.</summary>
public static class OfferingPublishing
{
    /// <summary>
    /// Lists the images that keep an offering from being published in a culture: those that do not exist or are not eligible there, each
    /// once, in order. Pass <see cref="Offering{TImageId, TPriceKind, TMetric}.ImageIds"/>. An editor can name them; the validation error
    /// only counts them.
    /// </summary>
    /// <typeparam name="TImageId">The image id type.</typeparam>
    /// <param name="imageIds">The images the offering shows.</param>
    /// <param name="culture">The culture to publish in.</param>
    /// <param name="images">The application's image knowledge.</param>
    /// <returns>The images that are not eligible.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static IReadOnlyList<TImageId> FindIneligible<TImageId>(IEnumerable<TImageId> imageIds, ContentCulture culture, IImageEligibility<TImageId> images)
        where TImageId : struct, IEquatable<TImageId>
    {
        ArgumentNullException.ThrowIfNull(imageIds);
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(images);

        return [.. imageIds.Distinct().Where(imageId => !images.Exists(imageId) || !images.IsEligible(imageId, culture))];
    }
}
