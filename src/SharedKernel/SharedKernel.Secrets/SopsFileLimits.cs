namespace PandaBear.Secrets;

/// <summary>The limits on the encrypted SOPS files that the engine reads.</summary>
internal static class SopsFileLimits
{
    /// <summary>The largest encrypted file read: 16 MiB.</summary>
    public const int MaxBytes = 16 * 1024 * 1024;
}
