namespace PandaBear.AspNetCore;

/// <summary>Where a moved path now lives, as answered by the resolver of <see cref="PermanentRedirectExtensions.UsePermanentRedirects"/>.</summary>
public sealed record PermanentRedirect
{
    /// <summary>Initializes a new instance of the <see cref="PermanentRedirect"/> class.</summary>
    /// <param name="Path">The local target path, such as <c>/en/tours/alps</c>; see <see cref="PermanentRedirectExtensions.UsePermanentRedirects"/> for what is accepted.</param>
    /// <param name="PreserveQuery">Whether the request's query string is appended to the target.</param>
    /// <exception cref="ArgumentNullException"><paramref name="Path"/> is <see langword="null"/>.</exception>
    public PermanentRedirect(string Path, bool PreserveQuery = false)
    {
        ArgumentNullException.ThrowIfNull(Path);
        this.Path = Path;
        this.PreserveQuery = PreserveQuery;
    }

    /// <summary>Gets the local target path.</summary>
    public string Path { get; }

    /// <summary>Gets a value indicating whether the request's query string is appended to the target.</summary>
    public bool PreserveQuery { get; }
}
