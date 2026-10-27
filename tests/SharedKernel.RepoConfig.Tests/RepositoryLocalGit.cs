namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// An executable named <c>git</c> in a repository root that records that it ran and fails, with the environment that
/// reaches it through a relative <c>PATH</c> entry ahead of the real Git.
/// </summary>
internal sealed class RepositoryLocalGit
{
    private readonly string _markerPath;

    private RepositoryLocalGit(string markerPath, IReadOnlyDictionary<string, string?> environmentVariables)
    {
        _markerPath = markerPath;
        EnvironmentVariables = environmentVariables;
    }

    /// <summary>Gets the environment variables that put the fake <c>git</c> ahead of the real one and name its marker file.</summary>
    public IReadOnlyDictionary<string, string?> EnvironmentVariables
    {
        get;
    }

    /// <summary>Gets a value indicating whether the fake <c>git</c> was executed, shown by its marker file existing.</summary>
    public bool Ran => File.Exists(_markerPath);

    /// <summary>Writes an executable fake <c>git</c> script into <paramref name="rootPath"/> and builds the environment that reaches it through a relative <c>PATH</c> entry.</summary>
    /// <param name="rootPath">The repository root that receives the script.</param>
    /// <param name="cancellationToken">The token that cancels writing the script.</param>
    public static async Task<RepositoryLocalGit> Create(string rootPath, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The repository-local Git requires a POSIX shell.");
        }

        var markerPath = Path.Combine(rootPath, "fake-git-ran");
        var fakeGitPath = Path.Combine(rootPath, "git");
        await File.WriteAllTextAsync(
            fakeGitPath,
            "#!/bin/sh\n: > \"$REPO_CONFIG_FAKE_GIT_MARKER\"\nexit 99\n",
            cancellationToken);
        File.SetUnixFileMode(
            fakeGitPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var relativeRoot = Path.GetRelativePath(Environment.CurrentDirectory, rootPath);
        Dictionary<string, string?> environmentVariables = new(StringComparer.Ordinal)
        {
            // A bare "git" would resolve through the relative entry to the fake, and shells read the empty entry as the
            // current directory.
            ["PATH"] = $"{relativeRoot}{Path.PathSeparator}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}",
            ["REPO_CONFIG_FAKE_GIT_MARKER"] = markerPath
        };
        return new RepositoryLocalGit(markerPath, environmentVariables);
    }
}
