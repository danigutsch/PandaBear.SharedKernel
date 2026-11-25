namespace SharedKernel.Aspire.Hosting;

/// <summary>
/// A pinned PostgreSQL server image, for <c>AddPostgres(...).WithImageTag(Tag).WithImageSHA256(Digest)</c>. Only constants: referencing them
/// does not bring <c>Aspire.Hosting.PostgreSQL</c> into an AppHost that does not use PostgreSQL.
/// </summary>
public static class PostgresContainerImageTags
{
    /// <summary>The registry, <c>docker.io</c>, the same as the Aspire PostgreSQL integration's default.</summary>
    public const string Registry = "docker.io";

    /// <summary>The image, <c>library/postgres</c>, the same as the Aspire PostgreSQL integration's default.</summary>
    public const string Image = "library/postgres";

    /// <summary>The tag, <c>18.4</c>.</summary>
    public const string Tag = "18.4";

    /// <summary>The SHA-256 digest of <c>docker.io/library/postgres:18.4</c>, without the <c>sha256:</c> prefix.</summary>
    public const string Digest = "4aabea78cf39b90e834caf3af7d602a18565f6fe2508705c8d01aa63245c2e20";
}
