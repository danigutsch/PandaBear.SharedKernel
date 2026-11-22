namespace PandaBear.Offerings.Tests;

/// <summary>An empty image library.</summary>
internal sealed class NoImages : IImageEligibility<Guid>
{
    /// <inheritdoc />
    public bool Exists(Guid imageId) => false;

    /// <inheritdoc />
    public bool IsEligible(Guid imageId, ContentCulture culture) => false;
}
