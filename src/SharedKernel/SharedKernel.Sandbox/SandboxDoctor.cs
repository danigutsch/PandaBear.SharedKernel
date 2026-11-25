using System.Globalization;
using SharedKernel.InputNormalization;

namespace SharedKernel.Sandbox;

/// <summary>
/// Runs the probes and says, for each, whether the environment allows the operation and what that means for .NET tools:
/// Unix sockets and named pipes, writing the temporary and NuGet package folders, and reading each configured denied path.
/// A probe tries an operation and undoes it; none changes anything that lasts.
/// </summary>
public sealed class SandboxDoctor
{
    private const string SocketAdvice =
        "Project-mode 'dotnet format' and 'dotnet test' cannot start, because they connect over a Unix socket. "
        + "Use 'dotnet format whitespace --folder <dir>', build with code-style enforcement, and run the built test executable directly. "
        + "Allowing every Unix socket only for this opens the container engine and the ssh and gpg agents to every command.";

    private readonly ISandboxEnvironment _environment;

    /// <summary>Creates the doctor for <paramref name="environment"/>.</summary>
    /// <param name="environment">The operations the probes try, such as <see cref="SystemSandboxEnvironment"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is <see langword="null"/>.</exception>
    public SandboxDoctor(ISandboxEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    /// <summary>Runs the probes, writes one line per probe and a summary to <paramref name="output"/>, and returns the exit code.</summary>
    /// <param name="configuration">The configuration whose denied read paths are checked, or <see langword="null"/> when there is none.</param>
    /// <param name="output">Receives the report; control characters in configured paths are escaped.</param>
    /// <param name="cancellationToken">Cancels writing the report.</param>
    /// <returns>0 when no required probe has a bad outcome, and 1 otherwise.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public async Task<int> Run(SandboxConfiguration? configuration, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        var results = Probe(configuration);
        foreach (var result in results)
        {
            await output.WriteLineAsync($"{result.Word} {ControlCharacters.Escape(result.Id)}: {result.Detail}".AsMemory(), cancellationToken).ConfigureAwait(false);
            if (!result.IsGood && !result.IsSkipped && result.Advice is not null)
            {
                await output.WriteLineAsync($"  -> {result.Advice}".AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }

        if (configuration is null)
        {
            await output.WriteLineAsync($"skipped denied reads: {SandboxConfigurationFile.RelativePath} does not exist, so no path is checked.".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        var failed = results.Count(static r => r.Fails);
        await output.WriteLineAsync(
            string.Create(CultureInfo.InvariantCulture, $"{results.Count} probes: {results.Count(static r => r.IsGood)} good, {results.Count(static r => !r.IsGood && !r.IsSkipped)} not good, {results.Count(static r => r.IsSkipped)} skipped.").AsMemory(),
            cancellationToken).ConfigureAwait(false);
        return failed == 0 ? 0 : 1;
    }

    /// <summary>Runs the probes in a fixed order: the capabilities first, then one protection probe per configured denied read path.</summary>
    /// <param name="configuration">The configuration whose denied read paths are checked, or <see langword="null"/> when there is none.</param>
    /// <returns>The result of every probe.</returns>
    public IReadOnlyList<ProbeResult> Probe(SandboxConfiguration? configuration)
    {
        var environment = _environment;
        List<ProbeResult> results = [];

        var socket = environment.CreateUnixSocket();
        results.Add(new ProbeResult("unix-socket", ProbeKind.Capability, socket.Outcome, Required: false, socket.Detail, SocketAdvice));

        var pipe = environment.CreateNamedPipe();
        results.Add(new ProbeResult("named-pipe", ProbeKind.Capability, pipe.Outcome, Required: false, pipe.Detail, SocketAdvice));

        // A temporary folder that does not exist or cannot be seen is as bad as one that cannot be written.
        var temp = environment.WriteFile(environment.TempDirectory);
        var tempOutcome = temp.Outcome == ProbeOutcome.Skipped ? (ProbeOutcome.Failed, "The temporary folder does not exist or cannot be seen.") : temp;
        results.Add(new ProbeResult("temp-write", ProbeKind.Capability, tempOutcome.Item1, Required: true, tempOutcome.Item2, "Builds and tests need a writable temporary folder; allow write access to it."));

        var nuget = environment.NuGetPackagesDirectory is { } folder
            ? environment.WriteFile(folder)
            : (ProbeOutcome.Skipped, "The NuGet package folder cannot be determined.");
        results.Add(new ProbeResult("nuget-write", ProbeKind.Capability, nuget.Item1, Required: true, nuget.Item2, "Restore cannot add packages; allow write access to the NuGet package folder."));

        foreach (var configured in configuration?.DeniedReadPaths ?? [])
        {
            if (configured.StartsWith("~/", StringComparison.Ordinal) && environment.HomeDirectory.Length == 0)
            {
                results.Add(new ProbeResult($"denied-read {configured}", ProbeKind.Protection, ProbeOutcome.Skipped, Required: true, "The home folder is unknown, so '~/' cannot be expanded.", null));
                continue;
            }

            var path = configured.StartsWith("~/", StringComparison.Ordinal)
                ? Path.Combine(environment.HomeDirectory, configured[2..])
                : configured;
            var read = environment.ReadPath(path);
            results.Add(new ProbeResult($"denied-read {configured}", ProbeKind.Protection, read.Outcome, Required: true, read.Detail, "This path is readable from the sandbox; add it to the denied reads."));
        }

        return results;
    }
}
