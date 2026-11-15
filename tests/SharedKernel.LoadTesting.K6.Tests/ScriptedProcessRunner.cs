using System.Diagnostics;

namespace PandaBear.LoadTesting.K6.Tests;

/// <summary>A process runner that answers each run in order from a script and records what was started.</summary>
/// <param name="answers">Produces the result of each run, in order, from the start information and the token.</param>
internal sealed class ScriptedProcessRunner(params Func<ProcessStartInfo, CancellationToken, ProcessOutput>[] answers) : IProcessRunner
{
    private readonly List<ProcessStartInfo> _runs = [];

    /// <summary>Gets each started process, in order.</summary>
    public IReadOnlyList<ProcessStartInfo> Runs => _runs;

    /// <summary>Creates an answer that returns <paramref name="exitCode"/>.</summary>
    public static Func<ProcessStartInfo, CancellationToken, ProcessOutput> Exit(int exitCode) =>
        (_, _) => new ProcessOutput(exitCode, string.Empty);

    /// <summary>Creates an answer that throws <paramref name="exception"/>.</summary>
    public static Func<ProcessStartInfo, CancellationToken, ProcessOutput> Throw(Exception exception) =>
        (_, _) => throw exception;

    /// <inheritdoc />
    public Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken)
    {
        _runs.Add(startInfo);
        return Task.FromResult(answers[_runs.Count - 1](startInfo, cancellationToken));
    }
}
