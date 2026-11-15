namespace PandaBear.Repositories;

/// <summary>
/// A repository label, as the <c>labels</c> section of the consumer configuration declares it and as a forge reports it.
/// </summary>
/// <param name="Name">Label name; forges match label names ignoring case.</param>
/// <param name="Color">Six hexadecimal digits without <c>#</c>.</param>
/// <param name="Description">Label description, which may be empty.</param>
public sealed record RepositoryLabel(string Name, string Color, string Description);
