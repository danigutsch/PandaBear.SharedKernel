using PandaBear.Tooling;

namespace PandaBear.Secrets.Tool;

/// <summary>Writes lines with control characters escaped, so file names and tool messages cannot steer a terminal or forge a log line.</summary>
internal static class EscapedOutput
{
    /// <summary>Writes <paramref name="text"/> with control characters escaped, then a line end.</summary>
    public static Task Line(TextWriter writer, string text, CancellationToken cancellationToken) =>
        writer.WriteLineAsync(ControlCharacters.Escape(text).AsMemory(), cancellationToken);
}
