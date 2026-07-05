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
            Directory.Delete(Root, recursive: true);
        }
    }
}
