using System.Diagnostics;
using SharedKernel.Processes;

namespace SharedKernel.Repositories.Tests;

/// <summary>Supplies bounded resolver process outcomes in the order they are requested.</summary>
/// <param name="responses">The responses the test expects the resolver to consume.</param>
internal sealed class NuGetCatalogProcessResponses(params ProcessOutput[] responses) : IProcessRunner
{
    private readonly List<ProcessStartInfo> runs = [];

    /// <summary>The commands started before the resolver returned.</summary>
    internal IReadOnlyList<ProcessStartInfo> Runs => runs;

    /// <inheritdoc />
    public Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken)
    {
        var index = runs.Count;
        runs.Add(startInfo);
        return index < responses.Length
            ? Task.FromResult(responses[index])
            : Task.FromException<ProcessOutput>(new TimeoutException("The test resolver exceeded its budget."));
    }
}
