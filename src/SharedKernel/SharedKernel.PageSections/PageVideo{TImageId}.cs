using PandaBear.BuildingBlocks;

namespace PandaBear.PageSections;

/// <summary>A video of a section: a poster image that links to the video's https address; the video is never embedded.</summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
public sealed record PageVideo<TImageId>
    where TImageId : struct, IEquatable<TImageId>
{
    /// <summary>Initializes a video.</summary>
    /// <param name="address">The address.</param>
    /// <param name="posterImageId">The poster.</param>
    /// <param name="title">The title.</param>
    internal PageVideo(WebUrl address, TImageId posterImageId, string title)
    {
        Address = address;
        PosterImageId = posterImageId;
        Title = title;
    }

    /// <summary>Gets the https address of the video.</summary>
    public WebUrl Address { get; }

    /// <summary>Gets the poster image.</summary>
    public TImageId PosterImageId { get; }

    /// <summary>Gets the title, which names the link.</summary>
    public string Title { get; }
}
