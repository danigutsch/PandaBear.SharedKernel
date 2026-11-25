namespace SharedKernel.PageSections;

/// <summary>An unvalidated person line of a section: a round photo, a name and a role.</summary>
/// <typeparam name="TImageId">The image id type.</typeparam>
/// <param name="ImageId">The optional photo, which must exist.</param>
/// <param name="Name">The name; required.</param>
/// <param name="Role">The optional role.</param>
public sealed record PagePersonDefinition<TImageId>(TImageId? ImageId, string? Name, string? Role)
    where TImageId : struct, IEquatable<TImageId>;
