namespace Aspire.Hosting;

/// <summary>Pinned container image coordinates for the SeaweedFS resource.</summary>
internal static class SeaweedFsContainerImageTags
{
    /// <remarks>docker.io</remarks>
    internal const string Registry = "docker.io";

    /// <remarks>chrislusf/seaweedfs</remarks>
    internal const string Image = "chrislusf/seaweedfs";

    /// <remarks>4.39</remarks>
    internal const string Tag = "4.39";

    /// <summary>The digest of the pinned image.</summary>
    internal const string Digest = "c7d6c721b30ae711db766bbbfd40192776e263d4e51e22f57baef7bef93c12c6";
}
