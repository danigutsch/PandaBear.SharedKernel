using System.Globalization;

namespace PandaBear.Testing;

/// <summary>
/// A <see cref="StringWriter"/> that captures text the same way on every operating system: it formats with
/// <see cref="CultureInfo.InvariantCulture"/> and ends each line with <c>"\n"</c> instead of <see cref="Environment.NewLine"/>.
/// </summary>
/// <remarks>
/// Pass it as the output or error writer of a command-line tool, a console or any other <see cref="TextWriter"/> consumer
/// whose text a test compares. A plain <see cref="StringWriter"/> ends lines with <c>"\r\n"</c> on Windows, so an
/// expectation written with <c>"\n"</c> passes on Linux and macOS but fails on Windows. Set <see cref="TextWriter.NewLine"/>
/// explicitly when a test needs another line ending.
/// </remarks>
public sealed class InvariantStringWriter : StringWriter
{
    /// <summary>Creates an empty writer that formats with the invariant culture and ends lines with <c>"\n"</c>.</summary>
    public InvariantStringWriter()
        : base(CultureInfo.InvariantCulture)
    {
        NewLine = "\n";
    }
}
