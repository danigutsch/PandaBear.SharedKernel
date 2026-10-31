using System.ComponentModel;
using System.Diagnostics;
using PandaBear.Tooling;

namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// Reads and writes repository-local Git settings through <c>git config --local</c>, started from an absolute path
/// without a shell. The repository's <c>.git/config</c> is never edited as text.
/// </summary>
internal static class BaselineGit
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Compares each setting with the repository's value.</summary>
    /// <exception cref="InvalidOperationException">The root is not the top level of a Git repository, or Git fails.</exception>
    public static async Task<IReadOnlyList<BaselineGitEntry>> Plan(
        string root,
        IReadOnlyList<BaselineGitConfigurationEntry> settings,
        CancellationToken cancellationToken)
    {
        var (exitCode, prefix) = await Run(root, ["rev-parse", "--show-prefix"], cancellationToken).ConfigureAwait(false);
        if (exitCode != 0 || prefix.Trim().Length != 0)
        {
            throw new InvalidOperationException("The root is not the top level of a Git repository; git settings and hooks apply only to a repository root.");
        }

        List<BaselineGitEntry> entries = [];
        foreach (var setting in settings)
        {
            var (code, output) = await Run(root, ["config", "--local", "--get-all", setting.Key], cancellationToken).ConfigureAwait(false);
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
    public static async Task Set(string root, BaselineGitEntry entry, CancellationToken cancellationToken)
    {
        var (exitCode, _) = await Run(root, ["config", "--local", "--replace-all", entry.Key, entry.Value], cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"git config could not set {entry.Key}.");
        }
    }

    private static async Task<(int ExitCode, string Output)> Run(string root, string[] arguments, CancellationToken cancellationToken)
    {
        var git = ExecutableResolver.Resolve("git", Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows())
            ?? throw new InvalidOperationException("Could not resolve git to an absolute executable path.");
        ProcessStartInfo startInfo = new(git)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = root,
        };

        // GIT_DIR, GIT_CONFIG_*, and the like would select another repository or configuration.
        foreach (var variable in startInfo.Environment.Keys.Where(static key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            startInfo.Environment.Remove(variable);
        }

        ChildProcessEnvironment.RemoveGitHubTokens(startInfo);
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = new CancellationTokenSource(CommandTimeout);
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Could not start git.");
            }
        }
        catch (Exception exception) when (exception is Win32Exception or NotSupportedException)
        {
            throw new InvalidOperationException("Could not start git.", exception);
        }

        // ponytail: output is read unbounded; the commands print one setting or one path.
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(execution.Token).ConfigureAwait(false);
            await Task.WhenAll(output, error).ConfigureAwait(false);
            return (process.ExitCode, await output.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException("git did not finish within 30 seconds.");
        }
    }
}
