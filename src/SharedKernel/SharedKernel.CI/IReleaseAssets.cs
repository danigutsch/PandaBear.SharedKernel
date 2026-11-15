namespace PandaBear.CI;

/// <summary>Locates the public download address of a release asset of one forge repository.</summary>
public interface IReleaseAssets
{
    /// <summary>Returns the HTTPS address that downloads <paramref name="asset"/> of the release tagged <paramref name="tag"/>.</summary>
    /// <param name="tag">The release tag, such as <c>v1.0.0-alpha.1</c>.</param>
    /// <param name="asset">The asset file name, such as <c>candidate.json</c>.</param>
    /// <param name="ct">Cancels a lookup.</param>
    /// <returns>The address, which is downloaded without credentials.</returns>
    /// <exception cref="HttpRequestException">The forge has no such release or asset, or refused a lookup.</exception>
    /// <exception cref="TimeoutException">A lookup took too long.</exception>
    Task<Uri> Locate(string tag, string asset, CancellationToken ct);
}
