using System.Diagnostics;

namespace PandaBear.Packages.Tests;

/// <summary>An <see cref="IProcessRunner"/> that always throws the given exception.</summary>
internal sealed class ThrowingProcessRunner(Exception exception) : IProcessRunner
{
    /// <summary>Throws the exception.</summary>
    public Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken) => throw exception;
}
