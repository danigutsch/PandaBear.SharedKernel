using System.Globalization;
using System.Text;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Creates repositories and workspaces, and runs commands, for the link and line-ending verification tests.</summary>
internal static class LinkTestRepository
{
    /// <summary>Creates a Git repository, seeded with <see cref="LinkCorpus"/> when <paramref name="seedCorpus"/> is set.</summary>
    public static async Task<TemporaryGitRepository> CreateGit(bool seedCorpus, CancellationToken cancellationToken)
    {
        var repository = new TemporaryGitRepository();
        try
        {
            await repository.Initialize(cancellationToken);
            if (seedCorpus)
            {
                await LinkCorpus.Write(repository, cancellationToken);
            }

            return repository;
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }

    /// <summary>Creates a directory with an empty <c>docs</c> folder and the given consumer configuration.</summary>
    public static TemporaryRepoConfigWorkspace CreateWorkspace(string configuration)
    {
        var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".config/sharedkernel-repo.json", configuration);
        Directory.CreateDirectory(Path.Combine(workspace.RootPath, "docs"));
        return workspace;
    }

    /// <summary>Runs the tool with the given arguments in the workspace and returns its exit code, output, and error.</summary>
    public static async Task<TemporaryGitRepository.CommandResult> Run(
        TemporaryRepoConfigWorkspace workspace,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var exitCode = await RepoConfigToolRunner.Run(arguments, output, error, workspace.RootPath, cancellationToken);
        return new TemporaryGitRepository.CommandResult(exitCode, output.ToString(), error.ToString());
    }

    /// <summary>Joins lines as a text writer writes them.</summary>
    public static string Lines(params string[] lines)
    {
        var text = new StringBuilder();
        foreach (var line in lines)
        {
            text.Append(line).Append(Environment.NewLine);
        }

        return text.ToString();
    }
}
