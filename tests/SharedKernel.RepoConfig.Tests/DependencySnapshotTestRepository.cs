using System.Text.Json;

namespace PandaBear.RepoConfig.Tests;

/// <summary>A temporary Git repository with a solution, a project, and an optional lock file for dependency snapshot tests.</summary>
internal sealed class DependencySnapshotTestRepository : IDisposable
{
    private readonly TemporaryGitRepository repository = new();

    /// <summary>The repository root directory.</summary>
    public string RootPath => repository.RootPath;

    /// <summary>The commit hash of the latest commit made by <see cref="Commit"/>.</summary>
    public string Head { get; private set; } = string.Empty;

    /// <summary>Creates a committed repository, writing the lock file only when <paramref name="lockJson"/> is given.</summary>
    public static async Task<DependencySnapshotTestRepository> Create(string? lockJson, string? solution = null)
    {
        var fixture = new DependencySnapshotTestRepository();
        await fixture.repository.Initialize(TestContext.Current.CancellationToken);
        fixture.repository.WriteText(".gitignore", ".home/\nTestResults/\n");
        fixture.repository.WriteText("Example.slnx", solution ?? "<Solution><Project Path=\"src/Example.csproj\" /></Solution>");
        fixture.repository.WriteText("src/Example.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        if (lockJson is not null)
        {
            fixture.WriteLock(lockJson);
        }
        await fixture.Commit();
        return fixture;
    }

    /// <summary>Writes <c>src/packages.lock.json</c>, replacing the <c>HASH</c> placeholder with a base64 content hash.</summary>
    public void WriteLock(string value) =>
        repository.WriteText("src/packages.lock.json", value.Replace("HASH", Convert.ToBase64String(new byte[64]), StringComparison.Ordinal));

    /// <summary>Stages every change, commits it without signing, and records the new <see cref="Head"/>.</summary>
    public async Task Commit()
    {
        var staged = await repository.RunGit(["add", "--all"], TestContext.Current.CancellationToken);
        staged.ExitCode.ShouldBe(0);
        var committed = await repository.RunGit(["-c", "user.name=Snapshot fixture", "-c", "user.email=snapshot@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "Fixture"], TestContext.Current.CancellationToken);
        committed.ExitCode.ShouldBe(0);
        var head = await repository.RunGit(["rev-parse", "HEAD"], TestContext.Current.CancellationToken);
        head.ExitCode.ShouldBe(0);
        Head = head.StandardOutput.Trim();
    }

    /// <summary>Runs the <c>dependency snapshot</c> command for the repository at the given commit and returns its result.</summary>
    public Task<TemporaryGitRepository.CommandResult> Run(string? destination = null, string? sha = null, IReadOnlyDictionary<string, string?>? environment = null) =>
        repository.RunCommand(["dependency", "snapshot", "--root", RootPath, "--sha", sha ?? Head, "--ref", "refs/heads/test", "--job", "123", "--correlator", "test-locks", "--output", destination ?? "TestResults/snapshot.json"], TestContext.Current.CancellationToken, environment: environment);

    /// <summary>Parses the snapshot file that the command wrote to <c>TestResults/snapshot.json</c>.</summary>
    public JsonDocument ReadSnapshot() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RootPath, "TestResults/snapshot.json")));

    /// <summary>Indicates whether the command wrote <c>TestResults/snapshot.json</c>.</summary>
    public bool SnapshotExists => File.Exists(Path.Combine(RootPath, "TestResults/snapshot.json"));

    /// <summary>Deletes the temporary repository.</summary>
    public void Dispose() => repository.Dispose();
}
