using SharedKernel.Processes;

namespace SharedKernel.Versioning;

/// <summary>
/// Packs the SharedKernel repository's projects with <c>dotnet</c>, checks their API compatibility, and proves that the
/// packages restore, install, build, and run for a consumer. Every operation writes its result line to the given output.
/// </summary>
/// <param name="processRunner">Runs <c>dotnet</c> and the packed tools and samples, such as <see cref="SystemProcessRunner"/>.</param>
/// <param name="executableResolver">Finds <c>dotnet</c>, such as <see cref="PathExecutableResolver.Default"/>.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="processRunner"/> or <paramref name="executableResolver"/> is <see langword="null"/>.</exception>
public sealed class Packager(IProcessRunner processRunner, IExecutableResolver executableResolver)
{
    private readonly ExternalCommands _commands = new(processRunner, executableResolver);

    /// <summary>
    /// Packs every SharedKernel project into a folder named after the version under the output root, optionally restores
    /// the packages and installs the tool packages from that folder alone, and returns the folder.
    /// </summary>
    /// <param name="options">The versions, output root, and repository root.</param>
    /// <param name="verifyRestore">Whether to restore the packages from the folder to prove that it is a consumable feed.</param>
    /// <param name="output">Receives the package folder line.</param>
    /// <param name="ct">Cancels the pack and ends the running process tree.</param>
    /// <returns>The package folder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The version already exists in the folder, no SharedKernel project exists, or a package did not restore.</exception>
    /// <exception cref="InvalidOperationException"><c>dotnet</c> could not start or failed.</exception>
    public Task<string> Pack(PackOptions options, bool verifyRestore, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        return ReleasePack.Run(_commands, options, verifyRestore, baselineVersion: null, output, ct);
    }

    /// <summary>
    /// Checks the public API baselines, packs SharedKernel with package validation against the optional baseline version
    /// into <c>packages</c> under the output root, and writes the API compatibility report. A pack failure appears in the
    /// report and fails the check unless the release phase treats it as report-only.
    /// </summary>
    /// <param name="options">The version, release phase, baseline, and directories.</param>
    /// <param name="output">Receives the report path line.</param>
    /// <param name="ct">Cancels the check and ends the running process tree.</param>
    /// <returns>A task that completes when the report is written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A public API baseline is missing, or the pack failed and the release phase does not allow it.</exception>
    /// <exception cref="InvalidOperationException"><c>dotnet</c> failed and the release phase does not allow it.</exception>
    public Task CheckApiCompatibility(ApiCompatibilityReportOptions options, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        return ApiCompatibilityCheck.Run(_commands, options, output, ct);
    }

    /// <summary>
    /// Packs the named projects from the working tree into the feed, and the dependencies that the feed lacks, then evicts
    /// each packed version from the global packages folder, so a consumer restores the new package.
    /// </summary>
    /// <param name="options">The projects, feed, version, and consumer cache settings.</param>
    /// <param name="output">Receives one line per project and the feed line.</param>
    /// <param name="ct">Cancels the pack and ends the running process tree.</param>
    /// <returns>The absolute feed directory.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A named project does not exist.</exception>
    /// <exception cref="InvalidOperationException"><c>dotnet pack</c> could not start or failed.</exception>
    public Task<string> PackDev(DevPackOptions options, TextWriter output, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        return DevPack.Run(_commands, options, output, ct);
    }

    /// <summary>
    /// Installs the tool packages of the feed and runs each of their commands with <c>--help</c>, then restores, builds,
    /// and runs the repository's samples against the packages, optionally also as NativeAOT executables.
    /// </summary>
    /// <param name="options">The repository root, feed, version, and NativeAOT choice.</param>
    /// <param name="output">Receives the result line.</param>
    /// <param name="error">Receives the output of the child processes.</param>
    /// <param name="ct">Cancels the check and ends the running process tree.</param>
    /// <returns>A task that completes when every check passed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The feed contains no packages, or a tool package declares an invalid command.</exception>
    /// <exception cref="InvalidOperationException">A tool did not install, or a command, restore, build, or sample failed.</exception>
    public Task ConsumePackages(PackageConsumptionOptions options, TextWriter output, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        return PackageConsumption.Run(_commands, options, output, error, ct);
    }
}
