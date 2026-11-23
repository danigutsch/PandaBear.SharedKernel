namespace PandaBear.PageSections;

/// <summary>The person line of a section: an optional round photo, a name and an optional role.</summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
public sealed record PagePerson<TImageId>
    where TImageId : struct, IEquatable<TImageId>
{
    /// <summary>Initializes a person line.</summary>
    /// <param name="imageId">The photo.</param>
    /// <param name="name">The name.</param>
    /// <param name="role">The role.</param>
    internal PagePerson(TImageId? imageId, string name, string? role)
    {
        ImageId = imageId;
        Name = name;
        Role = role;
    }

    /// <summary>Gets the optional photo.</summary>
    public TImageId? ImageId { get; }

    /// <summary>Gets the name.</summary>
    public string Name { get; }

    /// <summary>Gets the optional role.</summary>
    public string? Role { get; }
}
