using System.Text.RegularExpressions;

namespace PandaBear.LoadTesting;

/// <summary>The container engine and digest-pinned image that a container run starts the load generator in.</summary>
public sealed partial class LoadTestContainer
{
    /// <summary>The host name through which a container reaches the machine that runs it.</summary>
    public const string HostAlias = "host.docker.internal";

    /// <summary>Creates the container description.</summary>
    /// <param name="engine">The engine: <c>docker</c> or <c>podman</c>.</param>
    /// <param name="image">The image reference, pinned by a SHA-256 digest.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="engine"/> is neither <c>docker</c> nor <c>podman</c>, or <paramref name="image"/> is not pinned by digest.</exception>
    public LoadTestContainer(string engine, string image)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(image);
        if (engine is not ("docker" or "podman"))
        {
            throw new ArgumentException("The engine must be docker or podman.", nameof(engine));
        }

        if (!IsDigestPinned(image))
        {
            throw new ArgumentException("The image must be pinned by digest.", nameof(image));
        }

        Engine = engine;
        Image = image;
    }

    /// <summary>Gets the engine: <c>docker</c> or <c>podman</c>.</summary>
    public string Engine { get; }

    /// <summary>Gets the image reference, pinned by a SHA-256 digest.</summary>
    public string Image { get; }

    /// <summary>Reports whether <paramref name="image"/> is an image reference pinned by a SHA-256 digest.</summary>
    /// <param name="image">The image reference.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    public static bool IsDigestPinned(string image)
    {
        ArgumentNullException.ThrowIfNull(image);
        return DigestPinnedImage().IsMatch(image);
    }

    // The reference starts with a lowercase letter or digit, so it can never read as an engine option.
    [GeneratedRegex(@"\A[a-z0-9][^\s@]*@sha256:[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex DigestPinnedImage();
}
