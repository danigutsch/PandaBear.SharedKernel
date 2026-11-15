using PandaBear.Processes;

namespace PandaBear.Versioning;

/// <summary>Reads release versions and breaking-change markers from a repository's Git history.</summary>
/// <param name="processRunner">Runs <c>git</c>, such as <see cref="SystemProcessRunner"/>.</param>
/// <param name="executableResolver">Finds <c>git</c>, such as <see cref="PathExecutableResolver.Default"/>.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="processRunner"/> or <paramref name="executableResolver"/> is <see langword="null"/>.</exception>
public sealed class ReleaseHistory(IProcessRunner processRunner, IExecutableResolver executableResolver)
{
    private readonly ExternalCommands _commands = new(processRunner, executableResolver);

    /// <summary>
    /// Calculates the next version from the commit messages since the latest <c>v*</c> tag, or of the whole history when
    /// no tag exists.
    /// </summary>
    /// <param name="options">The repository, version kind, run number, and source revision.</param>
    /// <param name="ct">Cancels the run and ends the running <c>git</c> process.</param>
    /// <returns>The calculated version, its version JSON, and the tag it was calculated from.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The version kind is <c>prerelease</c> and the run number is empty or white-space.</exception>
    /// <exception cref="InvalidOperationException"><c>git</c> could not start or failed.</exception>
    public async Task<ReleaseVersionCalculationResult> CalculateVersion(ReleaseVersionOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.VersionKind == "prerelease")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(options.RunNumber);
        }

        var sourceSha = string.IsNullOrWhiteSpace(options.Sha)
            ? (await _commands.Run("git", ["rev-parse", "HEAD"], options.RepoRoot, ct).ConfigureAwait(false)).Trim()
            : options.Sha;
        var sourceTag = (await _commands.RunOrDefault(
            "git",
            ["describe", "--tags", "--match", "v[0-9]*", "--abbrev=0"],
            options.RepoRoot,
            ct).ConfigureAwait(false)).Trim();

        var commitRange = ReleaseVersionCalculator.GetCommitRange(sourceTag);
        var logOutput = await _commands.Run("git", ["log", "--format=%B%x00", commitRange], options.RepoRoot, ct).ConfigureAwait(false);
        return await ReleaseVersionCalculator.Calculate(
            new ReleaseVersionCalculationInput(
                sourceSha,
                sourceTag,
                logOutput,
                options.VersionKind,
                options.RunNumber)).ConfigureAwait(false);
    }

    /// <summary>Returns whether a commit in <paramref name="range"/> has a Conventional Commit breaking-change marker.</summary>
    /// <param name="range">The Git revision range, such as <c>origin/main..HEAD</c>; Git never reads it as an option.</param>
    /// <param name="repoRoot">The Git repository root.</param>
    /// <param name="ct">Cancels the run and ends the running <c>git</c> process.</param>
    /// <returns><see langword="true"/> when a commit in the range has a marker.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="range"/> or <paramref name="repoRoot"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><c>git</c> could not start or failed, for example for an unknown revision.</exception>
    public async Task<bool> HasBreakingChangeMarker(string range, string repoRoot, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(repoRoot);
        var log = await _commands.Run("git", ["log", "--format=%B%x00", "--end-of-options", range], repoRoot, ct).ConfigureAwait(false);
        return BreakingChangeMarker.HasMarker(log);
    }
}
