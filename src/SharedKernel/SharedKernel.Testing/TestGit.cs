using PandaBear.Processes;

namespace PandaBear.Testing;

/// <summary>Runs Git for test setup without the user's global or system configuration, hooks, signing, prompts, or credentials.</summary>
public static class TestGit
{
    private const string Identity = "Test Author";
    private const string IdentityEmail = "test-author@example.invalid";

    /// <summary>
    /// Gets the environment variables that cut Git off from the user's global and system configuration and turn off
    /// automatic maintenance (<c>maintenance.auto=false</c> and <c>gc.auto=0</c>, set through <c>GIT_CONFIG_COUNT</c>,
    /// which overrides the repository's configuration). Otherwise a command such as <c>commit</c> leaves a detached
    /// <c>git maintenance</c> running that keeps writing to <c>.git</c> after the command returned, while the test reads or
    /// deletes the repository. A caller that sets <c>GIT_CONFIG_COUNT</c> itself replaces these two settings.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> Environment { get; } = new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["GIT_ATTR_NOSYSTEM"] = "1",
        ["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null",
        ["GIT_CONFIG_NOSYSTEM"] = "1",
        ["GIT_CONFIG_COUNT"] = "2",
        ["GIT_CONFIG_KEY_0"] = "maintenance.auto",
        ["GIT_CONFIG_VALUE_0"] = "false",
        ["GIT_CONFIG_KEY_1"] = "gc.auto",
        ["GIT_CONFIG_VALUE_1"] = "0",
    };

    /// <summary>
    /// Runs <c>git</c> with <paramref name="arguments"/> in <paramref name="directory"/> and returns the result, whatever the
    /// exit code. Git starts as <see cref="GitStartInfo"/> describes it, from an absolute <c>PATH</c> directory, without a
    /// prompt or the host's tokens and <c>GIT_*</c> variables, then gets <see cref="Environment"/> and
    /// <paramref name="environment"/>, and runs through a <see cref="BoundedProcessRunner"/> for at most
    /// <see cref="GitStartInfo.Timeout"/>.
    /// </summary>
    /// <param name="directory">The working directory.</param>
    /// <param name="arguments">The Git arguments.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <param name="environment">Environment variables that replace or, with a <see langword="null" /> value, remove entries of <see cref="Environment" />.</param>
    /// <returns>The exit code and captured output.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directory"/> or <paramref name="arguments"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Git is not in an absolute <c>PATH</c> directory, cannot start, or writes more output than the limit.</exception>
    /// <exception cref="TimeoutException">Git does not finish within <see cref="GitStartInfo.Timeout"/>; it has been stopped.</exception>
    public static async Task<TestProcessResult> TryRun(
        string directory,
        IReadOnlyList<string> arguments,
        CancellationToken ct,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = GitStartInfo.Create(directory, arguments);
        foreach (var variable in environment is null ? Environment : Environment.Concat(environment))
        {
            if (variable.Value is null)
            {
                startInfo.Environment.Remove(variable.Key);
            }
            else
            {
                startInfo.Environment[variable.Key] = variable.Value;
            }
        }

        var output = await new BoundedProcessRunner(GitStartInfo.Timeout).Run(startInfo, forward: null, ct).ConfigureAwait(false);
        return new TestProcessResult(output.ExitCode, output.StandardOutput, output.StandardError);
    }

    /// <summary>Runs <c>git</c> with <paramref name="arguments"/> in <paramref name="directory"/> and returns its standard output.</summary>
    /// <param name="directory">The working directory.</param>
    /// <param name="arguments">The Git arguments.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The standard output of Git.</returns>
    /// <exception cref="InvalidOperationException">Git exited with a non-zero code; the message carries its standard error.</exception>
    public static async Task<string> Run(string directory, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var result = await TryRun(directory, arguments, ct).ConfigureAwait(false);
        return result.ExitCode == 0
            ? result.StandardOutput
            : throw new InvalidOperationException("git " + string.Join(' ', arguments) + " failed: " + result.StandardError);
    }

    /// <summary>Creates a repository in <paramref name="directory"/> that commits every file in it, and returns the commit.</summary>
    /// <param name="directory">The directory to turn into a repository.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The commit id.</returns>
    public static async Task<string> CommitAll(string directory, CancellationToken ct)
    {
        await Run(directory, ["init", "--quiet"], ct).ConfigureAwait(false);
        return await Commit(directory, "test: commit", ct).ConfigureAwait(false);
    }

    /// <summary>Commits every file in the repository at <paramref name="directory"/> and returns the commit.</summary>
    /// <param name="directory">The repository directory.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The commit id.</returns>
    public static Task<string> Commit(string directory, CancellationToken ct) => Commit(directory, "test: change", ct);

    private static async Task<string> Commit(string directory, string message, CancellationToken ct)
    {
        await Run(directory, ["add", "--all"], ct).ConfigureAwait(false);
        await Run(
            directory,
            ["-c", $"user.name={Identity}", "-c", $"user.email={IdentityEmail}", "-c", "commit.gpgsign=false", "commit", "--quiet", "--message", message],
            ct).ConfigureAwait(false);
        return (await Run(directory, ["rev-parse", "HEAD"], ct).ConfigureAwait(false)).Trim();
    }
}
