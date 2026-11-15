namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Handles the bytes of repository files that <c>PandaBear.IO.BoundedReader.ReadFile</c> read.
/// </summary>
internal static class RepositoryFile
{
    private static ReadOnlySpan<byte> Utf8ByteOrderMark => [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Returns <paramref name="content"/> without a leading UTF-8 byte order mark.
    /// </summary>
    public static ReadOnlyMemory<byte> WithoutUtf8ByteOrderMark(byte[] content) =>
        content.AsSpan().StartsWith(Utf8ByteOrderMark) ? content.AsMemory(Utf8ByteOrderMark.Length) : content;
}
