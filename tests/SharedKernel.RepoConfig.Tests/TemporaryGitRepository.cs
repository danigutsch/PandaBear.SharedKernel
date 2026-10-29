using System.Text;

namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Disposable temporary Git repository with an isolated Git environment and helpers to stage, store, corrupt, and inspect repository state for RepoConfig tool tests.
/// </summary>
internal sealed class TemporaryGitRepository : IDisposable
{
    private readonly Testing.TemporaryGitRepository _repository = new();
    private readonly List<string> _linkedWorktreePaths = [];

    /// <summary>
    /// Gets the absolute path of the repository root.
    /// </summary>
    public string RootPath => _repository.RootPath;

    /// <summary>
    /// Runs git init and disables core.autocrlf.
    /// </summary>
    public async Task Initialize(CancellationToken cancellationToken)
    {
        await RunGitChecked(["init", "--quiet"], cancellationToken);
        await RunGitChecked(["config", "core.autocrlf", "false"], cancellationToken);
    }

    /// <summary>
    /// Tries to initialize the repository with the SHA-256 object format and returns false when this Git version does not support it.
    /// </summary>
    public async Task<bool> TryInitializeSha256(CancellationToken cancellationToken)
    {
        var result = await RunGit(["init", "--quiet", "--object-format=sha256"], cancellationToken);
        if (result.ExitCode == 0)
        {
            await RunGitChecked(["config", "core.autocrlf", "false"], cancellationToken);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Adds a detached linked worktree at HEAD and returns its path.
    /// </summary>
    public async Task<string> CreateLinkedWorktree(CancellationToken cancellationToken)
    {
        var linkedRoot = RootPath + "-linked-" + Guid.NewGuid().ToString("N");
        await RunGitChecked(
            ["worktree", "add", "--quiet", "--detach", linkedRoot, "HEAD"],
            cancellationToken);
        _linkedWorktreePaths.Add(linkedRoot);
        return linkedRoot;
    }

    /// <summary>
    /// Writes raw bytes to a repository-relative path, creating missing directories.
    /// </summary>
    public void WriteBytes(string relativePath, ReadOnlySpan<byte> content)
    {
        var path = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? RootPath);
        File.WriteAllBytes(path, content);
    }

    /// <summary>
    /// Writes UTF-8 encoded text to a repository-relative path.
    /// </summary>
    public void WriteText(string relativePath, string content) =>
        WriteBytes(relativePath, Encoding.UTF8.GetBytes(content));

    /// <summary>
    /// Deletes a working tree file.
    /// </summary>
    public void Delete(string relativePath) =>
        File.Delete(Path.Combine(RootPath, relativePath));

    /// <summary>
    /// Stages a path using a literal pathspec.
    /// </summary>
    public async Task Stage(string relativePath, CancellationToken cancellationToken) =>
        await RunGitChecked(["add", "--", $":(literal){Normalize(relativePath)}"], cancellationToken);

    /// <summary>
    /// Writes the file as a Git blob without staging it and returns the object ID.
    /// </summary>
    public async Task<string> StoreBlob(string relativePath, CancellationToken cancellationToken)
    {
        var result = await RunGitChecked(["hash-object", "-w", "--", Normalize(relativePath)], cancellationToken);
        return result.StandardOutput.Trim();
    }

    /// <summary>
    /// Adds an index entry with the given mode and object ID without touching the working tree.
    /// </summary>
    public async Task SetIndexEntry(string mode, string objectId, string relativePath, CancellationToken cancellationToken) =>
        await RunGitChecked(
            ["update-index", "--add", "--cacheinfo", $"{mode},{objectId},{Normalize(relativePath)}"],
            cancellationToken);

    /// <summary>
    /// Runs the RepoConfig tool in process with the isolated Git environment plus any extra environment variables, defaulting the working directory to the repository root.
    /// </summary>
    public async Task<CommandResult> RunCommand(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        List<EnvironmentVariableScope> environmentScopes = [];
        foreach (var variable in _repository.GitEnvironment)
        {
            environmentScopes.Add(new EnvironmentVariableScope(variable.Key, variable.Value));
        }

        if (environment is not null)
        {
            foreach (var variable in environment)
            {
                environmentScopes.Add(new EnvironmentVariableScope(variable.Key, variable.Value));
            }
        }

        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        using var error = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            var exitCode = await RepoConfigToolRunner.Run(
                arguments,
                output,
                error,
                workingDirectory ?? RootPath,
                cancellationToken);
            return new CommandResult(exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            foreach (var scope in environmentScopes.AsEnumerable().Reverse())
            {
                scope.Dispose();
            }
        }
    }

    /// <summary>
    /// Runs the git executable in the repository root with the isolated Git environment.
    /// </summary>
    public async Task<TestProcessResult> RunGit(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        await _repository.TryRunGit(arguments, cancellationToken);

    /// <summary>
    /// Overwrites the Git index with invalid bytes.
    /// </summary>
    public void CorruptIndex() =>
        File.WriteAllBytes(Path.Combine(RootPath, ".git", "index"), [0x00, 0xFF, 0x00]);

    /// <summary>
    /// Copies the Git index to a file in the repository root and returns the copy's path.
    /// </summary>
    public string CopyIndex(string fileName)
    {
        var copyPath = Path.Combine(RootPath, fileName);
        File.Copy(Path.Combine(RootPath, ".git", "index"), copyPath, overwrite: true);
        return copyPath;
    }

    /// <summary>
    /// Writes the content to .git/info/attributes.
    /// </summary>
    public void WriteInfoAttributes(string content)
    {
        var path = Path.Combine(RootPath, ".git", "info", "attributes");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? RootPath);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Writes a global attributes file and a Git config pointing core.attributesFile at it, and returns the config path.
    /// </summary>
    public string CreateGlobalAttributesConfig(string attributes)
    {
        var attributesPath = Path.Combine(RootPath, "global-attributes");
        var configPath = Path.Combine(RootPath, "global.gitconfig");
        File.WriteAllText(attributesPath, attributes);
        File.WriteAllText(
            configPath,
            $"[core]{Environment.NewLine}\tattributesFile = {attributesPath.Replace('\\', '/')}" + Environment.NewLine);
        return configPath;
    }

    /// <summary>
    /// Creates a POSIX shell git wrapper that misbehaves according to the TEXT_ENCODING_GIT_SHIM_MODE variable, and returns a PATH value that puts it first.
    /// </summary>
    public string CreateGitShim()
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The process protocol Git shim requires POSIX shell tools.");
        }

        var realGit = Environment.GetEnvironmentVariable("PATH")?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, "git"))
            .FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("git executable was not found.");
        var shimDirectory = Path.Combine(RootPath, ".git-shim");
        Directory.CreateDirectory(shimDirectory);
        var shimPath = Path.Combine(shimDirectory, "git");
        File.WriteAllText(
            shimPath,
            $$"""
            #!/bin/sh
            if [ -n "${TEXT_ENCODING_GIT_SHIM_ENVIRONMENT_LOG:-}" ]; then
              {
                echo git-shim
                [ -n "${GH_TOKEN+set}" ] && echo GH_TOKEN
                [ -n "${GITHUB_TOKEN+set}" ] && echo GITHUB_TOKEN
              } >> "$TEXT_ENCODING_GIT_SHIM_ENVIRONMENT_LOG"
            fi
            case "${TEXT_ENCODING_GIT_SHIM_MODE:-}" in
              oversized-attributes)
                case " $* " in
                  *" check-attr "*) dd if=/dev/zero bs=1048576 count=65 2>/dev/null; exit 0 ;;
                esac
                ;;
              malformed-blob)
                case " $* " in
                  *" cat-file --batch "*) printf 'malformed\n'; sleep 30; exit 0 ;;
                esac
                ;;
              oversized-blob)
                case " $* " in
                  *" cat-file --batch "*) IFS= read -r oid; printf '%s blob 67108865\n' "$oid"; sleep 30; exit 0 ;;
                esac
                ;;
              hanging-blob)
                case " $* " in
                  *" cat-file --batch "*) sleep 30; exit 0 ;;
                esac
                ;;
              lingering-blob)
                case " $* " in
                  *" cat-file --batch "*) IFS= read -r oid; printf '%s blob 6\nvalid\n\n' "$oid"; sleep 30; exit 0 ;;
                esac
                ;;
            esac
            exec '{{realGit.Replace("'", "'\\''", StringComparison.Ordinal)}}' "$@"
            """);
        File.SetUnixFileMode(
            shimPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return shimDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
    }

    /// <summary>
    /// Deletes the loose object file for the object ID.
    /// </summary>
    public void DeleteLooseObject(string objectId)
    {
        var objectPath = Path.Combine(RootPath, ".git", "objects", objectId[..2], objectId[2..]);
        TemporaryRepositoryDirectory.DeleteFile(objectPath);
    }

    /// <summary>
    /// Deletes linked worktrees and the repository directory.
    /// </summary>
    public void Dispose()
    {
        foreach (var linkedWorktreePath in _linkedWorktreePaths)
        {
            TemporaryRepositoryDirectory.Delete(linkedWorktreePath);
        }

        _repository.Dispose();
    }

    private async Task<TestProcessResult> RunGitChecked(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await RunGit(arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Temporary Git repository setup failed.");
        }

        return result;
    }

    private static string Normalize(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    /// <summary>
    /// Exit code and captured output of an in-process tool invocation.
    /// </summary>
    /// <param name="ExitCode">The process exit code.</param>
    /// <param name="StandardOutput">The text written to standard output.</param>
    /// <param name="StandardError">The text written to standard error.</param>
    internal readonly record struct CommandResult(int ExitCode, string StandardOutput, string StandardError);
}
