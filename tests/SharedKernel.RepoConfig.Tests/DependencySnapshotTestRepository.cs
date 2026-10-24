using System.Text.Json;

namespace PandaBear.RepoConfig.Tests;

internal sealed class DependencySnapshotTestRepository : IDisposable
{
    private readonly TemporaryGitRepository repository = new();

    public string RootPath => repository.RootPath;

    public string Head { get; private set; } = string.Empty;

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

    public void WriteLock(string value) =>
        repository.WriteText("src/packages.lock.json", value.Replace("HASH", Convert.ToBase64String(new byte[64]), StringComparison.Ordinal));

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

    public Task<TemporaryGitRepository.CommandResult> Run(string? destination = null, string? sha = null, IReadOnlyDictionary<string, string?>? environment = null) =>
        repository.RunCommand(["dependency", "snapshot", "--root", RootPath, "--sha", sha ?? Head, "--ref", "refs/heads/test", "--job", "123", "--correlator", "test-locks", "--output", destination ?? "TestResults/snapshot.json"], TestContext.Current.CancellationToken, environment: environment);

    public JsonDocument ReadSnapshot() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RootPath, "TestResults/snapshot.json")));

    public bool SnapshotExists => File.Exists(Path.Combine(RootPath, "TestResults/snapshot.json"));

    public void Dispose() => repository.Dispose();
}
