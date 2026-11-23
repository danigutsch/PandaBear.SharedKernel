namespace PandaBear.Offerings;

/// <summary>Checks the addresses structured data carries.</summary>
internal static class AbsoluteUrl
{
    /// <summary>Checks that an address is absolute.</summary>
    /// <param name="url">The address.</param>
    /// <param name="name">The parameter name.</param>
    /// <returns>The address.</returns>
    /// <exception cref="ArgumentException">The address is relative.</exception>
    public static Uri? Check(Uri? url, string name) =>
        url is null || url.IsAbsoluteUri ? url : throw new ArgumentException("The address must be absolute.", name);
}
