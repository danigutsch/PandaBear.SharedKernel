namespace PandaBear.Galleries.Tests;

/// <summary>A photo library for the tests: which photos exist, and in which cultures each may be published.</summary>
internal sealed class PhotoLibrary : IAlbumPhotoEligibility<Guid>
{
    private readonly Dictionary<Guid, HashSet<ContentCulture>> _photos = [];

    /// <summary>Adds a photo that is eligible in the given cultures.</summary>
    /// <param name="cultures">The cultures in which it may be published.</param>
    /// <returns>The photo id.</returns>
    public Guid Add(params ContentCulture[] cultures)
    {
        var id = Guid.CreateVersion7();
        _photos[id] = [.. cultures];
        return id;
    }

    /// <inheritdoc />
    public bool Exists(Guid photoId) => _photos.ContainsKey(photoId);

    /// <inheritdoc />
    public bool IsEligible(Guid photoId, ContentCulture culture) => _photos.TryGetValue(photoId, out var cultures) && cultures.Contains(culture);
}
