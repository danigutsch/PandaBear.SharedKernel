namespace PandaBear.Testing;

/// <summary>A disposable Git repository in a temporary directory, isolated from the user's Git configuration, with signing disabled.</summary>
public sealed class TemporaryGitRepository : IDisposable
{
    private readonly TemporaryDirectory directory = new();
    private readonly Dictionary<string, string?> environment = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="TemporaryGitRepository" /> class with an empty root directory and a private home directory.</summary>
    public TemporaryGitRepository()
    {
        var homePath = Path.Combine(directory.Path, ".home");
        Directory.CreateDirectory(homePath);
        environment["HOME"] = homePath;
        environment["XDG_CONFIG_HOME"] = Path.Combine(homePath, ".config");
    }

    /// <summary>Gets the absolute path of the repository root.</summary>
    public string RootPath => directory.Path;

    /// <summary>Runs <c>git init</c> and configures an unsigned commit identity and unmodified line endings.</summary>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>A task that completes when the repository is configured.</returns>
    public async Task Initialize(CancellationToken ct)
    {
        await RunGit(["init", "--quiet"], ct).ConfigureAwait(false);
        await RunGit(["config", "core.autocrlf", "false"], ct).ConfigureAwait(false);
        await RunGit(["config", "commit.gpgsign", "false"], ct).ConfigureAwait(false);
        await RunGit(["config", "tag.gpgSign", "false"], ct).ConfigureAwait(false);
        await RunGit(["config", "user.email", "test@example.invalid"], ct).ConfigureAwait(false);
        await RunGit(["config", "user.name", "Test Author"], ct).ConfigureAwait(false);
    }

    /// <summary>Writes a file under the repository root, creating its directories.</summary>
    /// <param name="relativePath">The path relative to <see cref="RootPath" />.</param>
    /// <param name="content">The UTF-8 text to write.</param>
    public void WriteText(string relativePath, string content)
    {
        var path = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? RootPath);
        File.WriteAllText(path, content);
    }

    /// <summary>Writes a file and commits it with the given message.</summary>
    /// <param name="fileName">The path of the file relative to <see cref="RootPath" />.</param>
    /// <param name="content">The UTF-8 text to write.</param>
    /// <param name="message">The commit message.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>A task that completes when the commit exists.</returns>
    public async Task Commit(string fileName, string content, string message, CancellationToken ct)
    {
        WriteText(fileName, content);
        await RunGit(["add", "--", fileName], ct).ConfigureAwait(false);
        await RunGit(["commit", "--quiet", "--message", message], ct).ConfigureAwait(false);
    }

    /// <summary>Creates a lightweight tag.</summary>
    /// <param name="tag">The tag name.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>A task that completes when the tag exists.</returns>
    public Task Tag(string tag, CancellationToken ct) => RunGit(["tag", tag], ct);

    /// <summary>Runs <c>git</c> in the repository root with the isolated environment.</summary>
    /// <param name="arguments">The Git arguments.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The exit code and captured output, whatever the exit code.</returns>
    public Task<TestProcessResult> TryRunGit(IReadOnlyList<string> arguments, CancellationToken ct) =>
        TestGit.TryRun(RootPath, arguments, ct, environment);

    /// <summary>Runs <c>git</c> in the repository root with the isolated environment and requires success.</summary>
    /// <param name="arguments">The Git arguments.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The standard output of Git.</returns>
    /// <exception cref="InvalidOperationException">Git exited with a non-zero code; the message carries its standard error.</exception>
    public async Task<string> RunGit(IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var result = await TryRunGit(arguments, ct).ConfigureAwait(false);
        return result.ExitCode == 0
            ? result.StandardOutput
            : throw new InvalidOperationException("git " + string.Join(' ', arguments) + " failed: " + result.StandardError);
    }

    /// <summary>Gets the environment variables applied on top of <see cref="TestGit.Environment" /> to every Git run.</summary>
    public IReadOnlyDictionary<string, string?> GitEnvironment
    {
        get
        {
            var merged = new Dictionary<string, string?>(TestGit.Environment, StringComparer.Ordinal);
            foreach (var variable in environment)
            {
                merged[variable.Key] = variable.Value;
            }

            return merged;
        }
    }

    /// <summary>Deletes the repository directory after clearing read-only attributes on its files. Can be called more than once.</summary>
    public void Dispose() => directory.Dispose();
}
