using System.ComponentModel;
using System.Diagnostics;
using PandaBear.Processes;

namespace PandaBear.Versioning.Tests;

/// <summary>Fails every start as the operating system does for a missing executable.</summary>
internal sealed class ThrowingProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken) =>
        throw new Win32Exception(2, "No such file or directory");
}
