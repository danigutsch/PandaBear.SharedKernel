using System.Diagnostics;
using PandaBear.Processes;

namespace PandaBear.Versioning.Tests;

/// <summary>Captures the verified package inputs and supplies ApiCompat process results.</summary>
internal sealed class ComparisonApiCompatRunner(ProcessOutput packageResult) : IProcessRunner
{
    /// <summary>Gets the package paths and exact bytes handed to the comparison.</summary>
    internal List<(string Candidate, string Baseline, string CandidateHash, string BaselineHash)> Comparisons { get; } = [];

    /// <inheritdoc />
    public async Task<ProcessOutput> Run(ProcessStartInfo startInfo, TextWriter? forward, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (startInfo.ArgumentList is ["--version"])
        {
            return new ProcessOutput(0, "11.0.100+test\n");
        }

        if (startInfo.ArgumentList is not ["package", var candidate, "--baseline-package", var baseline])
        {
            throw new InvalidOperationException("Unexpected ApiCompat command.");
        }

        Comparisons.Add((
            candidate, baseline,
            await CandidateManifest.HashPackage(candidate, cancellationToken),
            await CandidateManifest.HashPackage(baseline, cancellationToken)));
        return packageResult;
    }
}
