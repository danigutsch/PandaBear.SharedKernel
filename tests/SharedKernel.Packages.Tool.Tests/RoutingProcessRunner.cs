namespace PandaBear.Packages.Tool.Tests;

/// <summary>An <see cref="IProcessRunner"/> that answers by executable path and records every call.</summary>
internal sealed class RoutingProcessRunner(Dictionary<string, ProcessOutput> answers) : IProcessRunner
{
    private readonly List<string> _calls = [];

    /// <summary>Gets the executables that were run, in order.</summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <summary>Records the call and returns the answer for the executable.</summary>
    public Task<ProcessOutput> Run(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        _calls.Add(executable);
        return Task.FromResult(answers[executable]);
    }
}
