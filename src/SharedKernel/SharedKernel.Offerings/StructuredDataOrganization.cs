namespace PandaBear.Offerings;

/// <summary>The organization that provides an offering, as schema.org <c>Organization</c>.</summary>
public sealed record StructuredDataOrganization
{
    /// <summary>Initializes the organization.</summary>
    /// <param name="name">The name.</param>
    /// <param name="url">The optional absolute address of its site.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/>, empty or blank, or <paramref name="url"/> is relative.</exception>
    public StructuredDataOrganization(string name, Uri? url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Url = AbsoluteUrl.Check(url, nameof(url));
    }

    /// <summary>Gets the name.</summary>
    public string Name { get; }

    /// <summary>Gets the absolute address of its site.</summary>
    public Uri? Url { get; }
}
