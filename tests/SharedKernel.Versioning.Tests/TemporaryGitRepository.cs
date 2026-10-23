using PandaBear.Versioning.Tool;

namespace PandaBear.Versioning.Tests;

internal sealed class TemporaryGitRepository : IDisposable
{
    public TemporaryGitRepository()
    {
        Root = Path.Combine(Path.GetTempPath(), $"versioning-git-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public async Task Initialize(CancellationToken ct)
    {
        await CommandRunner.Run("git", ["init"], Root, ct);
        await CommandRunner.Run("git", ["config", "commit.gpgsign", "false"], Root, ct);
        await CommandRunner.Run("git", ["config", "user.email", "test@example.invalid"], Root, ct);
        await CommandRunner.Run("git", ["config", "user.name", "Versioning Test"], Root, ct);
    }

    public async Task Commit(string fileName, string content, string message, CancellationToken ct)
    {
        await File.WriteAllTextAsync(Path.Combine(Root, fileName), content, ct);
        await CommandRunner.Run("git", ["add", fileName], Root, ct);
        await CommandRunner.Run("git", ["commit", "-m", message], Root, ct);
    }

    public Task Tag(string tag, CancellationToken ct) =>
        CommandRunner.Run("git", ["-c", "tag.gpgSign=false", "tag", tag], Root, ct);

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = false
            };
            foreach (var path in Directory.EnumerateFiles(Root, "*", options))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                }
            }

            Directory.Delete(Root, recursive: true);
        }
    }
}
