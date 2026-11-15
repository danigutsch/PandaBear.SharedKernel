using System.Diagnostics;

namespace PandaBear.Packages.Tests;

/// <summary>An <see cref="IProcessRunner"/> that returns one canned output and records the call.</summary>
internal sealed class FakeProcessRunner(ProcessOutput output) : IProcessRunner
{
    /// <summary>Gets the executable of the last call.</summary>
    public string? Executable { get; private set; }

    /// <summary>Gets the arguments of the last call.</summary>
    public IReadOnlyList<string>? Arguments { get; private set; }

    /// <summary>Records the call and returns the canned output.</summary>
    public Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken)
    {
        Executable = startInfo.FileName;
        Arguments = [.. startInfo.ArgumentList];
        return Task.FromResult(output);
    }
}
