namespace Aspire.Hosting;

/// <summary>Pinned container image coordinates for the Umami resource.</summary>
internal static class UmamiContainerImageTags
{
    /// <remarks>docker.umami.is</remarks>
    internal const string Registry = "docker.umami.is";

    /// <remarks>umami-software/umami</remarks>
    internal const string Image = "umami-software/umami";

    /// <remarks>3.4.1</remarks>
    internal const string Tag = "3.4.1";

    /// <summary>The digest of the multi-platform index of the pinned image (linux/amd64 and linux/arm64), without the <c>sha256:</c> prefix.</summary>
    internal const string Digest = "e69d1a7932e9dab0fc523edc7be1f3a53f3c261f57856fc2167ae74a8fc022e8";
}
