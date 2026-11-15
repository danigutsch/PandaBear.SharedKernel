using System.Diagnostics;
using PandaBear.Processes;

namespace PandaBear.Repository.Tests;

/// <summary>Answers each run with the standard output that a function returns for its start information, and exit code 0.</summary>
/// <param name="respond">Returns the standard output, or throws as a failed process would.</param>
internal sealed class DelegateProcessRunner(Func<ProcessStartInfo, CancellationToken, Task<string>> respond) : IProcessRunner
{
    /// <summary>Answers with the output of a function of the file name, the arguments, the working directory, and the token.</summary>
    public static DelegateProcessRunner From(Func<string, string[], string, CancellationToken, Task<string>> run) =>
        new((startInfo, ct) => run(startInfo.FileName, [.. startInfo.ArgumentList], startInfo.WorkingDirectory, ct));

    /// <inheritdoc />
    public async Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken) =>
        new(0, await respond(startInfo, cancellationToken));
}
