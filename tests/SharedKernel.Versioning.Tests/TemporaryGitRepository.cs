using SharedKernel.Versioning.Tool;

namespace SharedKernel.Versioning.Tests;

internal sealed class TemporaryGitRepository : IDisposable
{
    public TemporaryGitRepository()
    {
        Root = Path.Combine(Path.GetTempPath(), $"versioning-git-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
        CommandRunner.Run("git", ["init"], Root);
        CommandRunner.Run("git", ["config", "commit.gpgsign", "false"], Root);
        CommandRunner.Run("git", ["config", "user.email", "test@example.invalid"], Root);
        CommandRunner.Run("git", ["config", "user.name", "Versioning Test"], Root);
    }

    public string Root { get; }

    public void Commit(string fileName, string content, string message)
    {
        File.WriteAllText(Path.Combine(Root, fileName), content);
        CommandRunner.Run("git", ["add", fileName], Root);
        CommandRunner.Run("git", ["commit", "-m", message], Root);
    }

    public void Tag(string tag)
    {
        CommandRunner.Run("git", ["-c", "tag.gpgSign=false", "tag", tag], Root);
    }

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
