namespace PandaBear.Secrets;

/// <summary>
/// Reads files as they were at a base revision, through git: an absolute git path, arguments one by one, no shell, an environment with no user
/// configuration, and a revision that cannot be an option, a path, or anything but a plain revision name.
/// </summary>
internal sealed class GitBase
{
    private const int MaxRevisionLength = 200;

    private static readonly Dictionary<string, string> GitEnvironment = new(StringComparer.Ordinal)
    {
        ["GIT_CONFIG_NOSYSTEM"] = "1",
        ["GIT_CONFIG_GLOBAL"] = "/dev/null",
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_OPTIONAL_LOCKS"] = "0",
    };

    private readonly IToolRunner _runner;
    private readonly string _git;
    private readonly string _root;
    private readonly string _revision;

    private GitBase(IToolRunner runner, string git, string root, string revision)
    {
        _runner = runner;
        _git = git;
        _root = root;
        _revision = revision;
    }

    /// <summary>Checks the revision's spelling, finds git, and confirms that the revision names a commit.</summary>
    /// <exception cref="InvalidOperationException">The revision is not a plain revision name, git is missing, or the revision is not a commit.</exception>
    public static async Task<GitBase> Open(SecretsPrograms programs, string root, string revision, CancellationToken cancellationToken)
    {
        if (revision.Length is 0 or > MaxRevisionLength || revision[0] == '-' || !revision.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '/' or '@' or '^' or '~' or '{' or '}' or '+' or ',' or '-'))
        {
            throw new InvalidOperationException("--base must be a plain revision name such as main, origin/main, HEAD~1, or a commit hash; it cannot start with '-' or contain ':', spaces, or control characters.");
        }

        var git = new GitBase(programs.Runner, programs.ExecutableResolver.Find("git") ?? throw new InvalidOperationException("git was not found on PATH."), root, revision);
        await git.Run(["rev-parse", "--verify", "--quiet", revision + "^{commit}"], cancellationToken).ConfigureAwait(false);
        return git;
    }

    /// <summary>Reads the key holders that <paramref name="relativePath"/> named at the base revision, or <see langword="null"/> when the file did not exist or cannot be read there.</summary>
    /// <exception cref="InvalidOperationException">Git failed.</exception>
    public async Task<SopsFileRecipients?> ReadHolders(string relativePath, CancellationToken cancellationToken)
    {
        // './' makes git resolve the path from the directory it runs in (-C root), as the working tree does, not from the top of the repository: with
        // the root in a subdirectory a plain 'rev:path' would read a different file, possibly an attacker's twin at the top level.
        var listing = await Run(["ls-tree", "-z", _revision, "--", "./" + relativePath], cancellationToken).ConfigureAwait(false);
        if (!listing.Contains(" blob ", StringComparison.Ordinal))
        {
            return null;
        }

        var content = await Run(["show", $"{_revision}:./{relativePath}"], cancellationToken).ConfigureAwait(false);
        var format = SopsSniffer.FormatOf(relativePath);
        if (format is null)
        {
            return null;
        }

        var holders = SopsMetadataReader.Read(content, format.Value);
        return holders.IsSuccess ? holders.Value : null;
    }

    private async Task<string> Run(string[] arguments, CancellationToken cancellationToken)
    {
        ToolResult result;
        try
        {
            result = await _runner.Run(new ToolRequest(_git, ["-C", _root, .. arguments], GitEnvironment, default, _root), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            throw new InvalidOperationException($"git could not run: {exception.Message}", exception);
        }

        using (result)
        {
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"git {arguments[0]} failed with exit code {result.ExitCode}.");
            }

            return System.Text.Encoding.UTF8.GetString(result.StandardOutput.AsSpan());
        }
    }
}
