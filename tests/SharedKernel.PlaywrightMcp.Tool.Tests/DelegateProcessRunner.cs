using System.Diagnostics;

namespace PandaBear.PlaywrightMcp.Tool.Tests;

/// <summary>A process runner that hands every run to a delegate instead of starting a process.</summary>
/// <param name="run">Answers each run.</param>
internal sealed class DelegateProcessRunner(Func<ProcessStartInfo, TextWriter?, CancellationToken, Task<ProcessOutput>> run) : IProcessRunner
{
    /// <inheritdoc />
    public Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken) =>
        run(startInfo, forward, cancellationToken);
}
