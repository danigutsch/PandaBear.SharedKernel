namespace SharedKernel.PageSections;

/// <summary>An unvalidated video of a section: a poster image that links to the video, which is never embedded.</summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <param name="Address">The https address of the video; required.</param>
/// <param name="PosterImageId">The poster image; required, and it must exist.</param>
/// <param name="Title">The title, which names the link; required.</param>
public sealed record PageVideoDefinition<TImageId>(string? Address, TImageId? PosterImageId, string? Title)
    where TImageId : struct, IEquatable<TImageId>;
