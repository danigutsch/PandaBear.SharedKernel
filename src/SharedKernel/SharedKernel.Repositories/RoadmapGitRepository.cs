using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using SharedKernel.Processes;

namespace SharedKernel.Repositories;

/// <summary>
/// Reads git state through bounded, time-limited git processes: no prompt, no credentials, no inherited <c>GIT_*</c>
/// variables, 8 MiB of standard output, 64 KiB of standard error, and 30 seconds per call.
/// </summary>
public sealed class RoadmapGitRepository
{
    private const int MaxOutputBytes = 8 * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly GitProcess _git;
    private readonly Func<ProcessStartInfo, CancellationToken, Task<RoadmapGitCommandResult>> _run;

    /// <summary>Creates a repository reader that runs the real git executable.</summary>
    public RoadmapGitRepository()
        : this(GitProcess.Default)
    {
    }

    /// <summary>Creates a repository reader that runs git through the injected <paramref name="run"/> delegate.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="run"/> is <see langword="null"/>.</exception>
    public RoadmapGitRepository(
        Func<ProcessStartInfo, CancellationToken, Task<RoadmapGitCommandResult>> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _git = GitProcess.Default;
        _run = run;
    }

    /// <summary>Creates a repository reader that finds and runs git through <paramref name="git"/>.</summary>
    internal RoadmapGitRepository(GitProcess git)
    {
        _git = git;
        _run = (startInfo, cancellationToken) => RunProcess(git, startInfo, cancellationToken);
    }

    /// <summary>Returns the commit SHA of HEAD in <paramref name="rootPath"/>.</summary>
    /// <exception cref="InvalidOperationException">The Git output is invalid or is not a SHA.</exception>
    public async Task<string> ReadHead(string rootPath, CancellationToken cancellationToken)
    {
        var result = await Run(
            rootPath,
            ["rev-parse", "--verify", "HEAD"],
            cancellationToken).ConfigureAwait(false);
        string head;
        try
        {
            head = StrictUtf8.GetString(result.StandardOutput.Span).Trim();
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidOperationException("Roadmap automation Git inspection returned invalid output.");
        }

        if (head.Length != 40 || !head.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
        {
            throw new InvalidOperationException("Roadmap automation Git inspection returned an invalid HEAD SHA.");
        }

        return head;
    }

    /// <summary>Returns the raw NUL-delimited porcelain v2 status including untracked files.</summary>
    public async Task<byte[]> ReadStatus(string rootPath, CancellationToken cancellationToken)
    {
        var result = await Run(
            rootPath,
            ["status", "--porcelain=v2", "-z", "--untracked-files=all", "--ignore-submodules=none"],
            cancellationToken).ConfigureAwait(false);
        return result.StandardOutput.ToArray();
    }

    /// <summary>Lists the root-level entries of the tree at <paramref name="revision"/>, requiring <paramref name="rootPath"/> to be the repository root.</summary>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="rootPath"/> is not the repository root.</exception>
    public async Task<string[]> ReadCommittedRootPaths(string rootPath, string revision, CancellationToken cancellationToken)
    {
        var prefix = await RunOffline(rootPath, ["rev-parse", "--show-prefix"], cancellationToken).ConfigureAwait(false);
        if (StrictUtf8.GetString(prefix.StandardOutput.Span).Trim().Length != 0)
        {
            throw new InvalidOperationException("Committed dependency inputs require the repository root.");
        }

        var result = await RunOffline(rootPath, ["ls-tree", "-z", "--name-only", "--full-tree", revision], cancellationToken).ConfigureAwait(false);
        return StrictUtf8.GetString(result.StandardOutput.Span).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Indicates whether <paramref name="revision"/> contains an entry at <paramref name="relativePath"/>.</summary>
    public async Task<bool> CommittedPathExists(string rootPath, string revision, string relativePath, CancellationToken cancellationToken)
    {
        var result = await RunOffline(rootPath, ["ls-tree", "-z", "--name-only", revision, "--", relativePath], cancellationToken).ConfigureAwait(false);
        return result.StandardOutput.Length != 0;
    }

    /// <summary>Returns the bytes of the file at <paramref name="relativePath"/> in <paramref name="revision"/>.</summary>
    public async Task<byte[]> ReadCommittedFile(string rootPath, string revision, string relativePath, CancellationToken cancellationToken)
    {
        var result = await RunOffline(rootPath, ["cat-file", "blob", revision + ":" + relativePath], cancellationToken).ConfigureAwait(false);
        return result.StandardOutput.ToArray();
    }

    private Task<RoadmapGitCommandResult> RunOffline(string rootPath, string[] arguments, CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(rootPath, arguments);
        startInfo.Environment["GIT_NO_LAZY_FETCH"] = "1";
        startInfo.Environment["GIT_NO_REPLACE_OBJECTS"] = "1";
        return Run(startInfo, cancellationToken);
    }

    private async Task<RoadmapGitCommandResult> Run(
        string rootPath,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        return await Run(CreateStartInfo(rootPath, arguments), cancellationToken).ConfigureAwait(false);
    }

    private async Task<RoadmapGitCommandResult> Run(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        var result = await _run(startInfo, cancellationToken).ConfigureAwait(false);
        if (result.OutputLimitExceeded || result.ErrorLimitExceeded || result.ExitCode != 0)
        {
            throw new InvalidOperationException("Roadmap automation Git inspection failed.");
        }

        return result;
    }

    private ProcessStartInfo CreateStartInfo(string rootPath, IEnumerable<string> arguments) =>
        _git.CreateStartInfo(rootPath, arguments);

    private static async Task<RoadmapGitCommandResult> RunProcess(
        GitProcess git,
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        CapturedProcessOutput result;
        try
        {
            result = await git.Run(startInfo, MaxOutputBytes, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"Roadmap automation Git inspection timed out after {git.TimeoutSeconds} seconds.", exception);
        }
        catch (InvalidOperationException exception) when (exception.InnerException is Win32Exception)
        {
            throw new InvalidOperationException("Roadmap automation could not start Git inspection.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException("Roadmap automation Git inspection failed.", exception);
        }

        return new RoadmapGitCommandResult(
            result.ExitCode,
            result.StandardOutput,
            result.StandardError,
            OutputLimitExceeded: false,
            ErrorLimitExceeded: false);
    }
}
