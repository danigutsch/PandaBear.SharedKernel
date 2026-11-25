namespace SharedKernel.Offerings.Tests;

/// <summary>An image library for the tests: which images exist, and in which cultures each may be shown.</summary>
internal sealed class ImageLibrary : IImageEligibility<Guid>
{
    private readonly Dictionary<Guid, HashSet<ContentCulture>> _images = [];

    /// <summary>Adds an image that is eligible in the given cultures.</summary>
    /// <param name="cultures">The cultures in which it may be shown.</param>
    /// <returns>The image id.</returns>
    public Guid Add(params ContentCulture[] cultures)
    {
        var id = Guid.CreateVersion7();
        _images[id] = [.. cultures];
        return id;
    }

    /// <inheritdoc />
    public bool Exists(Guid imageId) => _images.ContainsKey(imageId);

    /// <inheritdoc />
    public bool IsEligible(Guid imageId, ContentCulture culture) => _images.TryGetValue(imageId, out var cultures) && cultures.Contains(culture);
}
