using System.Text;

namespace SharedKernel.RepoConfig.Tests;

internal sealed class TemporaryGitRepository : IDisposable
{
    private readonly IReadOnlyDictionary<string, string?> _gitEnvironment;
    private readonly List<string> _linkedWorktreePaths = [];

    public TemporaryGitRepository()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "sharedkernel-repo-config-git-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        var homePath = Path.Combine(RootPath, ".home");
        Directory.CreateDirectory(homePath);
        _gitEnvironment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["GIT_ATTR_NOSYSTEM"] = "1",
            ["GIT_CONFIG_GLOBAL"] = OperatingSystem.IsWindows() ? "NUL" : "/dev/null",
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["HOME"] = homePath,
            ["XDG_CONFIG_HOME"] = Path.Combine(homePath, ".config")
        };
    }

    public string RootPath { get; }

    public async Task Initialize(CancellationToken cancellationToken)
    {
        await RunGitChecked(["init", "--quiet"], cancellationToken);
        await RunGitChecked(["config", "core.autocrlf", "false"], cancellationToken);
    }

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

    public async Task<string> CreateLinkedWorktree(CancellationToken cancellationToken)
    {
        var linkedRoot = RootPath + "-linked-" + Guid.NewGuid().ToString("N");
        await RunGitChecked(
            ["worktree", "add", "--quiet", "--detach", linkedRoot, "HEAD"],
            cancellationToken);
        _linkedWorktreePaths.Add(linkedRoot);
        return linkedRoot;
    }

    public void WriteBytes(string relativePath, ReadOnlySpan<byte> content)
    {
        var path = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? RootPath);
        File.WriteAllBytes(path, content);
    }

    public void WriteText(string relativePath, string content) =>
        WriteBytes(relativePath, Encoding.UTF8.GetBytes(content));

    public void Delete(string relativePath) =>
        File.Delete(Path.Combine(RootPath, relativePath));

    public async Task Stage(string relativePath, CancellationToken cancellationToken) =>
        await RunGitChecked(["add", "--", $":(literal){Normalize(relativePath)}"], cancellationToken);

    public async Task<string> StoreBlob(string relativePath, CancellationToken cancellationToken)
    {
        var result = await RunGitChecked(["hash-object", "-w", "--", Normalize(relativePath)], cancellationToken);
        return result.StandardOutput.Trim();
    }

    public async Task SetIndexEntry(string mode, string objectId, string relativePath, CancellationToken cancellationToken) =>
        await RunGitChecked(
            ["update-index", "--add", "--cacheinfo", $"{mode},{objectId},{Normalize(relativePath)}"],
            cancellationToken);

    public async Task<CommandResult> RunCommand(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        List<EnvironmentVariableScope> environmentScopes = [];
        foreach (var variable in _gitEnvironment)
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
            var exitCode = await RepoConfigToolApplication.Run(
                [.. arguments],
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

    public async Task<CiTestSelectionTestProcess.ProcessResult> RunGit(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        await CiTestSelectionTestProcess.Run("git", RootPath, arguments, cancellationToken, _gitEnvironment);

    public void CorruptIndex() =>
        File.WriteAllBytes(Path.Combine(RootPath, ".git", "index"), [0x00, 0xFF, 0x00]);

    public string CopyIndex(string fileName)
    {
        var copyPath = Path.Combine(RootPath, fileName);
        File.Copy(Path.Combine(RootPath, ".git", "index"), copyPath, overwrite: true);
        return copyPath;
    }

    public void WriteInfoAttributes(string content)
    {
        var path = Path.Combine(RootPath, ".git", "info", "attributes");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? RootPath);
        File.WriteAllText(path, content);
    }

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

    public void DeleteLooseObject(string objectId)
    {
        var objectPath = Path.Combine(RootPath, ".git", "objects", objectId[..2], objectId[2..]);
        TemporaryRepositoryDirectory.DeleteFile(objectPath);
    }

    public void Dispose()
    {
        foreach (var linkedWorktreePath in _linkedWorktreePaths)
        {
            TemporaryRepositoryDirectory.Delete(linkedWorktreePath);
        }

        TemporaryRepositoryDirectory.Delete(RootPath);
    }

    private async Task<CiTestSelectionTestProcess.ProcessResult> RunGitChecked(
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

    internal readonly record struct CommandResult(int ExitCode, string StandardOutput, string StandardError);
}
