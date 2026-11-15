using System.Diagnostics;
using PandaBear.Processes;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Answers each process with the output that <paramref name="respond"/> returns for its arguments, records the start
/// information, and resolves a bare command name to <c>/usr/bin/&lt;name&gt;</c>.
/// </summary>
internal sealed class ScriptedProcessRunner(Func<IReadOnlyList<string>, ProcessOutput> respond) : IProcessRunner, IExecutableResolver
{
    /// <summary>Gets the start information of each process, in start order.</summary>
    public List<ProcessStartInfo> Started { get; } = [];

    /// <summary>Gets the writer that each process forwarded its output to, or <see langword="null"/>, in start order.</summary>
    public List<TextWriter?> Forwarded { get; } = [];

    /// <inheritdoc />
    public string? Find(string command) => "/usr/bin/" + command;

    /// <inheritdoc />
    public Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken)
    {
        Started.Add(startInfo);
        Forwarded.Add(forward);
        return Task.FromResult(respond([.. startInfo.ArgumentList]));
    }
}
