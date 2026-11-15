using System.Diagnostics;

namespace PandaBear.PlaywrightMcp.Tests;

/// <summary>A process runner that answers each run in order from a script and records what was started.</summary>
/// <param name="answers">The output of each run, in order.</param>
internal sealed class ScriptedProcessRunner(params ProcessOutput[] answers) : IProcessRunner
{
    private readonly List<(ProcessStartInfo StartInfo, TextWriter? Forward)> _runs = [];

    /// <summary>Gets each started process and the writer its output went to, in order.</summary>
    public IReadOnlyList<(ProcessStartInfo StartInfo, TextWriter? Forward)> Runs => _runs;

    /// <inheritdoc />
    public Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken)
    {
        _runs.Add((startInfo, forward));
        return Task.FromResult(answers[_runs.Count - 1]);
    }
}
