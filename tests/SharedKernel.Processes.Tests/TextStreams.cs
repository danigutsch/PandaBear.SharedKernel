namespace SharedKernel.Processes.Tests;

/// <summary>Creates in-memory readers that stand in for a process's output streams.</summary>
internal static class TextStreams
{
    /// <summary>Returns a reader over the UTF-8 bytes of <paramref name="text"/>.</summary>
    public static StreamReader Reader(string text) => new(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)));
}
