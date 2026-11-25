namespace SharedKernel.Aspire.Hosting;

/// <summary>
/// A pinned Redis image, for <c>AddRedis(...).WithImageTag(Tag).WithImageSHA256(Digest)</c>. Only constants: referencing them does not bring
/// <c>Aspire.Hosting.Redis</c> into an AppHost that does not use Redis.
/// </summary>
public static class RedisContainerImageTags
{
    /// <summary>The registry, <c>docker.io</c>, the same as the Aspire Redis integration's default.</summary>
    public const string Registry = "docker.io";

    /// <summary>The image, <c>library/redis</c>, the same as the Aspire Redis integration's default.</summary>
    public const string Image = "library/redis";

    /// <summary>The tag, <c>8.8</c>.</summary>
    public const string Tag = "8.8";

    /// <summary>The SHA-256 digest of <c>docker.io/library/redis:8.8</c>, without the <c>sha256:</c> prefix.</summary>
    public const string Digest = "2838d5524559494f6f1cd66e97e76b200d64a633a8614200620755ed395daf32";
}
