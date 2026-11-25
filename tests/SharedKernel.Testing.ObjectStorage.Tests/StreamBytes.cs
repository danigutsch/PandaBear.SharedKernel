namespace SharedKernel.Testing.ObjectStorage.Tests;

/// <summary>Reads streams handed out by the store under test.</summary>
internal static class StreamBytes
{
    /// <summary>Reads a stream from its position to its end.</summary>
    /// <param name="stream">The stream to read.</param>
    /// <returns>The bytes read.</returns>
    public static async Task<byte[]> ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        return buffer.ToArray();
    }
}
