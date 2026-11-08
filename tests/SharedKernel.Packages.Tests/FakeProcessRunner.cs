namespace PandaBear.Packages.Tests;

/// <summary>An <see cref="IProcessRunner"/> that returns one canned output and records the call.</summary>
internal sealed class FakeProcessRunner(ProcessOutput output) : IProcessRunner
{
    /// <summary>Gets the executable of the last call.</summary>
    public string? Executable
    {
        get; private set;
    }

    /// <summary>Gets the arguments of the last call.</summary>
    public IReadOnlyList<string>? Arguments
    {
        get; private set;
    }

    /// <summary>Records the call and returns the canned output.</summary>
    public Task<ProcessOutput> Run(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        Executable = executable;
        Arguments = arguments;
        return Task.FromResult(output);
    }
}
