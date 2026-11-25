using System.ComponentModel;
using System.Text;

namespace SharedKernel.Repositories;

/// <summary>
/// Reads and writes repository-local Git settings through <c>git config --local</c>, run by <see cref="GitProcess"/>:
/// an absolute path without a shell, no prompt, no credentials, bounded output, and a time limit. The repository's
/// <c>.git/config</c> is never edited as text.
/// </summary>
internal static class BaselineGit
{
    private const int MaxOutputBytes = 1024 * 1024;

    /// <summary>Compares each setting with the repository's value.</summary>
    /// <exception cref="InvalidOperationException">The root is not the top level of a Git repository, or Git fails.</exception>
    /// <exception cref="TimeoutException">A Git call does not finish within 30 seconds; it has been stopped.</exception>
    public static Task<IReadOnlyList<BaselineGitEntry>> Plan(
        string root,
        IReadOnlyList<BaselineGitConfigurationEntry> settings,
        CancellationToken cancellationToken) =>
        Plan(GitProcess.Default, root, settings, cancellationToken);

    /// <summary>Compares each setting with the repository's value, running Git through <paramref name="git"/>.</summary>
    /// <exception cref="InvalidOperationException">The root is not the top level of a Git repository, or Git fails.</exception>
    /// <exception cref="TimeoutException">A Git call does not finish within the time limit of <paramref name="git"/>; it has been stopped.</exception>
    public static async Task<IReadOnlyList<BaselineGitEntry>> Plan(
        GitProcess git,
        string root,
        IReadOnlyList<BaselineGitConfigurationEntry> settings,
        CancellationToken cancellationToken)
    {
        var (exitCode, prefix) = await Run(git, root, ["rev-parse", "--show-prefix"], cancellationToken).ConfigureAwait(false);
        if (exitCode != 0 || prefix.Trim().Length != 0)
        {
            throw new InvalidOperationException("The root is not the top level of a Git repository; git settings and hooks apply only to a repository root.");
        }

        List<BaselineGitEntry> entries = [];
        foreach (var setting in settings)
        {
            var (code, output) = await Run(git, root, ["config", "--local", "--get-all", setting.Key], cancellationToken).ConfigureAwait(false);
            var status = code switch
            {
                // Exit code 1 means the key is not set.
                1 => BaselineStatus.Missing,
                0 => output.TrimEnd('\n').Split('\n') is [var only] && only == setting.Value ? BaselineStatus.Current : BaselineStatus.Unmanaged,
                _ => throw new InvalidOperationException($"git config could not read {setting.Key}."),
            };
            entries.Add(new BaselineGitEntry(setting.Key, setting.Value, status, setting.OverwriteExisting));
        }

        return entries;
    }

    /// <summary>Sets the entry's key to its value, replacing every existing value.</summary>
    /// <exception cref="InvalidOperationException"><c>git config</c> could not set the entry.</exception>
    /// <exception cref="TimeoutException">Git does not finish within 30 seconds; it has been stopped.</exception>
    public static Task Set(string root, BaselineGitEntry entry, CancellationToken cancellationToken) =>
        Set(GitProcess.Default, root, entry, cancellationToken);

    /// <summary>Sets the entry's key to its value, replacing every existing value, running Git through <paramref name="git"/>.</summary>
    /// <exception cref="InvalidOperationException"><c>git config</c> could not set the entry.</exception>
    /// <exception cref="TimeoutException">Git does not finish within the time limit of <paramref name="git"/>; it has been stopped.</exception>
    public static async Task Set(GitProcess git, string root, BaselineGitEntry entry, CancellationToken cancellationToken)
    {
        var (exitCode, _) = await Run(git, root, ["config", "--local", "--replace-all", entry.Key, entry.Value], cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"git config could not set {entry.Key}.");
        }
    }

    /// <summary>Runs <c>git init</c> in <paramref name="root"/> with <paramref name="branch"/> as the initial branch.</summary>
    /// <exception cref="InvalidOperationException"><c>git init</c> failed.</exception>
    /// <exception cref="TimeoutException">Git does not finish within 30 seconds; it has been stopped.</exception>
    public static Task Init(string root, string branch, CancellationToken cancellationToken) =>
        Init(GitProcess.Default, root, branch, cancellationToken);

    /// <summary>Runs <c>git init</c> in <paramref name="root"/> with <paramref name="branch"/> as the initial branch, through <paramref name="git"/>.</summary>
    /// <exception cref="InvalidOperationException"><c>git init</c> failed.</exception>
    /// <exception cref="TimeoutException">Git does not finish within the time limit of <paramref name="git"/>; it has been stopped.</exception>
    public static async Task Init(GitProcess git, string root, string branch, CancellationToken cancellationToken)
    {
        var (exitCode, _) = await Run(git, root, ["init", "-b", branch], cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException("git init failed.");
        }
    }

    private static async Task<(int ExitCode, string Output)> Run(GitProcess git, string root, string[] arguments, CancellationToken cancellationToken)
    {
        var startInfo = git.CreateStartInfo(root, arguments);
        try
        {
            var result = await git.Run(startInfo, MaxOutputBytes, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
            return (result.ExitCode, Encoding.UTF8.GetString(result.StandardOutput.Span));
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"git did not finish within {git.TimeoutSeconds} seconds.", exception);
        }
        catch (InvalidOperationException exception) when (exception.InnerException is Win32Exception)
        {
            throw new InvalidOperationException("Could not start git.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"git {arguments[0]} failed: {exception.Message}", exception);
        }
    }
}
